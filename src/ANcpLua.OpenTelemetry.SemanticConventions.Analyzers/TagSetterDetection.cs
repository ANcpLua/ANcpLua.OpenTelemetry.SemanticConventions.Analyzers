// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// Shared detection helpers for tag-setter callsites across the analyzers.
/// </summary>
internal static class TagSetterDetection
{
    /// <summary>
    /// Method names treated as a "set this telemetry tag" callsite. Matched by simple
    /// name against <see cref="IInvocationOperation.TargetMethod"/>; we do not pin the
    /// receiver type because consumers use Activity, Span, TagList, custom builders,
    /// and OTel-extension wrappers interchangeably.
    /// </summary>
    public static readonly ImmutableHashSet<string> TagSetterMethodNames = ImmutableHashSet.Create(
        "SetTag", "AddTag", "SetAttribute", "AddAttribute");

    /// <summary>
    /// Walks an operation tree and yields every tag-setter invocation whose first
    /// argument is a constant string. Captures the constant key plus optional
    /// constant value (for callers that need to match on the value too).
    /// </summary>
    /// <summary>
    /// Unwraps surrounding implicit conversions (e.g. <c>string</c> → <c>object?</c>
    /// when calling <c>SetTag(string, object?)</c>) to expose the underlying operand
    /// whose <c>ConstantValue</c> we want to inspect.
    /// </summary>
    public static IOperation UnwrapConversion(IOperation operation)
    {
        while (operation is IConversionOperation conv && conv.IsImplicit)
        {
            operation = conv.Operand;
        }
        return operation;
    }

    public static void CollectTagSetterCalls(
        IOperation root,
        List<TagSetterCall> sink)
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

            // For extension methods, Arguments[0] is the implicit receiver — skip it.
            var keyArgIndex = invocation.TargetMethod.IsExtensionMethod ? 1 : 0;
            var valueArgIndex = keyArgIndex + 1;

            if (invocation.Arguments.Length <= keyArgIndex)
            {
                continue;
            }

            var keyArg = UnwrapConversion(invocation.Arguments[keyArgIndex].Value);
            if (keyArg.ConstantValue is { HasValue: true, Value: string key } && !string.IsNullOrEmpty(key))
            {
                string? value = null;
                if (invocation.Arguments.Length > valueArgIndex)
                {
                    var valueOp = UnwrapConversion(invocation.Arguments[valueArgIndex].Value);
                    if (valueOp.ConstantValue is { HasValue: true } secondConst && secondConst.Value is string s)
                    {
                        value = s;
                    }
                }

                sink.Add(new TagSetterCall(key, value, invocation, keyArgIndex));
            }
        }
    }
}

/// <summary>Captured tag-setter callsite with constant key and optional constant value.</summary>
internal readonly struct TagSetterCall
{
    public TagSetterCall(string key, string? value, IInvocationOperation invocation, int keyArgIndex)
    {
        Key = key;
        Value = value;
        Invocation = invocation;
        KeyArgIndex = keyArgIndex;
    }

    public string Key { get; }
    public string? Value { get; }
    public IInvocationOperation Invocation { get; }

    /// <summary>The index of the key argument in <see cref="Invocation"/>'s argument list (0 for instance methods, 1 for extension methods).</summary>
    public int KeyArgIndex { get; }

    /// <summary>The syntax location of the key argument (suitable for diagnostic reports).</summary>
    public Location KeyLocation => Invocation.Arguments[KeyArgIndex].Syntax.GetLocation();
}
