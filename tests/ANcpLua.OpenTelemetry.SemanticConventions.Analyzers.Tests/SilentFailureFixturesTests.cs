// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace OpenTelemetry.SemanticConventions.Analyzers.Tests;

/// <summary>
/// Pins the four boundary behaviours where a regression would manifest as a
/// missing diagnostic (or a missing code-fix) rather than a noisy failure:
///
/// (a) Weaver emits <c>*Metrics</c> classes too, not just <c>*Attributes</c>.
///     Without the <c>OtelSemConvNonAttributesTiers</c> flag those classes are
///     silently invisible to the analyzer. Pinned by step 2's
///     <c>Reference_To_Obsolete_In_MetricsClass_WithoutFlag_Reports_Nothing</c>.
///
/// (b) Upstream YAML can mark an attribute <c>code_generation: exclude: true</c>,
///     which tells Weaver to suppress the constant entirely. If the field never
///     exists, the analyzer cannot fire — pinned by an empty-class fixture.
///
/// (c) Custom Obsolete note formats (<c>"Use 'X' instead."</c>) are unfamiliar to
///     <see cref="SemconvCodeFixHelpers.TryExtractExactReplacement"/>; the
///     diagnostic still fires but the code-fix is silently withheld because the
///     replacement string would be guesswork.
///
/// (d) When the deprecation message names a replacement constant that does NOT
///     exist as a const-string field in the consumer's compilation,
///     <see cref="LiveSemconvMetadataCodeFixProvider.FindReplacementField"/>
///     returns null and the fix registration is silently skipped. Diagnostic
///     fires; code-fix no-ops.
/// </summary>
public class SilentFailureFixturesTests
{
    // Step 6(b) — code_generation: exclude: true. The Weaver template skips the
    // attribute; the generated C# contains no Obsolete const for the excluded
    // entry. From the analyzer's perspective, the class has nothing to flag.
    [Fact]
    public async Task Excluded_From_CodeGen_Produces_No_Field_So_Nothing_Fires()
    {
        const string testCode = """
            #pragma warning disable CS0618
            namespace OpenTelemetry.SemanticConventions.Attributes
            {
                public static class HttpAttributes
                {
                    // http.method.suppressed would live here, but code_generation: exclude: true
                    // suppressed the constant. Only the live attributes remain visible.
                    public const string AttributeHttpRequestMethod = "http.request.method";
                }
            }

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

    // Step 6(c) — Obsolete note that the parser refuses to commit to. Analyzer
    // still fires (the deprecation matters even if the message shape is bespoke);
    // code-fix is withheld because TryExtractExactReplacement rejects the shape
    // (pinned at the unit-test layer in SemconvCodeFixHelpersTests).
    [Fact]
    public async Task Custom_Note_Format_Reports_OTSC0010_Without_CodeFix()
    {
        const string testCode = """
            #pragma warning disable CS0618
            namespace OpenTelemetry.SemanticConventions.Attributes
            {
                public static class HttpAttributes
                {
                    [System.Obsolete("Use 'http.request.method' instead.")]
                    public const string AttributeHttpMethod = "http.method";

                    public const string AttributeHttpRequestMethod = "http.request.method";
                }
            }

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
            .WithArguments("HttpAttributes.AttributeHttpMethod", "Use 'http.request.method' instead.");

        // CSharpCodeFixTest with FixedCode == TestCode asserts both halves:
        //   1. the diagnostic fires,
        //   2. no code-fix is registered (otherwise FixedCode would differ).
        // If a future regression silently widens the parser to accept this shape,
        // a fix WILL be registered and the test will fail because the applied fix
        // changes the source.
        await new CSharpCodeFixTest<DeprecatedSemconvAnalyzer, LiveSemconvMetadataCodeFixProvider, DefaultVerifier>
        {
            TestCode = testCode,
            FixedCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }

    // Step 6(d) — replacement string parses cleanly but no matching field exists
    // in the compilation. LiveSemconvMetadataCodeFixProvider:72-76 short-circuits
    // (`FindReplacementField` returns null) and skips registration. Diagnostic
    // fires, no fix offered.
    [Fact]
    public async Task Replacement_Field_Not_Found_CodeFix_Silently_NoOps()
    {
        const string testCode = """
            #pragma warning disable CS0618
            namespace OpenTelemetry.SemanticConventions.Attributes
            {
                public static class HttpAttributes
                {
                    [System.Obsolete("Replaced by http.does.not.exist.")]
                    public const string AttributeHttpMethod = "http.method";

                    // Intentionally no AttributeHttpDoesNotExist field anywhere in the
                    // compilation — the code-fix walks the global namespace via
                    // FindReplacementField, finds nothing, and silently no-ops.
                }
            }

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
            .WithArguments("HttpAttributes.AttributeHttpMethod", "Replaced by http.does.not.exist.");

        await new CSharpCodeFixTest<DeprecatedSemconvAnalyzer, LiveSemconvMetadataCodeFixProvider, DefaultVerifier>
        {
            TestCode = testCode,
            FixedCode = testCode,
            ExpectedDiagnostics = { expected },
        }.RunAsync();
    }
}
