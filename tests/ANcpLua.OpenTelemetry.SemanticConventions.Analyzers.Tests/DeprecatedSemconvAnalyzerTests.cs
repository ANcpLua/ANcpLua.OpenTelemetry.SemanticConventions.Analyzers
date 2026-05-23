// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace OpenTelemetry.SemanticConventions.Analyzers.Tests;

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
    public async Task Reference_To_Obsolete_HttpAttribute_Reports_OTSC0010()
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

        var expected = new DiagnosticResult("OTSC0010", DiagnosticSeverity.Warning)
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
    public async Task Reference_To_Obsolete_NetAttribute_Reports_OTSC0010()
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

        var expected = new DiagnosticResult("OTSC0010", DiagnosticSeverity.Warning)
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
}
