// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.SemanticConventions.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SupplementalSemconvMigrationCodeFixProvider))]
public sealed class SupplementalSemconvMigrationCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
    [
        "OTSC0030",
        "OTSC0031",
        "OTSC0032",
    ];

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!IsExactReplacement(diagnostic)
                || !diagnostic.Properties.TryGetValue(
                    SupplementalSemconvMigrationAnalyzer.ReplacementNameProperty,
                    out var replacement)
                || string.IsNullOrWhiteSpace(replacement))
            {
                continue;
            }

            var literal = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                .FirstAncestorOrSelf<LiteralExpressionSyntax>();
            if (literal is null || !literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Replace with \"{replacement}\"",
                    ct => ReplaceLiteralAsync(document, literal, replacement!, ct),
                    nameof(SupplementalSemconvMigrationCodeFixProvider)),
                diagnostic);
        }
    }

    private static bool IsExactReplacement(Diagnostic diagnostic)
    {
        if (!diagnostic.Properties.TryGetValue(
                SupplementalSemconvMigrationAnalyzer.MigrationKindProperty,
                out var migrationKind))
        {
            return false;
        }

        return migrationKind == SemconvMigrationKind.ExactRename.ToString()
            || migrationKind == SemconvMigrationKind.ExactValueRename.ToString();
    }

    private static async Task<Document> ReplaceLiteralAsync(
        Document document,
        LiteralExpressionSyntax literal,
        string replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var replacementLiteral = CreateReplacementLiteral(literal, replacement);
        return document.WithSyntaxRoot(root.ReplaceNode(literal, replacementLiteral));
    }

    private static LiteralExpressionSyntax CreateReplacementLiteral(
        LiteralExpressionSyntax original,
        string replacement)
    {
        var token = original.Token;
        var text = token.Text;
        var literalText = text.StartsWith("@\"", StringComparison.Ordinal)
            ? "@\"" + replacement.Replace("\"", "\"\"") + "\""
            : "\"" + EscapeStringLiteral(replacement) + "\"";

        return SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(token.LeadingTrivia, literalText, replacement, token.TrailingTrivia))
            .WithTriviaFrom(original);
    }

    private static string EscapeStringLiteral(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(ch);
                    break;
            }
        }

        return builder.ToString();
    }
}
