// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// OTSC0010: Flags references to <c>[Obsolete]</c> <c>const string</c> fields under
/// <c>OpenTelemetry.SemanticConventions.Attributes.*</c>.
/// </summary>
/// <remarks>
/// The deprecation map is the consumer's own referenced
/// <c>OpenTelemetry.SemanticConventions</c> assembly — there is no hard-coded catalog
/// in this analyzer. Whatever Weaver emits as <c>[Obsolete("Replaced by ...")]</c>
/// is what we surface.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeprecatedSemconvAnalyzer : DiagnosticAnalyzer
{
    private const string SemconvNamespaceRoot = "OpenTelemetry.SemanticConventions";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.DeprecatedSemconvConstant);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeFieldReference, OperationKind.FieldReference);
    }

    private static void AnalyzeFieldReference(OperationAnalysisContext context)
    {
        var operation = (IFieldReferenceOperation)context.Operation;
        var field = operation.Field;

        if (field.IsConst is false || field.Type.SpecialType != SpecialType.System_String)
        {
            return;
        }

        var containingType = field.ContainingType;
        if (containingType is null || !IsSemconvAttributesType(containingType))
        {
            return;
        }

        var obsolete = field.GetAttributes().FirstOrDefault(IsObsoleteAttribute);
        if (obsolete is null)
        {
            return;
        }

        var message = ExtractObsoleteMessage(obsolete);
        var displayName = $"{containingType.Name}.{field.Name}";

        // Squiggle just the field name, not the full qualified expression.
        var location = operation.Syntax switch
        {
            MemberAccessExpressionSyntax member => member.Name.GetLocation(),
            _ => operation.Syntax.GetLocation(),
        };

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.DeprecatedSemconvConstant,
            location,
            displayName,
            message));
    }

    private static bool IsSemconvAttributesType(INamedTypeSymbol type)
    {
        // Match types named "*Attributes" living anywhere under OpenTelemetry.SemanticConventions.
        // This handles both the upstream flat layout
        //   (OpenTelemetry.SemanticConventions.HttpAttributes)
        // and the qyl-style nested layout
        //   (Qyl.OpenTelemetry.SemanticConventions.Attributes.Http.HttpAttributes —
        //    note the SemanticConventions root inside Qyl.* still works because we
        //    look for the substring, not a strict prefix).
        if (!type.Name.EndsWith("Attributes", System.StringComparison.Ordinal))
        {
            return false;
        }

        var ns = type.ContainingNamespace?.ToDisplayString();
        if (ns is null)
        {
            return false;
        }

        return ns == SemconvNamespaceRoot
               || ns.StartsWith(SemconvNamespaceRoot + ".", System.StringComparison.Ordinal)
               || ns.Contains("." + SemconvNamespaceRoot + ".")
               || ns.EndsWith("." + SemconvNamespaceRoot, System.StringComparison.Ordinal);
    }

    private static bool IsObsoleteAttribute(AttributeData attribute)
    {
        var attrClass = attribute.AttributeClass;
        return attrClass is { Name: "ObsoleteAttribute", ContainingNamespace.Name: "System" };
    }

    private static string ExtractObsoleteMessage(AttributeData obsolete)
    {
        if (obsolete.ConstructorArguments.Length > 0
            && obsolete.ConstructorArguments[0].Value is string message
            && !string.IsNullOrEmpty(message))
        {
            return message;
        }

        return "no replacement message provided";
    }
}
