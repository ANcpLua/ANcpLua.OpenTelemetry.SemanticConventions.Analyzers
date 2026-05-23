// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace OpenTelemetry.SemanticConventions.Analyzers.Tests;

public class SupplementalSemconvMigrationAnalyzerTests
{
    private const string FakeTelemetry = """
        using System.Collections.Generic;

        public sealed class FakeSpan
        {
            public FakeSpan SetTag(string key, object? value) => this;
            public FakeSpan AddTag(string key, object? value) => this;
            public FakeSpan SetBaggage(string key, string? value) => this;
        }

        public sealed class ActivitySource
        {
            public FakeSpan? StartActivity(string name, object? kind = null, object? parent = null, IEnumerable<KeyValuePair<string, object?>>? tags = null) => null;
        }

        public sealed class TagList
        {
            public void Add(string key, object? value) { }
        }

        public sealed class Meter
        {
            public void CreateHistogram<T>(string name) { }
        }

        public sealed class Counter<T>
        {
            public void Add(T delta, params KeyValuePair<string, object?>[] tags) { }
        }

        public sealed class Histogram<T>
        {
            public void Record(T value, params KeyValuePair<string, object?>[] tags) { }
        }

        public readonly struct Measurement<T>
        {
            public Measurement(T value, params KeyValuePair<string, object?>[] tags) { }
        }

        public sealed class ResourceBuilder
        {
            public ResourceBuilder AddAttributes(IEnumerable<KeyValuePair<string, object?>> attributes) => this;
        }

        public readonly struct ActivityEvent
        {
            public ActivityEvent(string name, object? timestamp = null, IEnumerable<KeyValuePair<string, object?>>? tags = null) { }
        }
        """;

    [Fact]
    public async Task Exact_Renamed_Literal_In_Production_Telemetry_Reports_Error()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(Meter meter)
                {
                    meter.CreateHistogram<long>({|#0:"system.memory.shared"|});
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0030", DiagnosticSeverity.Error)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Deprecated_Metadata_Literal_Remains_Handled_By_OTSC0012_Not_Supplemental()
    {
        const string semconvFixture = """
            namespace OpenTelemetry.SemanticConventions
            {
                public static class HttpAttributes
                {
                    [System.Obsolete("Replaced by http.request.method.")]
                    public const string AttributeHttpMethod = "http.method";
                }
            }
            """;

        const string testCode = FakeTelemetry + semconvFixture + """

            class C
            {
                void M(FakeSpan span)
                {
                    span.SetTag("http.method", "GET");
                }
            }
            """;

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task Removed_No_Replacement_Item_Reports_Manual_Review()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(FakeSpan span)
                {
                    span.SetTag({|#0:"message.id"|}, "42");
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0031", DiagnosticSeverity.Warning)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Generated_Value_Metadata_Is_Not_Duplicated_By_Supplemental_Catalog()
    {
        const string liveMetadataFixture = """

            namespace OpenTelemetry.SemanticConventions
            {
                public static class CloudAttributes
                {
                    public const string AttributeCloudPlatform = "cloud.platform";

                    public static class CloudPlatformValues
                    {
                        [System.Obsolete("Use 'azure.aks' instead.")]
                        public const string AzureAks = "azure_aks";
                    }
                }
            }
            """;

        const string testCode = FakeTelemetry + liveMetadataFixture + """

            class C
            {
                void M(FakeSpan span)
                {
                    span.SetTag("cloud.platform", "azure_aks");
                }
            }
            """;

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task Supplemental_Exact_Value_Rename_Reports_Error_When_Live_Metadata_Is_Absent()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(FakeSpan span)
                {
                    span.SetTag("cloud.platform", {|#0:"azure_aks"|});
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0030", DiagnosticSeverity.Error)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Supplemental_Removed_Value_Reports_Manual_Review()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(FakeSpan span)
                {
                    span.SetTag("db.system", {|#0:"coldfusion"|});
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0031", DiagnosticSeverity.Warning)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Context_Sensitive_Item_Downgrades_To_Manual_Review()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(FakeSpan span)
                {
                    span.SetTag({|#0:"error.message"|}, "boom");
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0031", DiagnosticSeverity.Warning)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Test_Project_Downgrades_To_Info()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(Meter meter)
                {
                    meter.CreateHistogram<long>({|#0:"system.memory.shared"|});
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0032", DiagnosticSeverity.Info)
            .WithLocation(0);

        var test = new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", "is_global = true\nbuild_property.IsTestProject = true\n"));

        await test.RunAsync();
    }

    [Fact]
    public async Task Migration_Catalog_Source_Does_Not_Self_Report()
    {
        const string testCode = FakeTelemetry + """

            class OpenTelemetryDeprecatedSemconvCatalog
            {
                void M(Meter meter)
                {
                    meter.CreateHistogram<long>("system.memory.shared");
                }
            }
            """;

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { ("/repo/src/OpenTelemetryDeprecatedSemconvCatalog.cs", testCode) },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task Schema_Translator_Class_Downgrades_To_Info()
    {
        const string testCode = FakeTelemetry + """

            class SchemaTranslator
            {
                void M(Meter meter)
                {
                    meter.CreateHistogram<long>({|#0:"system.memory.shared"|});
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0032", DiagnosticSeverity.Info)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Older_Schema_Url_Context_Downgrades_To_Info()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                private const string SchemaUrl = "https://opentelemetry.io/schemas/1.40.0";

                void M(Meter meter)
                {
                    meter.CreateHistogram<long>({|#0:"system.memory.shared"|});
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0032", DiagnosticSeverity.Info)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Current_Schema_Url_Context_Keeps_Production_Error()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                private const string SchemaUrl = "https://opentelemetry.io/schemas/1.41.0";

                void M(Meter meter)
                {
                    meter.CreateHistogram<long>({|#0:"system.memory.shared"|});
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0030", DiagnosticSeverity.Error)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Generated_Source_Is_Ignored_Or_Downgraded()
    {
        const string testCode = FakeTelemetry + """

            class GeneratedTelemetry
            {
                void M(Meter meter)
                {
                    meter.CreateHistogram<long>("system.memory.shared");
                }
            }
            """;

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { ("/repo/src/Generated/Telemetry.g.cs", testCode) },
            },
        }.RunAsync();
    }

    [Fact]
    public async Task Compatibility_Mode_Downgrades_Production_Error_To_Warning()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(Meter meter)
                {
                    meter.CreateHistogram<long>({|#0:"system.memory.shared"|});
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0031", DiagnosticSeverity.Warning)
            .WithLocation(0);

        var test = new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", "is_global = true\nbuild_property.OtelSemConvLegacyMode = compatibility\n"));

        await test.RunAsync();
    }

    [Fact]
    public async Task Off_Mode_Disables_Supplemental_Catalog_Diagnostics()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(Meter meter)
                {
                    meter.CreateHistogram<long>("system.memory.shared");
                }
            }
            """;

        var test = new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", "is_global = true\nbuild_property.OtelSemConvLegacyMode = off\n"));

        await test.RunAsync();
    }

    [Fact]
    public async Task Dictionary_Payloads_Report_Manual_Review_Not_Error()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M()
                {
                    var tags = new Dictionary<string, object?>();
                    tags.Add({|#0:"message.id"|}, "42");
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0031", DiagnosticSeverity.Warning)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task ResourceBuilder_AddAttributes_Dictionary_Indexer_Initializer_Reports_Visible_Key()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(ResourceBuilder resourceBuilder)
                {
                    resourceBuilder.AddAttributes(new Dictionary<string, object?>
                    {
                        [{|#0:"message.id"|}] = "42",
                    });
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0031", DiagnosticSeverity.Warning)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task ActivityEvent_Tags_Dictionary_Collection_Initializer_Reports_Visible_Key_Once()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                ActivityEvent Create()
                {
                    return new ActivityEvent(
                        "cache.prune",
                        tags: new Dictionary<string, object?>
                        {
                            { {|#0:"message.id"|}, "42" },
                        });
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0031", DiagnosticSeverity.Warning)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task ActivitySource_StartActivity_Tags_Report_Production_Manual_Review_Once()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(ActivitySource source)
                {
                    source.StartActivity(
                        "GET /users",
                        tags: new[]
                        {
                            new KeyValuePair<string, object?>({|#0:"message.id"|}, "42"),
                        });
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0031", DiagnosticSeverity.Warning)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task ResourceBuilder_AddAttributes_KeyValuePair_Array_Reports_Visible_Key_Once()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(ResourceBuilder resourceBuilder)
                {
                    resourceBuilder.AddAttributes(new[]
                    {
                        new KeyValuePair<string, object?>({|#0:"message.id"|}, "42"),
                    });
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0031", DiagnosticSeverity.Warning)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Metric_Names_Are_Recognized_On_Meter_Instruments()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(Meter meter)
                {
                    meter.CreateHistogram<long>({|#0:"system.memory.shared"|});
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0030", DiagnosticSeverity.Error)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task MetricCounter_Add_Tag_Payload_Reports_Production_Manual_Review()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(Counter<long> counter)
                {
                    counter.Add(1, new KeyValuePair<string, object?>({|#0:"message.id"|}, "42"));
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0031", DiagnosticSeverity.Warning)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Histogram_Record_Tag_Value_Reports_Production_Error()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(Histogram<long> histogram)
                {
                    histogram.Record(1, new KeyValuePair<string, object?>("cloud.platform", {|#0:"azure_aks"|}));
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0030", DiagnosticSeverity.Error)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Measurement_Tag_Payload_Reports_Production_Manual_Review()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                Measurement<long> M()
                {
                    return new Measurement<long>(1, new KeyValuePair<string, object?>({|#0:"message.id"|}, "42"));
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0031", DiagnosticSeverity.Warning)
            .WithLocation(0);

        await new CSharpAnalyzerTest<SupplementalSemconvMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Exact_Rename_CodeFix_Replaces_Only_Literal_Token()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(Meter meter)
                {
                    meter.CreateHistogram<long>({|#0:"system.memory.shared"|});
                }
            }
            """;

        const string fixedCode = FakeTelemetry + """

            class C
            {
                void M(Meter meter)
                {
                    meter.CreateHistogram<long>("system.memory.linux.shared");
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0030", DiagnosticSeverity.Error)
            .WithLocation(0);

        await new CSharpCodeFixTest<SupplementalSemconvMigrationAnalyzer, SupplementalSemconvMigrationCodeFixProvider, DefaultVerifier>
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Metric_Tag_Exact_Value_CodeFix_Replaces_Only_Value_Literal()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(Histogram<long> histogram)
                {
                    histogram.Record(1, new KeyValuePair<string, object?>("cloud.platform", {|#0:"azure_aks"|}));
                }
            }
            """;

        const string fixedCode = FakeTelemetry + """

            class C
            {
                void M(Histogram<long> histogram)
                {
                    histogram.Record(1, new KeyValuePair<string, object?>("cloud.platform", "azure.aks"));
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0030", DiagnosticSeverity.Error)
            .WithLocation(0);

        await new CSharpCodeFixTest<SupplementalSemconvMigrationAnalyzer, SupplementalSemconvMigrationCodeFixProvider, DefaultVerifier>
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task ActivitySource_StartActivity_Tag_Exact_Value_CodeFix_Replaces_Only_Value_Literal()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(ActivitySource source)
                {
                    source.StartActivity(
                        "GET /users",
                        tags: new[]
                        {
                            new KeyValuePair<string, object?>("cloud.platform", {|#0:"azure_aks"|}),
                        });
                }
            }
            """;

        const string fixedCode = FakeTelemetry + """

            class C
            {
                void M(ActivitySource source)
                {
                    source.StartActivity(
                        "GET /users",
                        tags: new[]
                        {
                            new KeyValuePair<string, object?>("cloud.platform", "azure.aks"),
                        });
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0030", DiagnosticSeverity.Error)
            .WithLocation(0);

        await new CSharpCodeFixTest<SupplementalSemconvMigrationAnalyzer, SupplementalSemconvMigrationCodeFixProvider, DefaultVerifier>
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Exact_Value_Rename_CodeFix_Replaces_Only_Value_Literal()
    {
        const string testCode = FakeTelemetry + """

            class C
            {
                void M(FakeSpan span)
                {
                    span.SetTag("cloud.platform", {|#0:"azure_aks"|});
                }
            }
            """;

        const string fixedCode = FakeTelemetry + """

            class C
            {
                void M(FakeSpan span)
                {
                    span.SetTag("cloud.platform", "azure.aks");
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0030", DiagnosticSeverity.Error)
            .WithLocation(0);

        await new CSharpCodeFixTest<SupplementalSemconvMigrationAnalyzer, SupplementalSemconvMigrationCodeFixProvider, DefaultVerifier>
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }
}
