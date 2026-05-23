// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.SemanticConventions.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SupplementalSemconvMigrationAnalyzer : DiagnosticAnalyzer
{
    internal const string OldNameProperty = "OldName";
    internal const string ReplacementNameProperty = "ReplacementName";
    internal const string MigrationKindProperty = "MigrationKind";
    internal const string ItemKindProperty = "Kind";
    internal const string SignalProperty = "Signal";
    internal const string DomainProperty = "Domain";
    internal const string ChangelogVersionProperty = "ChangelogVersion";

    private static readonly ImmutableHashSet<string> BaggageMethodNames = ImmutableHashSet.Create(
        "SetBaggage",
        "AddBaggage");

    private static readonly ImmutableHashSet<string> MetricInstrumentMethodNames = ImmutableHashSet.Create(
        "CreateCounter",
        "CreateHistogram",
        "CreateGauge",
        "CreateObservableCounter",
        "CreateObservableGauge",
        "CreateObservableUpDownCounter",
        "CreateUpDownCounter");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        DiagnosticDescriptors.SupplementalExactSemconvMigration,
        DiagnosticDescriptors.SupplementalManualSemconvMigration,
        DiagnosticDescriptors.SupplementalCompatibilitySemconvMigration,
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var legacyMode = SemconvIntentClassifier.GetLegacyMode(context.Options);
        if (legacyMode == SemconvLegacyMode.Off)
        {
            return;
        }

        var liveObsoleteAttributeNames = BuildLiveObsoleteAttributeNames(context.Compilation);
        var liveObsoleteAttributeValues = BuildLiveObsoleteAttributeValues(context.Compilation);

        context.RegisterOperationAction(
            ctx => AnalyzeInvocation(ctx, legacyMode, liveObsoleteAttributeNames, liveObsoleteAttributeValues),
            OperationKind.Invocation);
        context.RegisterOperationAction(
            ctx => AnalyzeObjectCreation(ctx, legacyMode, liveObsoleteAttributeNames, liveObsoleteAttributeValues),
            OperationKind.ObjectCreation);
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (SemconvIntentClassifier.IsCatalogSource(invocation))
        {
            return;
        }

        AnalyzeMetricInstrumentName(context, invocation, legacyMode);
        AnalyzeActivityOrEventName(context, invocation, legacyMode);
        AnalyzeKeyValueInvocation(context, invocation, legacyMode, liveObsoleteAttributeNames, liveObsoleteAttributeValues);
    }

    private static void AnalyzeObjectCreation(
        OperationAnalysisContext context,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues)
    {
        var objectCreation = (IObjectCreationOperation)context.Operation;
        if (SemconvIntentClassifier.IsCatalogSource(objectCreation))
        {
            return;
        }

        if (IsKeyValuePairStringObject(objectCreation.Type)
            && TryGetArgumentByOrdinal(objectCreation.Arguments, extensionMethod: false, 0, out var keyArgument)
            && TryGetArgumentByOrdinal(objectCreation.Arguments, extensionMethod: false, 1, out var valueArgument)
            && TryGetBareLiteral(keyArgument.Value, out var key, out var keySyntax))
        {
            ReportNameIfCatalogOnly(
                context,
                key,
                keySyntax,
                legacyMode,
                liveObsoleteAttributeNames,
                isProductionEmission: false);

            if (TryGetBareLiteral(valueArgument.Value, out var value, out var valueSyntax))
            {
                ReportValueIfCatalogOnly(
                    context,
                    key,
                    value,
                    valueSyntax,
                    legacyMode,
                    liveObsoleteAttributeValues,
                    isProductionEmission: false);
            }
        }

        if (IsActivityEventCreation(objectCreation.Type)
            && TryGetArgumentByOrdinal(objectCreation.Arguments, extensionMethod: false, 0, out var nameArgument)
            && TryGetBareLiteral(nameArgument.Value, out var eventName, out var eventNameSyntax))
        {
            ReportNameIfCatalogOnly(
                context,
                eventName,
                eventNameSyntax,
                legacyMode,
                liveObsoleteAttributeNames,
                isProductionEmission: true);
        }
    }

    private static void AnalyzeKeyValueInvocation(
        OperationAnalysisContext context,
        IInvocationOperation invocation,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues)
    {
        var isProductionEmission = IsKnownProductionTagSetter(invocation);
        var isAmbiguousPayload = isProductionEmission is false && IsAmbiguousAttributePayload(invocation);
        if (!isProductionEmission && !isAmbiguousPayload)
        {
            return;
        }

        if (!TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var keyArgument)
            || !TryGetBareLiteral(keyArgument.Value, out var key, out var keySyntax))
        {
            return;
        }

        ReportNameIfCatalogOnly(
            context,
            key,
            keySyntax,
            legacyMode,
            liveObsoleteAttributeNames,
            isProductionEmission);

        if (!TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 1, out var valueArgument)
            || !TryGetBareLiteral(valueArgument.Value, out var value, out var valueSyntax))
        {
            return;
        }

        ReportValueIfCatalogOnly(
            context,
            key,
            value,
            valueSyntax,
            legacyMode,
            liveObsoleteAttributeValues,
            isProductionEmission);
    }

    private static void AnalyzeMetricInstrumentName(
        OperationAnalysisContext context,
        IInvocationOperation invocation,
        SemconvLegacyMode legacyMode)
    {
        if (!MetricInstrumentMethodNames.Contains(invocation.TargetMethod.Name)
            || !IsMeterLike(invocation.TargetMethod.ContainingType)
            || !TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var nameArgument)
            || !TryGetBareLiteral(nameArgument.Value, out var metricName, out var metricNameSyntax)
            || !SemconvMigrationCatalog.TryGetMigrationByName(metricName, out var entry)
            || entry.Kind != SemconvMigrationItemKind.MetricName)
        {
            return;
        }

        if (!SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(entry))
        {
            return;
        }

        ReportCatalogDiagnostic(context, entry, metricNameSyntax, legacyMode, isProductionEmission: true);
    }

    private static void AnalyzeActivityOrEventName(
        OperationAnalysisContext context,
        IInvocationOperation invocation,
        SemconvLegacyMode legacyMode)
    {
        if (invocation.TargetMethod.Name == "StartActivity"
            && IsActivitySourceLike(invocation.TargetMethod.ContainingType)
            && TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var spanArgument)
            && TryGetBareLiteral(spanArgument.Value, out var spanName, out var spanNameSyntax)
            && SemconvMigrationCatalog.TryGetMigrationByName(spanName, out var spanEntry)
            && spanEntry.Kind == SemconvMigrationItemKind.SpanName
            && SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(spanEntry))
        {
            ReportCatalogDiagnostic(context, spanEntry, spanNameSyntax, legacyMode, isProductionEmission: true);
        }

        if (invocation.TargetMethod.Name == "AddEvent"
            && TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var eventArgument)
            && TryGetBareLiteral(eventArgument.Value, out var eventName, out var eventNameSyntax)
            && SemconvMigrationCatalog.TryGetMigrationByName(eventName, out var eventEntry)
            && eventEntry.Kind == SemconvMigrationItemKind.EventName
            && SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(eventEntry))
        {
            ReportCatalogDiagnostic(context, eventEntry, eventNameSyntax, legacyMode, isProductionEmission: true);
        }
    }

    private static void ReportNameIfCatalogOnly(
        OperationAnalysisContext context,
        string key,
        LiteralExpressionSyntax syntax,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        bool isProductionEmission)
    {
        if (liveObsoleteAttributeNames.Contains(key)
            || !SemconvMigrationCatalog.TryGetMigrationByName(key, out var entry))
        {
            return;
        }

        if (entry.Kind == SemconvMigrationItemKind.MetricName
            || entry.Kind == SemconvMigrationItemKind.SpanName
            || !SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(entry))
        {
            return;
        }

        ReportCatalogDiagnostic(context, entry, syntax, legacyMode, isProductionEmission);
    }

    private static void ReportValueIfCatalogOnly(
        OperationAnalysisContext context,
        string key,
        string value,
        LiteralExpressionSyntax syntax,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeValues,
        bool isProductionEmission)
    {
        var valueKey = key + "=" + value;
        if (liveObsoleteAttributeValues.Contains(valueKey)
            || !TryGetAttributeValueMigration(key, value, out var entry))
        {
            return;
        }

        ReportCatalogDiagnostic(context, entry, syntax, legacyMode, isProductionEmission);
    }

    private static void ReportCatalogDiagnostic(
        OperationAnalysisContext context,
        SemconvMigrationCatalogEntry entry,
        LiteralExpressionSyntax syntax,
        SemconvLegacyMode legacyMode,
        bool isProductionEmission)
    {
        var compatibilityContext = SemconvIntentClassifier.IsCompatibilityOrTestContext(context)
            || SemconvIntentClassifier.IsGeneratedSource(context.Operation);

        var descriptor = SelectDescriptor(entry, legacyMode, compatibilityContext, isProductionEmission);
        var replacement = entry.ReplacementNames.Length == 1 ? entry.ReplacementNames[0] : "";
        var evidence = string.IsNullOrEmpty(entry.ChangelogEvidence)
            ? entry.MigrationKind.ToString()
            : entry.ChangelogEvidence;

        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(OldNameProperty, entry.OldName)
            .Add(ReplacementNameProperty, replacement)
            .Add(MigrationKindProperty, entry.MigrationKind.ToString())
            .Add(ItemKindProperty, entry.Kind.ToString())
            .Add(SignalProperty, entry.Signal)
            .Add(DomainProperty, entry.Domain)
            .Add(ChangelogVersionProperty, entry.ChangelogVersion);

        var args = descriptor.Id switch
        {
            "OTSC0030" => new object[] { entry.OldName, replacement, evidence },
            _ => new object[] { entry.OldName, evidence },
        };

        context.ReportDiagnostic(Diagnostic.Create(
            descriptor,
            syntax.GetLocation(),
            properties,
            args));
    }

    private static DiagnosticDescriptor SelectDescriptor(
        SemconvMigrationCatalogEntry entry,
        SemconvLegacyMode legacyMode,
        bool compatibilityContext,
        bool isProductionEmission)
    {
        if (compatibilityContext)
        {
            return DiagnosticDescriptors.SupplementalCompatibilitySemconvMigration;
        }

        if (legacyMode == SemconvLegacyMode.Compatibility)
        {
            return DiagnosticDescriptors.SupplementalManualSemconvMigration;
        }

        return entry.HasExactReplacement && isProductionEmission
            ? DiagnosticDescriptors.SupplementalExactSemconvMigration
            : DiagnosticDescriptors.SupplementalManualSemconvMigration;
    }

    private static bool IsKnownProductionTagSetter(IInvocationOperation invocation)
    {
        if (TagSetterDetection.IsTagSetterInvocation(invocation)
            || BaggageMethodNames.Contains(invocation.TargetMethod.Name))
        {
            return true;
        }

        return invocation.TargetMethod.Name == "Add"
            && IsTelemetryTagCollection(invocation.Instance?.Type ?? invocation.TargetMethod.ContainingType);
    }

    private static bool IsAmbiguousAttributePayload(IInvocationOperation invocation)
    {
        if (invocation.TargetMethod.Name != "Add")
        {
            return false;
        }

        return IsStringKeyDictionary(invocation.Instance?.Type ?? invocation.TargetMethod.ContainingType);
    }

    private static bool IsTelemetryTagCollection(ITypeSymbol? type) =>
        type?.Name is "TagList" or "ActivityTagsCollection";

    private static bool IsStringKeyDictionary(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named || named.TypeArguments.Length < 2)
        {
            return false;
        }

        return named.Name is "Dictionary" or "IDictionary" or "IReadOnlyDictionary"
            && named.TypeArguments[0].SpecialType == SpecialType.System_String;
    }

    private static bool IsKeyValuePairStringObject(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol { Name: "KeyValuePair", TypeArguments.Length: 2 } named)
        {
            return false;
        }

        return named.TypeArguments[0].SpecialType == SpecialType.System_String;
    }

    private static bool IsMeterLike(INamedTypeSymbol? type) =>
        type?.Name is "Meter";

    private static bool IsActivitySourceLike(INamedTypeSymbol? type) =>
        type?.Name is "ActivitySource";

    private static bool IsActivityEventCreation(ITypeSymbol? type) =>
        type?.Name is "ActivityEvent";

    private static bool TryGetAttributeValueMigration(
        string key,
        string value,
        out SemconvMigrationCatalogEntry entry)
    {
        entry = default;
        return false;
    }

    private static bool TryGetBareLiteral(
        IOperation operation,
        [NotNullWhen(true)] out string? value,
        [NotNullWhen(true)] out LiteralExpressionSyntax? syntax)
    {
        var unwrapped = TagSetterDetection.UnwrapConversion(operation);
        if (unwrapped.Syntax is LiteralExpressionSyntax literal
            && TagSetterDetection.TryGetNonEmptyStringConstant(unwrapped, out value))
        {
            syntax = literal;
            return true;
        }

        value = null;
        syntax = null;
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

    private static ImmutableHashSet<string> BuildLiveObsoleteAttributeNames(Compilation compilation)
    {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        WalkAttributeTypes(compilation.GlobalNamespace, type =>
        {
            foreach (var member in type.GetMembers())
            {
                if (member is not IFieldSymbol
                    {
                        IsConst: true,
                        Type.SpecialType: SpecialType.System_String,
                        ConstantValue: string value,
                    } field
                    || string.IsNullOrEmpty(value)
                    || !HasObsoleteAttribute(field))
                {
                    continue;
                }

                builder.Add(value);
            }
        });

        return builder.ToImmutable();
    }

    private static ImmutableHashSet<string> BuildLiveObsoleteAttributeValues(Compilation compilation)
    {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        WalkAttributeTypes(compilation.GlobalNamespace, type =>
        {
            Dictionary<string, string>? attrConstNameToValue = null;
            foreach (var member in type.GetMembers())
            {
                if (member is IFieldSymbol
                    {
                        IsConst: true,
                        Type.SpecialType: SpecialType.System_String,
                        ConstantValue: string attrName,
                    } field
                    && !string.IsNullOrEmpty(attrName))
                {
                    attrConstNameToValue ??= new Dictionary<string, string>(StringComparer.Ordinal);
                    attrConstNameToValue[field.Name] = attrName;
                }
            }

            if (attrConstNameToValue is null)
            {
                return;
            }

            foreach (var nested in type.GetTypeMembers())
            {
                if (!nested.Name.EndsWith("Values", StringComparison.Ordinal))
                {
                    continue;
                }

                var prefix = nested.Name.Substring(0, nested.Name.Length - "Values".Length);
                if (!attrConstNameToValue.TryGetValue("Attribute" + prefix, out var attrName))
                {
                    continue;
                }

                foreach (var nestedMember in nested.GetMembers())
                {
                    if (nestedMember is IFieldSymbol
                        {
                            IsConst: true,
                            Type.SpecialType: SpecialType.System_String,
                            ConstantValue: string value,
                        } valueField
                        && HasObsoleteAttribute(valueField))
                    {
                        builder.Add(attrName + "=" + value);
                    }
                }
            }
        });

        return builder.ToImmutable();
    }

    private static void WalkAttributeTypes(INamespaceSymbol ns, Action<INamedTypeSymbol> callback)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (SemconvNamespace.IsAttributesType(type))
            {
                callback(type);
            }
        }

        foreach (var nested in ns.GetNamespaceMembers())
        {
            WalkAttributeTypes(nested, callback);
        }
    }

    private static bool HasObsoleteAttribute(ISymbol symbol) =>
        symbol.GetAttributes().Any(static attribute =>
            attribute.AttributeClass is { Name: "ObsoleteAttribute", ContainingNamespace.Name: "System" });
}
