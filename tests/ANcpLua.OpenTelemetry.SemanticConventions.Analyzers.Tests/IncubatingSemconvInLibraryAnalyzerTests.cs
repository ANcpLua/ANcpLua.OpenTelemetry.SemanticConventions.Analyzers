// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace OpenTelemetry.SemanticConventions.Analyzers.Tests;

public class IncubatingSemconvInLibraryAnalyzerTests
{
    private const string IncubatingFixture = """
        namespace OpenTelemetry.SemanticConventions.Incubating
        {
            public static class GenAiAttributes
            {
                public const string AttributeGenAiPrompt = "gen_ai.prompt";
            }
        }

        public class FakeSpan
        {
            public FakeSpan SetTag(string key, object? value) => this;
        }
        """;

    [Fact]
    public async Task LibraryProject_References_Incubating_Reports_OTSC0021()
    {
        const string testCode = IncubatingFixture + """

            class C
            {
                public void M(FakeSpan s)
                {
                    s.SetTag({|#0:OpenTelemetry.SemanticConventions.Incubating.GenAiAttributes.AttributeGenAiPrompt|}, "hi");
                }
            }
            """;

        var expected = new DiagnosticResult("OTSC0021", DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("OpenTelemetry.SemanticConventions.Incubating.GenAiAttributes.AttributeGenAiPrompt");

        await new CSharpAnalyzerTest<IncubatingSemconvInLibraryAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    [Fact]
    public async Task LocalConstCopy_Suppresses_OTSC0021()
    {
        const string testCode = IncubatingFixture + """

            class C
            {
                // The local-copy mitigation: copying the value into a const field
                // suppresses the diagnostic, even though it dereferences the
                // incubating member to compute the value at compile time.
                public const string LocalGenAiPrompt = OpenTelemetry.SemanticConventions.Incubating.GenAiAttributes.AttributeGenAiPrompt;
            }
            """;

        await new CSharpAnalyzerTest<IncubatingSemconvInLibraryAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task ExeProject_References_Incubating_Reports_Nothing()
    {
        const string testCode = IncubatingFixture + """

            class C
            {
                public static void Main()
                {
                    var x = OpenTelemetry.SemanticConventions.Incubating.GenAiAttributes.AttributeGenAiPrompt;
                }
            }
            """;

        await new CSharpAnalyzerTest<IncubatingSemconvInLibraryAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            TestState = { OutputKind = OutputKind.ConsoleApplication },
        }.RunAsync();
    }
}
