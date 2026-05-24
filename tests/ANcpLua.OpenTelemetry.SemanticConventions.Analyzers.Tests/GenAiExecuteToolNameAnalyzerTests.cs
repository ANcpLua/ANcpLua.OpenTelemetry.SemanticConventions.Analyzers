// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using Qyl.OpenTelemetry.SemanticConventions.Analyzers;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests;

public class GenAiExecuteToolNameAnalyzerTests
{
    private const string FakeSpanShim = """
        public class FakeSpan
        {
            public FakeSpan SetTag(string key, object? value) => this;
        }
        """;

    [Fact]
    public async Task ExecuteTool_Without_ToolName_Reports_QYL0001()
    {
        const string testCode = FakeSpanShim + """

            class Service
            {
                void Invoke(FakeSpan activity)
                {
                    activity.SetTag({|#0:"gen_ai.operation.name"|}, "execute_tool");
                    activity.SetTag("gen_ai.system", "openai");
                }
            }
            """;

        var expected = new DiagnosticResult("QYL0001", DiagnosticSeverity.Warning)
            .WithLocation(0);

        await new CSharpAnalyzerTest<GenAiExecuteToolNameAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task ExecuteTool_With_ToolName_Reports_Nothing()
    {
        const string testCode = FakeSpanShim + """

            class Service
            {
                void Invoke(FakeSpan activity)
                {
                    activity.SetTag("gen_ai.operation.name", "execute_tool");
                    activity.SetTag("gen_ai.tool.name", "code_search");
                    activity.SetTag("gen_ai.system", "openai");
                }
            }
            """;

        await new CSharpAnalyzerTest<GenAiExecuteToolNameAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task NonExecuteTool_OperationName_Reports_Nothing()
    {
        const string testCode = FakeSpanShim + """

            class Service
            {
                void Invoke(FakeSpan activity)
                {
                    activity.SetTag("gen_ai.operation.name", "chat");
                    activity.SetTag("gen_ai.system", "openai");
                }
            }
            """;

        await new CSharpAnalyzerTest<GenAiExecuteToolNameAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task ExecuteTool_With_DynamicToolName_Reports_Nothing()
    {
        // Even when the value is non-constant, presence of the key satisfies the rule.
        const string testCode = FakeSpanShim + """

            class Service
            {
                void Invoke(FakeSpan activity, string toolName)
                {
                    activity.SetTag("gen_ai.operation.name", "execute_tool");
                    activity.SetTag("gen_ai.tool.name", toolName);
                }
            }
            """;

        await new CSharpAnalyzerTest<GenAiExecuteToolNameAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }
}
