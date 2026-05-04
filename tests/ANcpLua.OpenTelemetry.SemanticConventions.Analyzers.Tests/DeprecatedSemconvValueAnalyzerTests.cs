// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace OpenTelemetry.SemanticConventions.Analyzers.Tests;

public class DeprecatedSemconvValueAnalyzerTests
{
    private const string SemconvFixture = """
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

        var expected = new DiagnosticResult("OTSC0014", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
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
}
