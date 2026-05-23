// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace OpenTelemetry.SemanticConventions.Analyzers.Tests;

public class LiteralMatchesDeprecatedSemconvAnalyzerTests
{
    private const string SemconvFixture = """
        using System.Collections.Generic;

        #pragma warning disable CS0618
        namespace OpenTelemetry.SemanticConventions
        {
            public static class HttpAttributes
            {
                [System.Obsolete("Replaced by http.request.method.")]
                public const string AttributeHttpMethod = "http.method";

                public const string AttributeHttpRequestMethod = "http.request.method";
            }
        }

        public class FakeSpan
        {
            public FakeSpan SetTag(string key, object? value) => this;
            public FakeSpan SetBaggage(string key, string? value) => this;
        }

        public sealed class TagList
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
    public async Task BareLiteral_DeprecatedName_Reports_OTSC0012()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M(FakeSpan s)
                {
                    s.SetTag({|#0:"http.method"|}, "GET");
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0012", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("http.method", "Replaced by http.request.method.");

        await new CSharpAnalyzerTest<LiteralMatchesDeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task BareLiteral_NonDeprecatedName_Reports_Nothing()
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

        await new CSharpAnalyzerTest<LiteralMatchesDeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task TypedConstant_Skipped_By_OTSC0012()
    {
        // Typed constants are OTSC0010's job. OTSC0012 only fires on bare literals.
        const string testCode = SemconvFixture + """

            class C
            {
                void M(FakeSpan s)
                {
                    s.SetTag(OpenTelemetry.SemanticConventions.HttpAttributes.AttributeHttpMethod, "GET");
                }
            }
            """;

        await new CSharpAnalyzerTest<LiteralMatchesDeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task SetBaggage_DeprecatedLiteralKey_Reports_OTSC0012()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M(FakeSpan s)
                {
                    s.SetBaggage({|#0:"http.method"|}, "GET");
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0012", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("http.method", "Replaced by http.request.method.");

        await new CSharpAnalyzerTest<LiteralMatchesDeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task TagList_Add_DeprecatedLiteralKey_Reports_OTSC0012()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M(TagList tags)
                {
                    tags.Add({|#0:"http.method"|}, "GET");
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0012", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("http.method", "Replaced by http.request.method.");

        await new CSharpAnalyzerTest<LiteralMatchesDeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task ResourceBuilder_AddAttributes_DeprecatedLiteralKey_Reports_OTSC0012()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M(ResourceBuilder resourceBuilder)
                {
                    resourceBuilder.AddAttributes(new Dictionary<string, object?>
                    {
                        [{|#0:"http.method"|}] = "GET",
                    });
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0012", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("http.method", "Replaced by http.request.method.");

        await new CSharpAnalyzerTest<LiteralMatchesDeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task ActivityEvent_Tags_DeprecatedLiteralKey_Reports_OTSC0012_Once()
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
                            { {|#0:"http.method"|}, "GET" },
                        });
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0012", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("http.method", "Replaced by http.request.method.");

        await new CSharpAnalyzerTest<LiteralMatchesDeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Payload_TypedConstantKey_Remains_Skipped_By_OTSC0012()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M(ResourceBuilder resourceBuilder)
                {
                    resourceBuilder.AddAttributes(new[]
                    {
                        new KeyValuePair<string, object?>(
                            OpenTelemetry.SemanticConventions.HttpAttributes.AttributeHttpMethod,
                            "GET"),
                    });
                }
            }
            """;

        await new CSharpAnalyzerTest<LiteralMatchesDeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task MetricCounter_Add_DeprecatedLiteralKey_Reports_OTSC0012()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M(Counter<long> counter)
                {
                    counter.Add(1, new KeyValuePair<string, object?>({|#0:"http.method"|}, "GET"));
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0012", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("http.method", "Replaced by http.request.method.");

        await new CSharpAnalyzerTest<LiteralMatchesDeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Measurement_Tags_DeprecatedLiteralKey_Reports_OTSC0012()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                Measurement<long> M()
                {
                    return new Measurement<long>(1, new KeyValuePair<string, object?>({|#0:"http.method"|}, "GET"));
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0012", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("http.method", "Replaced by http.request.method.");

        await new CSharpAnalyzerTest<LiteralMatchesDeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }
}
