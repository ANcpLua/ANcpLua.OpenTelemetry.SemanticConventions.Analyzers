// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// OTSC0011: When a string literal passed to <c>SetTag/AddTag/SetAttribute</c>
/// matches a known <c>const string</c> field exposed by
/// <c>OpenTelemetry.SemanticConventions.Attributes.*</c>, suggest the typed constant.
/// </summary>
/// <remarks>
/// The catalog is built at compilation-start by walking the consumer's referenced
/// SemConv assembly via <see cref="Compilation.GetTypeByMetadataName"/>. If the
/// consumer doesn't reference <c>OpenTelemetry.SemanticConventions</c>, the analyzer
/// is a silent no-op.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferSemconvConstantAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Property-bag key carrying the fully-qualified suggested constant for code-fix providers.</summary>
    public const string SuggestedConstantKey = "SuggestedConstant";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.PreferSemconvConstant);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var catalog = BuildCatalog(context.Compilation);
        if (catalog.Count == 0)
        {
            return;
        }

        context.RegisterOperationAction(
            ctx => AnalyzeInvocation(ctx, catalog),
            OperationKind.Invocation);
    }

    private static Dictionary<string, string> BuildCatalog(Compilation compilation)
    {
        var catalog = new Dictionary<string, string>(System.StringComparer.Ordinal);

        // Walk every assembly-defined namespace. Collect public const string fields
        // from any "*Attributes" type whose namespace is or descends from
        // OpenTelemetry.SemanticConventions. This works for upstream's flat layout
        // (OpenTelemetry.SemanticConventions.HttpAttributes), qyl's nested layout
        // (Qyl.OpenTelemetry.SemanticConventions.Attributes.Http.HttpAttributes),
        // and any other consumer that shares the SemanticConventions namespace root.
        WalkNamespace(compilation.GlobalNamespace, catalog);

        return catalog;
    }

    private static void WalkNamespace(INamespaceSymbol ns, Dictionary<string, string> catalog)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (!type.Name.EndsWith("Attributes", System.StringComparison.Ordinal))
            {
                continue;
            }

            if (!SemconvNamespace.IsAttributesType(type))
            {
                continue;
            }

            foreach (var member in type.GetMembers())
            {
                if (member is not IFieldSymbol field
                    || !field.IsConst
                    || field.Type.SpecialType != SpecialType.System_String
                    || field.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (field.ConstantValue is string value && !string.IsNullOrEmpty(value))
                {
                    var qualified = $"{type.Name}.{field.Name}";
                    if (!catalog.ContainsKey(value))
                    {
                        catalog.Add(value, qualified);
                    }
                }
            }
        }

        foreach (var nested in ns.GetNamespaceMembers())
        {
            WalkNamespace(nested, catalog);
        }
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        Dictionary<string, string> catalog)
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

        // Only flag bare string literals. Symbol references (typed SemConv constants,
        // user-defined locals/consts, members) are out of scope: if the user already
        // wrote a named symbol, even one whose value matches the catalog, we should
        // not nag — the symbol may be intentional or carry meaning beyond the value.
        if (keyArgValue.Syntax is not LiteralExpressionSyntax)
        {
            return;
        }

        if (keyArgValue.ConstantValue is not { HasValue: true, Value: string literal }
            || string.IsNullOrEmpty(literal))
        {
            return;
        }

        if (!catalog.TryGetValue(literal, out var suggestedConstant))
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(SuggestedConstantKey, suggestedConstant);

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.PreferSemconvConstant,
            keyArg.Syntax.GetLocation(),
            properties,
            literal,
            suggestedConstant));
    }

}
