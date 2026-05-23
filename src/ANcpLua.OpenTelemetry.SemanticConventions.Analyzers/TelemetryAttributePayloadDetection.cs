// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.SemanticConventions.Analyzers;

internal static class TelemetryAttributePayloadDetection
{
    public static void AnalyzeInvocation(
        IInvocationOperation invocation,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        if (TagSetterDetection.IsTagSetterInvocation(invocation))
        {
            AnalyzeTagSetterInvocation(invocation, report);
        }

        if (IsResourceBuilderAddAttributes(invocation)
            && TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var attributesArgument))
        {
            AnalyzePayload(attributesArgument.Value, report);
            return;
        }

        if (!IsInsideKnownTelemetryAttributePayload(invocation)
            && invocation.TargetMethod.Name == "Add"
            && IsStringKeyDictionary(invocation.Instance?.Type ?? invocation.TargetMethod.ContainingType))
        {
            AnalyzeAddLikeInvocation(invocation, report);
        }
    }

    public static void AnalyzeObjectCreation(
        IObjectCreationOperation objectCreation,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        if (IsKeyValuePairStringObject(objectCreation.Type)
            && !IsInsideKnownTelemetryAttributePayload(objectCreation))
        {
            AnalyzeKeyValuePairCreation(objectCreation, report);
        }

        if (IsActivityEventCreation(objectCreation.Type)
            && TryGetArgumentByNameOrOrdinal(objectCreation.Arguments, "tags", 2, out var tagsArgument))
        {
            AnalyzePayload(tagsArgument.Value, report);
        }
    }

    private static void AnalyzePayload(
        IOperation operation,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        var unwrapped = TagSetterDetection.UnwrapConversion(operation);

        if (unwrapped is IArrayCreationOperation arrayCreation)
        {
            if (arrayCreation.Initializer is not null)
            {
                AnalyzeArrayInitializer(arrayCreation.Initializer, report);
            }

            return;
        }

        if (unwrapped is IArrayInitializerOperation arrayInitializer)
        {
            AnalyzeArrayInitializer(arrayInitializer, report);
            return;
        }

        if (unwrapped is IObjectCreationOperation objectCreation)
        {
            if (IsKeyValuePairStringObject(objectCreation.Type))
            {
                AnalyzeKeyValuePairCreation(objectCreation, report);
            }

            if (objectCreation.Initializer is not null)
            {
                AnalyzeObjectInitializer(objectCreation.Initializer, report);
            }

            return;
        }

        if (unwrapped is IInvocationOperation { TargetMethod.Name: "Add" } invocation)
        {
            AnalyzeAddLikeInvocation(invocation, report);
            return;
        }

        if (unwrapped is ISimpleAssignmentOperation assignment)
        {
            AnalyzeIndexerAssignment(assignment, report);
        }
    }

    private static void AnalyzeTagSetterInvocation(
        IInvocationOperation invocation,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        if (!TagSetterDetection.TryGetTagSetterKeyArgument(invocation, out var keyArgument)
            || !TryGetKey(keyArgument.Value, out var key, out var keySyntax, out var keyIsBareLiteral))
        {
            return;
        }

        string? value = null;
        SyntaxNode? valueSyntax = null;
        if (TagSetterDetection.TryGetTagSetterValueArgument(invocation, out var valueArgument))
        {
            TryGetValue(valueArgument.Value, out value, out valueSyntax);
        }

        report(new TelemetryAttributePayloadLiteral(key, keySyntax, keyIsBareLiteral, value, valueSyntax));
    }

    private static void AnalyzeArrayInitializer(
        IArrayInitializerOperation initializer,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        foreach (var element in initializer.ElementValues)
        {
            AnalyzePayload(element, report);
        }
    }

    private static void AnalyzeObjectInitializer(
        IObjectOrCollectionInitializerOperation initializer,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        foreach (var initializerOperation in initializer.Initializers)
        {
            AnalyzePayload(initializerOperation, report);
        }
    }

    private static void AnalyzeKeyValuePairCreation(
        IObjectCreationOperation objectCreation,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        if (!TryGetArgumentByOrdinal(objectCreation.Arguments, extensionMethod: false, 0, out var keyArgument)
            || !TryGetArgumentByOrdinal(objectCreation.Arguments, extensionMethod: false, 1, out var valueArgument)
            || !TryGetKey(keyArgument.Value, out var key, out var keySyntax, out var keyIsBareLiteral))
        {
            return;
        }

        TryGetValue(valueArgument.Value, out var value, out var valueSyntax);
        report(new TelemetryAttributePayloadLiteral(key, keySyntax, keyIsBareLiteral, value, valueSyntax));
    }

    private static void AnalyzeAddLikeInvocation(
        IInvocationOperation invocation,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        if (!TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var keyArgument)
            || !TryGetKey(keyArgument.Value, out var key, out var keySyntax, out var keyIsBareLiteral))
        {
            return;
        }

        string? value = null;
        SyntaxNode? valueSyntax = null;
        if (TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 1, out var valueArgument))
        {
            TryGetValue(valueArgument.Value, out value, out valueSyntax);
        }

        report(new TelemetryAttributePayloadLiteral(key, keySyntax, keyIsBareLiteral, value, valueSyntax));
    }

    private static void AnalyzeIndexerAssignment(
        ISimpleAssignmentOperation assignment,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        if (!TryGetIndexerKey(assignment.Target, out var key, out var keySyntax, out var keyIsBareLiteral))
        {
            return;
        }

        TryGetValue(assignment.Value, out var value, out var valueSyntax);
        report(new TelemetryAttributePayloadLiteral(key, keySyntax, keyIsBareLiteral, value, valueSyntax));
    }

    private static bool TryGetKey(
        IOperation operation,
        [NotNullWhen(true)] out string? key,
        [NotNullWhen(true)] out SyntaxNode? syntax,
        out bool isBareLiteral)
    {
        var unwrapped = TagSetterDetection.UnwrapConversion(operation);
        isBareLiteral = unwrapped.Syntax is LiteralExpressionSyntax;
        if (TagSetterDetection.TryGetNonEmptyStringConstant(unwrapped, out key))
        {
            syntax = unwrapped.Syntax;
            return true;
        }

        syntax = null;
        return false;
    }

    private static bool TryGetValue(
        IOperation operation,
        [NotNullWhen(true)] out string? value,
        [NotNullWhen(true)] out SyntaxNode? syntax)
    {
        var unwrapped = TagSetterDetection.UnwrapConversion(operation);
        if (TagSetterDetection.TryGetStringConstant(unwrapped, out value))
        {
            syntax = unwrapped.Syntax;
            return true;
        }

        syntax = null;
        return false;
    }

    private static bool TryGetIndexerKey(
        IOperation operation,
        [NotNullWhen(true)] out string? key,
        [NotNullWhen(true)] out SyntaxNode? syntax,
        out bool isBareLiteral)
    {
        var unwrapped = TagSetterDetection.UnwrapConversion(operation);
        if (unwrapped is IPropertyReferenceOperation propertyReference)
        {
            foreach (var argument in propertyReference.Arguments)
            {
                if (IsLogicalArgument(argument, extensionMethod: false, 0)
                    && TryGetKey(argument.Value, out key, out syntax, out isBareLiteral))
                {
                    return true;
                }
            }
        }

        key = null;
        syntax = null;
        isBareLiteral = false;
        return false;
    }

    private static bool TryGetArgumentByOrdinal(
        ImmutableArray<IArgumentOperation> arguments,
        bool extensionMethod,
        int logicalParameterOrdinal,
        [NotNullWhen(true)] out IArgumentOperation? argument)
    {
        var parameterOrdinal = extensionMethod ? logicalParameterOrdinal + 1 : logicalParameterOrdinal;
        foreach (var candidate in arguments)
        {
            if (candidate.Parameter?.Ordinal == parameterOrdinal)
            {
                argument = candidate;
                return true;
            }
        }

        var fallbackIndex = arguments.Length > parameterOrdinal ? parameterOrdinal : logicalParameterOrdinal;
        if (arguments.Length > fallbackIndex)
        {
            argument = arguments[fallbackIndex];
            return true;
        }

        argument = null;
        return false;
    }

    private static bool TryGetArgumentByNameOrOrdinal(
        ImmutableArray<IArgumentOperation> arguments,
        string parameterName,
        int parameterOrdinal,
        [NotNullWhen(true)] out IArgumentOperation? argument)
    {
        foreach (var candidate in arguments)
        {
            if (string.Equals(candidate.Parameter?.Name, parameterName, StringComparison.Ordinal))
            {
                argument = candidate;
                return true;
            }
        }

        return TryGetArgumentByOrdinal(arguments, extensionMethod: false, parameterOrdinal, out argument);
    }

    private static bool IsLogicalArgument(
        IArgumentOperation argument,
        bool extensionMethod,
        int logicalParameterOrdinal)
    {
        var parameterOrdinal = extensionMethod ? logicalParameterOrdinal + 1 : logicalParameterOrdinal;
        return argument.Parameter?.Ordinal == parameterOrdinal;
    }

    private static bool IsResourceBuilderAddAttributes(IInvocationOperation invocation)
    {
        if (invocation.TargetMethod.Name != "AddAttributes")
        {
            return false;
        }

        if (invocation.TargetMethod.ContainingType.Name == "ResourceBuilder")
        {
            return true;
        }

        return invocation.TargetMethod.IsExtensionMethod
            && invocation.TargetMethod.Parameters.Length > 0
            && invocation.TargetMethod.Parameters[0].Type.Name == "ResourceBuilder";
    }

    private static bool IsInsideKnownTelemetryAttributePayload(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is not IArgumentOperation argument)
            {
                continue;
            }

            if (argument.Parent is IInvocationOperation invocation
                && IsResourceBuilderAddAttributes(invocation)
                && IsLogicalArgument(argument, invocation.TargetMethod.IsExtensionMethod, 0))
            {
                return true;
            }

            if (argument.Parent is IObjectCreationOperation objectCreation
                && IsActivityEventTagsArgument(objectCreation, argument))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsActivityEventTagsArgument(
        IObjectCreationOperation objectCreation,
        IArgumentOperation argument) =>
        IsActivityEventCreation(objectCreation.Type)
        && (string.Equals(argument.Parameter?.Name, "tags", StringComparison.Ordinal)
            || argument.Parameter?.Ordinal == 2);

    private static bool IsKeyValuePairStringObject(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol { Name: "KeyValuePair", TypeArguments.Length: 2 } named)
        {
            return false;
        }

        return named.TypeArguments[0].SpecialType == SpecialType.System_String;
    }

    private static bool IsStringKeyDictionary(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named || named.TypeArguments.Length < 2)
        {
            return false;
        }

        return named.Name is "Dictionary" or "IDictionary" or "IReadOnlyDictionary"
            && named.TypeArguments[0].SpecialType == SpecialType.System_String;
    }

    private static bool IsActivityEventCreation(ITypeSymbol? type) =>
        type?.Name is "ActivityEvent";
}

internal readonly struct TelemetryAttributePayloadLiteral
{
    public TelemetryAttributePayloadLiteral(
        string key,
        SyntaxNode keySyntax,
        bool keyIsBareLiteral,
        string? value,
        SyntaxNode? valueSyntax)
    {
        Key = key;
        KeySyntax = keySyntax;
        KeyIsBareLiteral = keyIsBareLiteral;
        Value = value;
        ValueSyntax = valueSyntax;
    }

    public string Key { get; }

    public SyntaxNode KeySyntax { get; }

    public bool KeyIsBareLiteral { get; }

    public string? Value { get; }

    public SyntaxNode? ValueSyntax { get; }
}
