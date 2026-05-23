// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.SemanticConventions.Analyzers;

internal enum SemconvMigrationItemKind
{
    AttributeKey,
    AttributeValue,
    MetricName,
    EventName,
    SpanName,
    ResourceAttribute,
    EnumValue,
    Namespace,
    Group,
    GuidanceOnly,
}

internal enum SemconvMigrationKind
{
    ExactRename,
    ExactValueRename,
    RemovedNoReplacement,
    ContextSensitive,
    ManualReview,
    DeprecatedButGenerated,
}

internal enum SemconvLegacyMode
{
    Production,
    Compatibility,
    Off,
}

internal readonly struct SemconvMigrationCatalogEntry
{
    public SemconvMigrationCatalogEntry(
        string oldName,
        SemconvMigrationItemKind kind,
        string signal,
        string domain,
        string sinceVersion,
        ImmutableArray<string> replacementNames,
        SemconvMigrationKind migrationKind,
        string changelogVersion,
        string changelogEvidence,
        DiagnosticSeverity defaultProductionSeverity,
        DiagnosticSeverity fixtureSeverity)
    {
        OldName = oldName;
        Kind = kind;
        Signal = signal;
        Domain = domain;
        SinceVersion = sinceVersion;
        ReplacementNames = replacementNames;
        MigrationKind = migrationKind;
        ChangelogVersion = changelogVersion;
        ChangelogEvidence = changelogEvidence;
        DefaultProductionSeverity = defaultProductionSeverity;
        FixtureSeverity = fixtureSeverity;
    }

    public string OldName { get; }

    public SemconvMigrationItemKind Kind { get; }

    public string Signal { get; }

    public string Domain { get; }

    public string SinceVersion { get; }

    public ImmutableArray<string> ReplacementNames { get; }

    public SemconvMigrationKind MigrationKind { get; }

    public string ChangelogVersion { get; }

    public string ChangelogEvidence { get; }

    public DiagnosticSeverity DefaultProductionSeverity { get; }

    public DiagnosticSeverity FixtureSeverity { get; }

    public bool HasExactReplacement =>
        (MigrationKind == SemconvMigrationKind.ExactRename
            || MigrationKind == SemconvMigrationKind.ExactValueRename)
        && ReplacementNames.Length == 1
        && !string.IsNullOrWhiteSpace(ReplacementNames[0]);
}
