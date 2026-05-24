// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using Qyl.OpenTelemetry.SemanticConventions.Analyzers;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests;

public class GraphqlDocumentOptInAnalyzerTests
{
    private const string FakeSpanShim = """
        public class FakeSpan
        {
            public FakeSpan SetTag(string key, object? value) => this;
        }
        """;

    [Fact]
    public async Task GraphqlDocument_SetTag_Reports_QYL0002()
    {
        const string testCode = FakeSpanShim + """

            class Resolver
            {
                void Resolve(FakeSpan activity, string query)
                {
                    activity.SetTag({|#0:"graphql.document"|}, query);
                }
            }
            """;

        var expected = new DiagnosticResult("QYL0002", DiagnosticSeverity.Info)
            .WithLocation(0);

        await new CSharpAnalyzerTest<GraphqlDocumentOptInAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task GraphqlOperationName_SetTag_Reports_Nothing()
    {
        const string testCode = FakeSpanShim + """

            class Resolver
            {
                void Resolve(FakeSpan activity, string opName)
                {
                    activity.SetTag("graphql.operation.name", opName);
                    activity.SetTag("graphql.operation.type", "query");
                }
            }
            """;

        await new CSharpAnalyzerTest<GraphqlDocumentOptInAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task GraphqlDocument_AddTag_Also_Reports()
    {
        const string testCode = FakeSpanShim + """

            public static class FakeSpanExtensions
            {
                public static FakeSpan AddTag(this FakeSpan span, string key, object? value) => span;
            }

            class Resolver
            {
                void Resolve(FakeSpan activity, string query)
                {
                    activity.AddTag({|#0:"graphql.document"|}, query);
                }
            }
            """;

        var expected = new DiagnosticResult("QYL0002", DiagnosticSeverity.Info)
            .WithLocation(0);

        await new CSharpAnalyzerTest<GraphqlDocumentOptInAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }
}
