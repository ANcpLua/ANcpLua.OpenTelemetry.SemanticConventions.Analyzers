// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

using Qyl.OpenTelemetry.SemanticConventions.Analyzers;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests;

/// <summary>
/// Pins the message-shape contract that <see cref="SemconvCodeFixHelpers.TryExtractExactReplacement"/>
/// enforces. The analyzer always reports the diagnostic regardless of the message shape; only the
/// <c>ReplacementValue</c> diagnostic property — which gates code-fix registration in
/// <see cref="LiveSemconvMetadataCodeFixProvider"/> — depends on extraction succeeding.
/// </summary>
/// <remarks>
/// Splitting accept and reject into two theories (rather than one with a bool flag) keeps each
/// signal's wiring narrow: accept theories assert both the return value and the exact extracted
/// string; reject theories assert only the negative.
/// </remarks>
public class SemconvCodeFixHelpersTests
{
    // Accept: shapes Weaver and the upstream model documentation actually emit.
    [Theory]
    [InlineData("Replaced by http.request.method.", "http.request.method")]
    [InlineData("Replaced by `http.request.method`.", "http.request.method")]
    [InlineData("Use <c>http.request.method</c> instead.", "http.request.method")]
    [InlineData("Replaced by http.request.method", "http.request.method")] // trailing period optional
    [InlineData("REPLACED BY http.request.method.", "http.request.method")] // prefix is case-insensitive
    [InlineData("Replaced by \"http.request.method\".", "http.request.method")] // double-quoted candidate
    [InlineData("Replaced by 'http.request.method'.", "http.request.method")] // single-quoted candidate
    [InlineData("Free-form preamble. Use <c>http.request.method</c> instead.", "http.request.method")] // <c> wins anywhere
    public void TryExtractExactReplacement_Accepts_KnownShapes(string message, string expected)
    {
        var ok = SemconvCodeFixHelpers.TryExtractExactReplacement(message, out var replacement);

        Assert.True(ok, $"expected extraction to succeed for '{message}'");
        Assert.Equal(expected, replacement);
    }

    // Reject: shapes that look migration-shaped but the extractor refuses to commit to. Diagnostic
    // still fires upstream; the code-fix simply isn't offered for these. Each entry guards a
    // specific class of would-be silent failure.
    [Theory]
    [InlineData("Use 'http.request.method' instead.")] // no "Replaced by" prefix and no <c> tag
    [InlineData("Migrated to http.request.method.")] // unrecognised verb
    [InlineData("Replaced by http.request.method and url.full.")] // ambiguous — two candidates
    [InlineData("Replaced by http.request.method, url.full.")] // ambiguous — comma-separated
    [InlineData("Deprecated.")] // no replacement at all
    [InlineData("Replaced by .")] // empty candidate after the prefix
    [InlineData("Replaced by ``.")] // empty backtick candidate
    [InlineData("Use <c></c> instead.")] // empty <c> tag
    [InlineData("")] // empty message
    [InlineData("   ")] // whitespace
    public void TryExtractExactReplacement_Rejects_AmbiguousOrUnknownShapes(string message)
    {
        var ok = SemconvCodeFixHelpers.TryExtractExactReplacement(message, out _);

        // Only the return value matters — the [NotNullWhen(true)] contract means callers must not
        // read `replacement` when this returns false, so its post-rejection value is intentionally
        // unspecified (LiveSemconvMetadataCodeFixProvider already gates on the diagnostic property,
        // not the out parameter).
        Assert.False(ok, $"expected extraction to fail for '{message}'");
    }

    // When both a <c>...</c> tag and a "Replaced by" prefix appear, the <c>-tag path wins — pins
    // the precedence so a future ordering change doesn't silently swap which fragment becomes the
    // code-fix replacement.
    [Fact]
    public void TryExtractExactReplacement_CodeTag_Takes_Precedence_Over_ReplacedByPrefix()
    {
        var ok = SemconvCodeFixHelpers.TryExtractExactReplacement(
            "Replaced by old.value. Use <c>new.value</c> instead.",
            out var replacement);

        Assert.True(ok);
        Assert.Equal("new.value", replacement);
    }
}
