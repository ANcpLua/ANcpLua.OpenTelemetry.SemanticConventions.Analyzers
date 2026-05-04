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

    private const string AttributesNamespace = "OpenTelemetry.SemanticConventions.Attributes";

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

        // Walk the SemConv attributes namespace. The package emits one type per
        // namespace (HttpAttributes, NetworkAttributes, …); we discover them by
        // crawling the namespace symbol rather than hardcoding ~90 type names.
        var ns = compilation.GetTypeByMetadataName(AttributesNamespace + ".HttpAttributes")?.ContainingNamespace
                 ?? ResolveNamespace(compilation.GlobalNamespace, AttributesNamespace);

        if (ns is null)
        {
            return catalog;
        }

        foreach (var type in ns.GetTypeMembers())
        {
            if (!type.Name.EndsWith("Attributes", System.StringComparison.Ordinal))
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
                    // First mapping wins. Different attribute classes occasionally
                    // expose the same const string (e.g. error.* across Error and
                    // Exception); preferring the first deterministic encounter is
                    // fine because we only need one valid suggestion.
                    if (!catalog.ContainsKey(value))
                    {
                        catalog.Add(value, qualified);
                    }
                }
            }
        }

        return catalog;
    }

    private static INamespaceSymbol? ResolveNamespace(INamespaceSymbol root, string fullName)
    {
        var parts = fullName.Split('.');
        var current = root;

        foreach (var part in parts)
        {
            INamespaceSymbol? found = null;
            foreach (var member in current.GetMembers(part))
            {
                if (member is INamespaceSymbol ns)
                {
                    found = ns;
                    break;
                }
            }

            if (found is null)
            {
                return null;
            }

            current = found;
        }

        return current;
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

    private static bool IsSemconvAttributesType(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString();
        return ns == AttributesNamespace
               || (ns is not null && ns.StartsWith(AttributesNamespace + ".", System.StringComparison.Ordinal));
    }
}
