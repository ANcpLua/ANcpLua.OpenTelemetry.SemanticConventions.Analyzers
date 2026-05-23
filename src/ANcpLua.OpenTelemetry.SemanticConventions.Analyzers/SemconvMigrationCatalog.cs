// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using ANcpLua.Analyzers.Analyzers;

namespace OpenTelemetry.SemanticConventions.Analyzers;

internal static class SemconvMigrationCatalog
{
    public const int ExpectedCuratedMentionCount = 156;

    public static ImmutableArray<SemconvMigrationCatalogEntry> Entries { get; } = BuildEntries();

    private static readonly ImmutableDictionary<string, SemconvMigrationCatalogEntry> s_entriesByOldName =
        BuildEntriesByOldName(Entries);

    private static readonly ImmutableArray<SemconvMigrationCatalogEntry> s_wildcardEntries =
        BuildWildcardEntries(Entries);

    public static bool TryGetMigrationByName(
        string oldName,
        out SemconvMigrationCatalogEntry entry)
    {
        if (s_entriesByOldName.TryGetValue(oldName, out entry))
        {
            return true;
        }

        foreach (var wildcardEntry in s_wildcardEntries)
        {
            if (!MatchesWildcard(oldName, wildcardEntry.OldName))
            {
                continue;
            }

            entry = wildcardEntry;
            return true;
        }

        entry = default;
        return false;
    }

    public static bool IsSupplementalDiagnosticEntry(SemconvMigrationCatalogEntry entry) =>
        entry.MigrationKind != SemconvMigrationKind.DeprecatedButGenerated
        && entry.Kind != SemconvMigrationItemKind.GuidanceOnly;

    public static void Validate()
    {
        if (Entries.Length != ExpectedCuratedMentionCount)
        {
            throw new InvalidOperationException(
                $"Expected {ExpectedCuratedMentionCount} curated semantic-convention migration mentions, found {Entries.Length}.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in Entries)
        {
            Require(entry.OldName, nameof(entry.OldName), entry);
            Require(entry.Signal, nameof(entry.Signal), entry);
            Require(entry.Domain, nameof(entry.Domain), entry);
            Require(entry.SinceVersion, nameof(entry.SinceVersion), entry);
            Require(entry.ChangelogVersion, nameof(entry.ChangelogVersion), entry);
            Require(entry.ChangelogEvidence, nameof(entry.ChangelogEvidence), entry);

            if (!seen.Add(entry.OldName))
            {
                throw new InvalidOperationException($"Duplicate migration catalog key '{entry.OldName}'.");
            }

            if (entry.HasExactReplacement && entry.ReplacementNames.Length != 1)
            {
                throw new InvalidOperationException($"Exact migration '{entry.OldName}' must have one replacement.");
            }
        }
    }

    private static ImmutableArray<SemconvMigrationCatalogEntry> BuildEntries()
    {
        var builder = ImmutableArray.CreateBuilder<SemconvMigrationCatalogEntry>(
            OpenTelemetryDeprecatedSemconvCatalog.Entries.Length);

        foreach (var entry in OpenTelemetryDeprecatedSemconvCatalog.Entries)
        {
            if (ShouldExcludeFromCuratedInventory(entry))
            {
                continue;
            }

            builder.Add(Normalize(entry));
        }

        builder.Add(CreateGenAiChatHistoryEventEntry());
        return builder.ToImmutable();
    }

    private static bool ShouldExcludeFromCuratedInventory(SemconvMigrationCatalogEntry entry)
    {
        if (entry.Kind == SemconvMigrationItemKind.AttributeValue)
        {
            return true;
        }

        // OTSC0005 owns the v1.41.0 RPC server-span client.* removal directly.
        if (entry.OldName is "client.address" or "client.port")
        {
            return true;
        }

        // Keep the actual event name (`rpc.message`) and drop the legacy helper key.
        if (entry.OldName == "event.rpc.message")
        {
            return true;
        }

        return entry.OldName is "event.gen_ai.assistant.message"
            or "event.gen_ai.choice"
            or "event.gen_ai.system.message"
            or "event.gen_ai.tool.message"
            or "event.gen_ai.user.message";
    }

    private static SemconvMigrationCatalogEntry CreateGenAiChatHistoryEventEntry() =>
        new(
            "event.gen_ai.*",
            SemconvMigrationItemKind.EventName,
            "event",
            "gen_ai",
            "1.37.0",
            ImmutableArray.Create(
                "gen_ai.input.messages",
                "gen_ai.output.messages",
                "gen_ai.system_instructions",
                "gen_ai.client.inference.operation.details"),
            SemconvMigrationKind.ContextSensitive,
            "1.37.0",
            "GenAI chat history events were replaced by structured span attributes or the gen_ai.client.inference.operation.details event.",
            DiagnosticSeverity.Warning,
            DiagnosticSeverity.Info);

    private static SemconvMigrationCatalogEntry Normalize(SemconvMigrationCatalogEntry entry)
    {
        var isGeneratedMetadata = IsGeneratedMetadataEntry(entry);
        var migrationKind = isGeneratedMetadata
            ? SemconvMigrationKind.DeprecatedButGenerated
            : entry.MigrationKind;
        var sinceVersion = VersionOrKnownUnknown(entry.SinceVersion, entry.ChangelogVersion);
        var changelogVersion = VersionOrKnownUnknown(entry.ChangelogVersion, entry.SinceVersion);
        var evidence = string.IsNullOrWhiteSpace(entry.ChangelogEvidence)
            ? "Curated changelog migration entry."
            : entry.ChangelogEvidence;

        if (isGeneratedMetadata)
        {
            evidence = "Covered by generated [Obsolete] metadata when the consumer references OpenTelemetry.SemanticConventions. " + evidence;
        }

        return new SemconvMigrationCatalogEntry(
            entry.OldName,
            entry.Kind,
            entry.Signal,
            entry.Domain,
            sinceVersion,
            entry.ReplacementNames,
            migrationKind,
            changelogVersion,
            evidence,
            migrationKind == SemconvMigrationKind.DeprecatedButGenerated
                ? DiagnosticSeverity.Info
                : entry.DefaultProductionSeverity,
            entry.FixtureSeverity);
    }

    private static bool IsGeneratedMetadataEntry(SemconvMigrationCatalogEntry entry) =>
        entry.Kind == SemconvMigrationItemKind.AttributeKey
        && entry.MigrationKind == SemconvMigrationKind.ExactRename
        && (entry.ChangelogEvidence.StartsWith("semantic-conventions/model deprecated", StringComparison.Ordinal)
            || entry.ChangelogEvidence.StartsWith("semantic-conventions/model deprecated GenAI", StringComparison.Ordinal));

    private static string VersionOrKnownUnknown(string preferred, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred;
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        return "unknown";
    }

    private static ImmutableDictionary<string, SemconvMigrationCatalogEntry> BuildEntriesByOldName(
        ImmutableArray<SemconvMigrationCatalogEntry> entries)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, SemconvMigrationCatalogEntry>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (entry.OldName.IndexOf('*') >= 0)
            {
                continue;
            }

            if (!builder.ContainsKey(entry.OldName))
            {
                builder.Add(entry.OldName, entry);
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<SemconvMigrationCatalogEntry> BuildWildcardEntries(
        ImmutableArray<SemconvMigrationCatalogEntry> entries)
    {
        var builder = ImmutableArray.CreateBuilder<SemconvMigrationCatalogEntry>();

        foreach (var entry in entries)
        {
            if (entry.OldName.IndexOf('*') >= 0)
            {
                builder.Add(entry);
            }
        }

        return builder.ToImmutable();
    }

    private static bool MatchesWildcard(string oldName, string wildcard)
    {
        var starIndex = wildcard.IndexOf('*');
        if (starIndex < 0)
        {
            return string.Equals(oldName, wildcard, StringComparison.Ordinal);
        }

        var prefix = wildcard.Substring(0, starIndex);
        var suffix = wildcard.Substring(starIndex + 1);
        return oldName.StartsWith(prefix, StringComparison.Ordinal)
            && oldName.EndsWith(suffix, StringComparison.Ordinal);
    }

    private static void Require(string value, string propertyName, SemconvMigrationCatalogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Catalog entry '{entry.OldName}' has empty {propertyName}.");
        }
    }
}
