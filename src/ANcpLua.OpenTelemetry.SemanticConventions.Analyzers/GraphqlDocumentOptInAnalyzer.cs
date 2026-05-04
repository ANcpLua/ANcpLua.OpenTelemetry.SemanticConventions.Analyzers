// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// OTSC0002: Flags <c>SetTag/AddTag/SetAttribute("graphql.document", …)</c>. v1.41.0
/// demoted <c>graphql.document</c> from <c>recommended</c> to <c>opt_in</c> due to
/// its high-cardinality and PII risk. Diagnostic surfaces as a hint, not a hard
/// failure — capture is still legal when explicitly enabled with sanitization.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GraphqlDocumentOptInAnalyzer : DiagnosticAnalyzer
{
    private const string GraphqlDocumentKey = "graphql.document";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.GraphqlDocumentIsOptIn);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
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

        var keyArg = TagSetterDetection.UnwrapConversion(invocation.Arguments[keyArgIndex].Value);
        if (keyArg.ConstantValue is not { HasValue: true, Value: string key })
        {
            return;
        }

        if (!string.Equals(key, GraphqlDocumentKey, System.StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.GraphqlDocumentIsOptIn,
            invocation.Arguments[keyArgIndex].Syntax.GetLocation()));
    }
}
