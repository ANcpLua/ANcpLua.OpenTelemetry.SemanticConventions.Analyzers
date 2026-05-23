// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace OpenTelemetry.SemanticConventions.Analyzers.Tests;

public class DeprecatedSemconvValueAnalyzerTests
{
    private const string SemconvFixture = """
        using System.Collections.Generic;

        #pragma warning disable CS0618
        namespace OpenTelemetry.SemanticConventions
        {
            public static class HttpAttributes
            {
                public const string AttributeHttpRequestMethod = "http.request.method";

                public static class HttpRequestMethodValues
                {
                    public const string Get = "GET";
                    public const string Post = "POST";

                    [System.Obsolete("Use the canonical RFC 9110 verb 'GET'.")]
                    public const string LegacyGet = "_LEGACY_GET";
                }
            }
        }

        public class FakeSpan
        {
            public FakeSpan SetTag(string key, object? value) => this;
            public FakeSpan AddBaggage(string key, string? value) => this;
        }

        public sealed class ActivityTagsCollection
        {
            public void Add(string key, object? value) { }
        }

        public sealed class ResourceBuilder
        {
            public ResourceBuilder AddAttributes(IEnumerable<KeyValuePair<string, object?>> attributes) => this;
        }

        public readonly struct ActivityEvent
        {
            public ActivityEvent(string name, object? timestamp = null, IEnumerable<KeyValuePair<string, object?>>? tags = null) { }
        }

        public sealed class Counter<T>
        {
            public void Add(T delta, params KeyValuePair<string, object?>[] tags) { }
        }

        public readonly struct Measurement<T>
        {
            public Measurement(T value, params KeyValuePair<string, object?>[] tags) { }
        }
        """;

    [Fact]
    public async Task DeprecatedValue_Reports_OTSC0014()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M(FakeSpan s)
                {
                    s.SetTag("http.request.method", {|#0:"_LEGACY_GET"|});
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0014", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("_LEGACY_GET", "http.request.method", "Use the canonical RFC 9110 verb 'GET'.");

        await new CSharpAnalyzerTest<DeprecatedSemconvValueAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task NonDeprecatedValue_Reports_Nothing()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M(FakeSpan s)
                {
                    s.SetTag("http.request.method", "GET");
                }
            }
            """;

        await new CSharpAnalyzerTest<DeprecatedSemconvValueAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task UnknownAttribute_Reports_Nothing()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M(FakeSpan s)
                {
                    s.SetTag("my.custom.tag", "_LEGACY_GET");
                }
            }
            """;

        await new CSharpAnalyzerTest<DeprecatedSemconvValueAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task AddBaggage_DeprecatedValue_Reports_OTSC0014()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M(FakeSpan s)
                {
                    s.AddBaggage("http.request.method", {|#0:"_LEGACY_GET"|});
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0014", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("_LEGACY_GET", "http.request.method", "Use the canonical RFC 9110 verb 'GET'.");

        await new CSharpAnalyzerTest<DeprecatedSemconvValueAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task ActivityTagsCollection_Add_DeprecatedValue_Reports_OTSC0014()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M(ActivityTagsCollection tags)
                {
                    tags.Add("http.request.method", {|#0:"_LEGACY_GET"|});
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0014", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("_LEGACY_GET", "http.request.method", "Use the canonical RFC 9110 verb 'GET'.");

        await new CSharpAnalyzerTest<DeprecatedSemconvValueAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task ResourceBuilder_AddAttributes_DeprecatedValue_Reports_OTSC0014()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M(ResourceBuilder resourceBuilder)
                {
                    resourceBuilder.AddAttributes(new[]
                    {
                        new KeyValuePair<string, object?>(
                            OpenTelemetry.SemanticConventions.HttpAttributes.AttributeHttpRequestMethod,
                            {|#0:"_LEGACY_GET"|}),
                    });
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0014", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("_LEGACY_GET", "http.request.method", "Use the canonical RFC 9110 verb 'GET'.");

        await new CSharpAnalyzerTest<DeprecatedSemconvValueAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task ActivityEvent_Tags_DeprecatedValue_Reports_OTSC0014_Once()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                ActivityEvent Create()
                {
                    return new ActivityEvent(
                        "legacy.event",
                        tags: new Dictionary<string, object?>
                        {
                            { "http.request.method", {|#0:"_LEGACY_GET"|} },
                        });
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0014", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("_LEGACY_GET", "http.request.method", "Use the canonical RFC 9110 verb 'GET'.");

        await new CSharpAnalyzerTest<DeprecatedSemconvValueAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task MetricCounter_Add_DeprecatedValue_Reports_OTSC0014()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M(Counter<long> counter)
                {
                    counter.Add(1, new KeyValuePair<string, object?>("http.request.method", {|#0:"_LEGACY_GET"|}));
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0014", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("_LEGACY_GET", "http.request.method", "Use the canonical RFC 9110 verb 'GET'.");

        await new CSharpAnalyzerTest<DeprecatedSemconvValueAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Measurement_Tags_DeprecatedValue_Reports_OTSC0014()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                Measurement<long> M()
                {
                    return new Measurement<long>(1, new KeyValuePair<string, object?>("http.request.method", {|#0:"_LEGACY_GET"|}));
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0014", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("_LEGACY_GET", "http.request.method", "Use the canonical RFC 9110 verb 'GET'.");

        await new CSharpAnalyzerTest<DeprecatedSemconvValueAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }
}
