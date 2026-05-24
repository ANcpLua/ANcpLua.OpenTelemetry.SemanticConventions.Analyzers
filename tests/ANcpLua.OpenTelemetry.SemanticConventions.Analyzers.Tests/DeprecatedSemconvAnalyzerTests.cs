// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using Qyl.OpenTelemetry.SemanticConventions.Analyzers;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests;

public class DeprecatedSemconvAnalyzerTests
{
    private const string SemconvFixture = """
        #pragma warning disable CS0618
        namespace OpenTelemetry.SemanticConventions.Attributes
        {
            public static class HttpAttributes
            {
                [System.Obsolete("Replaced by http.request.method.")]
                public const string AttributeHttpMethod = "http.method";

                public const string AttributeHttpRequestMethod = "http.request.method";
            }

            public static class NetAttributes
            {
                [System.Obsolete("Replaced by network.local.address.")]
                public const string AttributeNetSockHostAddr = "net.sock.host.addr";
            }
        }
        """;

    [Fact]
    public async Task Reference_To_Obsolete_HttpAttribute_Reports_QYL0010()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M()
                {
                    var x = OpenTelemetry.SemanticConventions.Attributes.HttpAttributes.{|#0:AttributeHttpMethod|};
                }
            }
            """;

        var expected = new DiagnosticResult("QYL0010", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("HttpAttributes.AttributeHttpMethod", "Replaced by http.request.method.");

        await new CSharpAnalyzerTest<DeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Reference_To_Non_Obsolete_HttpAttribute_Reports_Nothing()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M()
                {
                    var x = OpenTelemetry.SemanticConventions.Attributes.HttpAttributes.AttributeHttpRequestMethod;
                }
            }
            """;

        await new CSharpAnalyzerTest<DeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task Exact_Replacement_CodeFix_Uses_Typed_Replacement_Constant()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M()
                {
                    var x = OpenTelemetry.SemanticConventions.Attributes.HttpAttributes.{|#0:AttributeHttpMethod|};
                }
            }
            """;

        const string fixedCode = SemconvFixture + """

            class C
            {
                void M()
                {
                    var x = OpenTelemetry.SemanticConventions.Attributes.HttpAttributes.AttributeHttpRequestMethod;
                }
            }
            """;

        var expected = new DiagnosticResult("QYL0010", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("HttpAttributes.AttributeHttpMethod", "Replaced by http.request.method.");

        await new CSharpCodeFixTest<DeprecatedSemconvAnalyzer, LiveSemconvMetadataCodeFixProvider, DefaultVerifier>
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Reference_To_Obsolete_NetAttribute_Reports_QYL0010()
    {
        const string testCode = SemconvFixture + """

            class C
            {
                void M()
                {
                    var x = OpenTelemetry.SemanticConventions.Attributes.NetAttributes.{|#0:AttributeNetSockHostAddr|};
                }
            }
            """;

        var expected = new DiagnosticResult("QYL0010", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("NetAttributes.AttributeNetSockHostAddr", "Replaced by network.local.address.");

        await new CSharpAnalyzerTest<DeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Obsolete_Constant_Outside_Semconv_Namespace_Reports_Nothing()
    {
        const string testCode = """
            #pragma warning disable CS0618
            namespace MyApp
            {
                public static class MyAttributes
                {
                    [System.Obsolete("Don't use this.")]
                    public const string Foo = "foo";
                }

                class C
                {
                    void M() { var x = MyAttributes.Foo; }
                }
            }
            """;

        await new CSharpAnalyzerTest<DeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task NonConst_String_With_Obsolete_In_Semconv_Reports_Nothing()
    {
        const string testCode = """
            #pragma warning disable CS0618
            namespace OpenTelemetry.SemanticConventions.Attributes
            {
                public static class HttpAttributes
                {
                    [System.Obsolete("not a const, should not fire")]
                    public static readonly string SomethingWeird = "x";
                }
            }

            class C
            {
                void M() { var x = OpenTelemetry.SemanticConventions.Attributes.HttpAttributes.SomethingWeird; }
            }
            """;

        await new CSharpAnalyzerTest<DeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    // Guards SemconvNamespace.IsInSemconvNamespace branch:
    //   s == Root
    // The producer puts the *Attributes type directly under the bare semconv
    // root with no further nesting. The base SemconvFixture above exercises
    // the StartsWith(Root + ".") branch via the conventional `*.Attributes`
    // sub-namespace, but `== Root` itself was not directly covered.
    [Fact]
    public async Task Reference_To_Obsolete_In_Exact_Root_Semconv_Namespace_Reports_QYL0010()
    {
        const string testCode = """
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

            class C
            {
                void M()
                {
                    var x = OpenTelemetry.SemanticConventions.HttpAttributes.{|#0:AttributeHttpMethod|};
                }
            }
            """;

        var expected = new DiagnosticResult("QYL0010", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("HttpAttributes.AttributeHttpMethod", "Replaced by http.request.method.");

        await new CSharpAnalyzerTest<DeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    // Guards SemconvNamespace.IsInSemconvNamespace branch:
    //   s.Contains("." + Root + ".", StringComparison.Ordinal)
    // This is the consumer-side nested layout documented at SemconvNamespace.cs
    // ("e.g. qyl"). If the .Contains branch regresses, every nested-namespace
    // consumer silently loses QYL0010/0011/0012/0014 coverage with no compile error.
    [Fact]
    public async Task Reference_To_Obsolete_In_Nested_Semconv_Namespace_Reports_QYL0010()
    {
        const string testCode = """
            #pragma warning disable CS0618
            namespace Qyl.OpenTelemetry.SemanticConventions.Http.Attributes
            {
                public static class HttpAttributes
                {
                    [System.Obsolete("Replaced by http.request.method.")]
                    public const string AttributeHttpMethod = "http.method";

                    public const string AttributeHttpRequestMethod = "http.request.method";
                }
            }

            class C
            {
                void M()
                {
                    var x = Qyl.OpenTelemetry.SemanticConventions.Http.Attributes.HttpAttributes.{|#0:AttributeHttpMethod|};
                }
            }
            """;

        var expected = new DiagnosticResult("QYL0010", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("HttpAttributes.AttributeHttpMethod", "Replaced by http.request.method.");

        await new CSharpAnalyzerTest<DeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    // Guards SemconvNamespace.IsInSemconvNamespace branch:
    //   s.EndsWith("." + Root, StringComparison.Ordinal)
    // This is the consumer-side layout where SemanticConventions is the tail
    // segment of a custom root, e.g. types live directly under
    // `Qyl.OpenTelemetry.SemanticConventions` with no further nesting.
    // Step 2 — non-Attributes tier coverage gated by an MSBuild flag.
    //
    // Weaver SourceGeneration emits five tiers per stability band:
    //   *Attributes, *Metrics, *Meters, *Events, *Activities.
    //
    // The default surface scans only the *Attributes tier. Consumers that opt
    // into wider coverage set `build_property.OtelSemConvNonAttributesTiers = true`
    // and pick up [Obsolete] const-string deprecations in the other four tiers
    // too. These two tests pin the contract from both sides so a regression in
    // either direction (default loosened, or flag stops being honoured) becomes
    // a red test rather than a silent behaviour drift.

    [Fact]
    public async Task Reference_To_Obsolete_In_MetricsClass_WithoutFlag_Reports_Nothing()
    {
        const string testCode = """
            #pragma warning disable CS0618
            namespace OpenTelemetry.SemanticConventions.Metrics
            {
                public static class HttpMetrics
                {
                    [System.Obsolete("Replaced by http.client.request.duration.")]
                    public const string MetricHttpClientDuration = "http.client.duration";
                }
            }

            class C
            {
                void M()
                {
                    var x = OpenTelemetry.SemanticConventions.Metrics.HttpMetrics.MetricHttpClientDuration;
                }
            }
            """;

        await new CSharpAnalyzerTest<DeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task Reference_To_Obsolete_In_MetricsClass_WithFlag_Reports_QYL0010()
    {
        const string testCode = """
            #pragma warning disable CS0618
            namespace OpenTelemetry.SemanticConventions.Metrics
            {
                public static class HttpMetrics
                {
                    [System.Obsolete("Replaced by http.client.request.duration.")]
                    public const string MetricHttpClientDuration = "http.client.duration";
                }
            }

            class C
            {
                void M()
                {
                    var x = OpenTelemetry.SemanticConventions.Metrics.HttpMetrics.{|#0:MetricHttpClientDuration|};
                }
            }
            """;

        var expected = new DiagnosticResult("QYL0010", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("HttpMetrics.MetricHttpClientDuration", "Replaced by http.client.request.duration.");

        await new CSharpAnalyzerTest<DeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            TestState =
            {
                AnalyzerConfigFiles =
                {
                    ("/.globalconfig", """
                        is_global = true
                        build_property.OtelSemConvNonAttributesTiers = true
                        """),
                },
            },
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task Reference_To_Obsolete_In_Trailing_Semconv_Namespace_Reports_QYL0010()
    {
        const string testCode = """
            #pragma warning disable CS0618
            namespace Qyl.OpenTelemetry.SemanticConventions
            {
                public static class HttpAttributes
                {
                    [System.Obsolete("Replaced by http.request.method.")]
                    public const string AttributeHttpMethod = "http.method";

                    public const string AttributeHttpRequestMethod = "http.request.method";
                }
            }

            class C
            {
                void M()
                {
                    var x = Qyl.OpenTelemetry.SemanticConventions.HttpAttributes.{|#0:AttributeHttpMethod|};
                }
            }
            """;

        var expected = new DiagnosticResult("QYL0010", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("HttpAttributes.AttributeHttpMethod", "Replaced by http.request.method.");

        await new CSharpAnalyzerTest<DeprecatedSemconvAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }
}
