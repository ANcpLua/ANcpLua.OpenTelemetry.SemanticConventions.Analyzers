// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CodeAnalysis;

namespace OpenTelemetry.SemanticConventions.Analyzers;

internal static class DiagnosticDescriptors
{
    private const string Category = "OpenTelemetry.SemanticConventions";
    private const string HelpLinkBase = "https://github.com/ANcpLua/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers/blob/main/docs/";

    public static readonly DiagnosticDescriptor DeprecatedSemconvConstant = new(
        id: "OTSC0010",
        title: "Deprecated semantic-convention constant",
        messageFormat: "Semantic-convention constant '{0}' is deprecated: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "References to constants in OpenTelemetry.SemanticConventions.Attributes.* that carry [Obsolete]. Migrate to the replacement attribute named in the deprecation message.",
        helpLinkUri: HelpLinkBase + "OTSC0010.md");

    public static readonly DiagnosticDescriptor RpcServerHasClientAddressAttribute = new(
        id: "OTSC0005",
        title: "RPC server span must not include client.address / client.port",
        messageFormat: "RPC server span sets '{0}'; v1.41.0 removed client.* attributes from RPC server span definitions",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "From v1.41.0, RPC server spans extend the rpc base group directly and no longer include client.address or client.port. Use server.address / server.port instead.",
        helpLinkUri: HelpLinkBase + "OTSC0005.md");
}
