// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;

namespace OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>Shared namespace-shape detection for SemConv attribute classes.</summary>
internal static class SemconvNamespace
{
    private const string Root = "OpenTelemetry.SemanticConventions";

    /// <summary>
    /// Returns true if the type is named <c>*Attributes</c> AND lives in or under any
    /// namespace segment named <c>OpenTelemetry.SemanticConventions</c>. Handles both
    /// upstream's flat layout and consumer-side nested layouts (e.g. qyl).
    /// </summary>
    public static bool IsAttributesType(INamedTypeSymbol type)
    {
        if (!type.Name.EndsWith("Attributes", System.StringComparison.Ordinal))
        {
            return false;
        }

        return IsInSemconvNamespace(type.ContainingNamespace);
    }

    public static bool IsInSemconvNamespace(INamespaceSymbol? ns)
    {
        var s = ns?.ToDisplayString();
        if (s is null)
        {
            return false;
        }

        return s == Root
               || s.StartsWith(Root + ".", System.StringComparison.Ordinal)
               || s.Contains("." + Root + ".")
               || s.EndsWith("." + Root, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns true if the namespace's display string contains an <c>.Incubating</c>
    /// segment AND descends from a SemanticConventions root. Mirrors the AL0136
    /// detection: any namespace ending in or containing <c>.Incubating</c> under
    /// <c>OpenTelemetry.SemanticConventions</c> or <c>OpenTelemetry.SemConv</c>.
    /// </summary>
    public static bool IsIncubatingNamespace(INamespaceSymbol? ns)
    {
        var s = ns?.ToDisplayString();
        if (s is null || !s.Contains(".Incubating"))
        {
            return false;
        }

        return s.Contains("SemanticConventions") || s.Contains("SemConv");
    }
}
