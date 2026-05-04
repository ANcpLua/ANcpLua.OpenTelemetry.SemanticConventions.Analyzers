// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// OTSC0005: Flags <c>SetTag("client.address", …)</c> / <c>"client.port"</c> calls inside
/// a method that also contains a <c>SetTag("rpc.system", …)</c>. v1.41.0 removed
/// <c>client.*</c> from RPC server span definitions.
/// </summary>
/// <remarks>
/// Heuristic: scope analysis to the enclosing method. We treat the method as an
/// "RPC server span context" if any sibling tag-setter call uses the literal key
/// <c>"rpc.system"</c>, <c>"rpc.service"</c>, or <c>"rpc.method"</c>. False-positive
/// risk on RPC client spans (which legitimately use <c>server.*</c>, not <c>client.*</c>),
/// so we focus on the literal client.* keys upstream removed.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RpcServerClientAttributeAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> RpcContextKeys = ImmutableHashSet.Create(
        "rpc.system", "rpc.service", "rpc.method", "rpc.grpc.status_code");

    private static readonly ImmutableHashSet<string> ForbiddenClientKeys = ImmutableHashSet.Create(
        "client.address", "client.port");

    private static readonly ImmutableHashSet<string> TagSetterMethodNames = ImmutableHashSet.Create(
        "SetTag", "AddTag", "SetAttribute", "AddAttribute");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.RpcServerHasClientAddressAttribute);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationBlockAction(AnalyzeBlock);
    }

    private static void AnalyzeBlock(OperationBlockAnalysisContext context)
    {
        var keyedInvocations = new List<(string Key, IInvocationOperation Invocation)>();

        foreach (var blockOperation in context.OperationBlocks)
        {
            CollectTagSetterCalls(blockOperation, keyedInvocations);
        }

        if (keyedInvocations.Count == 0)
        {
            return;
        }

        var inRpcServerContext = false;
        foreach (var (key, _) in keyedInvocations)
        {
            if (RpcContextKeys.Contains(key))
            {
                inRpcServerContext = true;
                break;
            }
        }

        if (!inRpcServerContext)
        {
            return;
        }

        foreach (var (key, invocation) in keyedInvocations)
        {
            if (ForbiddenClientKeys.Contains(key))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.RpcServerHasClientAddressAttribute,
                    invocation.Arguments[0].Syntax.GetLocation(),
                    key));
            }
        }
    }

    private static void CollectTagSetterCalls(IOperation root, List<(string Key, IInvocationOperation Invocation)> sink)
    {
        foreach (var descendant in root.DescendantsAndSelf())
        {
            if (descendant is not IInvocationOperation invocation)
            {
                continue;
            }

            if (!TagSetterMethodNames.Contains(invocation.TargetMethod.Name))
            {
                continue;
            }

            if (invocation.Arguments.Length == 0)
            {
                continue;
            }

            var firstArg = invocation.Arguments[0].Value;
            if (firstArg.ConstantValue is { HasValue: true, Value: string key } && !string.IsNullOrEmpty(key))
            {
                sink.Add((key, invocation));
            }
        }
    }
}
