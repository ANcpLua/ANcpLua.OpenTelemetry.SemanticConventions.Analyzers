// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// OTSC0014: Flags <c>SetTag(name, value)</c> callsites where the value argument
/// is a constant string that matches a deprecated member of the corresponding
/// <c>*Values</c> nested class on the SemConv attribute type.
/// </summary>
/// <remarks>
/// Map is built from compiled symbols at compilation start. For each <c>*Attributes</c>
/// class, every nested <c>*Values</c> class is matched against its sibling
/// <c>Attribute*</c> constant by name (e.g. <c>HttpRequestMethodValues</c> ↔
/// <c>AttributeHttpRequestMethod</c>). <c>[Obsolete]</c>-marked value-class members
/// then become entries in a <c>(attrName, value) → message</c> map.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeprecatedSemconvValueAnalyzer : DiagnosticAnalyzer
{
    private const string ValuesSuffix = "Values";
    private const string AttributeConstPrefix = "Attribute";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.DeprecatedSemconvValue);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var map = BuildValueDeprecationMap(context.Compilation);
        if (map.Count == 0)
        {
            return;
        }

        context.RegisterOperationAction(
            ctx => AnalyzeInvocation(ctx, map),
            OperationKind.Invocation);
    }

    private static Dictionary<(string AttrName, string Value), string> BuildValueDeprecationMap(Compilation compilation)
    {
        var map = new Dictionary<(string, string), string>();
        Walk(compilation.GlobalNamespace, map);
        return map;
    }

    private static void Walk(INamespaceSymbol ns, Dictionary<(string, string), string> map)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (!SemconvNamespace.IsAttributesType(type))
            {
                continue;
            }

            // Build local map: AttributeXxx const name → its string value.
            Dictionary<string, string>? attrConstNameToValue = null;
            foreach (var member in type.GetMembers())
            {
                if (member is IFieldSymbol field
                    && field.IsConst
                    && field.Type.SpecialType == SpecialType.System_String
                    && field.ConstantValue is string attrName)
                {
                    attrConstNameToValue ??= new Dictionary<string, string>(System.StringComparer.Ordinal);
                    attrConstNameToValue[field.Name] = attrName;
                }
            }

            if (attrConstNameToValue is null)
            {
                continue;
            }

            foreach (var nested in type.GetTypeMembers())
            {
                if (!nested.Name.EndsWith(ValuesSuffix, System.StringComparison.Ordinal))
                {
                    continue;
                }

                var prefix = nested.Name.Substring(0, nested.Name.Length - ValuesSuffix.Length);
                var siblingConstName = AttributeConstPrefix + prefix;
                if (!attrConstNameToValue.TryGetValue(siblingConstName, out var attrName))
                {
                    continue;
                }

                foreach (var nestedMember in nested.GetMembers())
                {
                    if (nestedMember is not IFieldSymbol valueField
                        || !valueField.IsConst
                        || valueField.Type.SpecialType != SpecialType.System_String
                        || valueField.ConstantValue is not string value)
                    {
                        continue;
                    }

                    var obsolete = valueField.GetAttributes().FirstOrDefault(IsObsoleteAttribute);
                    if (obsolete is null)
                    {
                        continue;
                    }

                    var message = ExtractObsoleteMessage(obsolete);
                    var key = (attrName, value);
                    if (!map.ContainsKey(key))
                    {
                        map[key] = message;
                    }
                }
            }
        }

        foreach (var nestedNs in ns.GetNamespaceMembers())
        {
            Walk(nestedNs, map);
        }
    }

    private static bool IsObsoleteAttribute(AttributeData attribute)
    {
        var attrClass = attribute.AttributeClass;
        return attrClass is { Name: "ObsoleteAttribute", ContainingNamespace.Name: "System" };
    }

    private static string ExtractObsoleteMessage(AttributeData obsolete)
    {
        if (obsolete.ConstructorArguments.Length > 0
            && obsolete.ConstructorArguments[0].Value is string message
            && !string.IsNullOrEmpty(message))
        {
            return message;
        }
        return "no replacement message provided";
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        Dictionary<(string AttrName, string Value), string> map)
    {
        var invocation = (IInvocationOperation)context.Operation;

        if (!TagSetterDetection.TagSetterMethodNames.Contains(invocation.TargetMethod.Name))
        {
            return;
        }

        var keyArgIndex = invocation.TargetMethod.IsExtensionMethod ? 1 : 0;
        var valueArgIndex = keyArgIndex + 1;

        if (invocation.Arguments.Length <= valueArgIndex)
        {
            return;
        }

        var keyOp = TagSetterDetection.UnwrapConversion(invocation.Arguments[keyArgIndex].Value);
        var valueOp = TagSetterDetection.UnwrapConversion(invocation.Arguments[valueArgIndex].Value);

        if (keyOp.ConstantValue is not { HasValue: true, Value: string attrName }) return;
        if (valueOp.ConstantValue is not { HasValue: true, Value: string value }) return;

        if (!map.TryGetValue((attrName, value), out var message))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.DeprecatedSemconvValue,
            invocation.Arguments[valueArgIndex].Syntax.GetLocation(),
            value,
            attrName,
            message));
    }
}
