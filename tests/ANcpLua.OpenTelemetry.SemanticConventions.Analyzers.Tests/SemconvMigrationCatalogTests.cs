// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

using Qyl.OpenTelemetry.SemanticConventions.Analyzers;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests;

public class SemconvMigrationCatalogTests
{
    [Fact]
    public void Catalog_Has_Expected_Curated_Count_And_Required_Split()
    {
        SemconvMigrationCatalog.Validate();

        Assert.Equal(
            SemconvMigrationCatalog.ExpectedCuratedMentionCount,
            SemconvMigrationCatalog.Entries.Length);
        Assert.Equal(105, SemconvMigrationCatalog.Entries.Count(
            entry => entry.MigrationKind == SemconvMigrationKind.DeprecatedButGenerated));
        Assert.Equal(51, SemconvMigrationCatalog.Entries.Count(SemconvMigrationCatalog.IsSupplementalDiagnosticEntry));
        Assert.DoesNotContain(
            SemconvMigrationCatalog.Entries,
            entry => entry.Kind == SemconvMigrationItemKind.AttributeValue);
    }

    [Fact]
    public void Catalog_Classifies_Live_Metadata_And_Supplemental_Entries()
    {
        Assert.True(SemconvMigrationCatalog.TryGetMigrationByName("http.method", out var liveMetadataEntry));
        Assert.Equal(SemconvMigrationKind.DeprecatedButGenerated, liveMetadataEntry.MigrationKind);
        Assert.False(SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(liveMetadataEntry));

        Assert.True(SemconvMigrationCatalog.TryGetMigrationByName("system.memory.shared", out var exactSupplementalEntry));
        Assert.Equal(SemconvMigrationKind.ExactRename, exactSupplementalEntry.MigrationKind);
        Assert.True(SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(exactSupplementalEntry));
        Assert.Equal("system.memory.linux.shared", Assert.Single(exactSupplementalEntry.ReplacementNames));

        Assert.True(SemconvMigrationCatalog.TryGetMigrationByName("event.gen_ai.user.message", out var wildcardEntry));
        Assert.Equal("event.gen_ai.*", wildcardEntry.OldName);
        Assert.Equal(SemconvMigrationKind.ContextSensitive, wildcardEntry.MigrationKind);
    }

    [Fact]
    public void Catalog_Exposes_Supplemental_Value_Lookup_Outside_Curated_Name_Count()
    {
        Assert.DoesNotContain(
            SemconvMigrationCatalog.Entries,
            entry => entry.Kind == SemconvMigrationItemKind.AttributeValue);
        Assert.Equal(21, SemconvMigrationCatalog.SupplementalAttributeValueEntries.Length);

        Assert.True(SemconvMigrationCatalog.TryGetAttributeValueMigration(
            "cloud.platform",
            "azure_aks",
            out var exactValueEntry));
        Assert.Equal("cloud.platform=azure_aks", exactValueEntry.OldName);
        Assert.Equal(SemconvMigrationItemKind.AttributeValue, exactValueEntry.Kind);
        Assert.Equal(SemconvMigrationKind.ExactValueRename, exactValueEntry.MigrationKind);
        Assert.Equal("azure.aks", Assert.Single(exactValueEntry.ReplacementNames));

        Assert.True(SemconvMigrationCatalog.TryGetAttributeValueMigration(
            "db.system",
            "coldfusion",
            out var removedValueEntry));
        Assert.Equal(SemconvMigrationKind.RemovedNoReplacement, removedValueEntry.MigrationKind);
    }
}
