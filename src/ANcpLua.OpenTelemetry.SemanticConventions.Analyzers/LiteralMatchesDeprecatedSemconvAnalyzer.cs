// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// OTSC0012: Flags string literals in tag-setter callsites whose values match a
/// semantic-convention attribute name that is marked <c>[Obsolete]</c> in the
/// consumer's resolved <c>OpenTelemetry.SemanticConventions</c> assembly.
/// </summary>
/// <remarks>
/// Distinct from <see cref="DeprecatedSemconvAnalyzer"/> (OTSC0010), which fires
/// on direct typed-constant references. This rule catches the case where the
/// consumer hardcodes a literal, bypassing the typed constant entirely — a common
/// pattern in legacy code that the OTel SDK's <c>SetTag(string, …)</c> overloads encourage.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LiteralMatchesDeprecatedSemconvAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.LiteralMatchesDeprecatedSemconv);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var deprecationMap = BuildDeprecationMap(context.Compilation);
        if (deprecationMap.Count == 0)
        {
            return;
        }

        context.RegisterOperationAction(
            ctx => AnalyzeInvocation(ctx, deprecationMap),
            OperationKind.Invocation);
    }

    private static Dictionary<string, string> BuildDeprecationMap(Compilation compilation)
    {
        var map = new Dictionary<string, string>(System.StringComparer.Ordinal);
        Walk(compilation.GlobalNamespace, map);
        return map;
    }

    private static void Walk(INamespaceSymbol ns, Dictionary<string, string> map)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (!SemconvNamespace.IsAttributesType(type))
            {
                continue;
            }

            foreach (var member in type.GetMembers())
            {
                if (member is not IFieldSymbol field
                    || !field.IsConst
                    || field.Type.SpecialType != SpecialType.System_String
                    || field.DeclaredAccessibility != Accessibility.Public
                    || field.ConstantValue is not string value
                    || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                var obsolete = field.GetAttributes().FirstOrDefault(IsObsoleteAttribute);
                if (obsolete is null)
                {
                    continue;
                }

                var message = ExtractObsoleteMessage(obsolete);
                if (!map.ContainsKey(value))
                {
                    map[value] = message;
                }
            }
        }

        foreach (var nested in ns.GetNamespaceMembers())
        {
            Walk(nested, map);
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
        Dictionary<string, string> deprecationMap)
    {
        var invocation = (IInvocationOperation)context.Operation;

        if (!TagSetterDetection.TagSetterMethodNames.Contains(invocation.TargetMethod.Name))
        {
            return;
        }

        var keyArgIndex = invocation.TargetMethod.IsExtensionMethod ? 1 : 0;
        if (invocation.Arguments.Length <= keyArgIndex)
        {
            return;
        }

        var keyArg = invocation.Arguments[keyArgIndex];
        var keyArgValue = TagSetterDetection.UnwrapConversion(keyArg.Value);

        // Only fire on bare literals; OTSC0010 already handles typed-constant references.
        if (keyArgValue.Syntax is not LiteralExpressionSyntax)
        {
            return;
        }

        if (keyArgValue.ConstantValue is not { HasValue: true, Value: string literal }
            || string.IsNullOrEmpty(literal))
        {
            return;
        }

        if (!deprecationMap.TryGetValue(literal, out var deprecationMessage))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.LiteralMatchesDeprecatedSemconv,
            keyArg.Syntax.GetLocation(),
            literal,
            deprecationMessage));
    }
}
