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
}
