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

    public static readonly DiagnosticDescriptor GenAiExecuteToolMissingToolName = new(
        id: "OTSC0001",
        title: "gen_ai.execute_tool span requires gen_ai.tool.name",
        messageFormat: "Method sets gen_ai.operation.name=\"execute_tool\" but does not set gen_ai.tool.name; the tool name is required for span naming as of v1.41.0",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "v1.41.0 made gen_ai.tool.name a required attribute on the gen_ai.execute_tool internal span; the canonical span name is 'execute_tool {gen_ai.tool.name}'.",
        helpLinkUri: HelpLinkBase + "OTSC0001.md");

    public static readonly DiagnosticDescriptor GraphqlDocumentIsOptIn = new(
        id: "OTSC0002",
        title: "graphql.document is opt-in",
        messageFormat: "Setting graphql.document captures user-supplied data; v1.41.0 demoted it from recommended to opt_in — verify explicit enablement and sanitization",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "graphql.document carries user-inputted, potentially sensitive, high-cardinality content. v1.41.0 moved its requirement level from recommended to opt_in. Capture only behind an explicit opt-in flag with sanitization.",
        helpLinkUri: HelpLinkBase + "OTSC0002.md");

    public static readonly DiagnosticDescriptor PreferSemconvConstant = new(
        id: "OTSC0011",
        title: "Prefer typed semantic-convention constant over string literal",
        messageFormat: "String literal \"{0}\" matches the semantic-convention constant '{1}' — use the typed constant",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "When a string literal passed to SetTag/AddTag/SetAttribute matches a known semantic-convention attribute name from OpenTelemetry.SemanticConventions.Attributes.*, prefer the typed constant for refactor-safety and discoverability.",
        helpLinkUri: HelpLinkBase + "OTSC0011.md");

    public static readonly DiagnosticDescriptor LiteralMatchesDeprecatedSemconv = new(
        id: "OTSC0012",
        title: "String literal matches a deprecated semantic-convention name",
        messageFormat: "Literal \"{0}\" matches a deprecated semantic-convention attribute: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When a string literal passed to SetTag/AddTag/SetAttribute matches a semantic-convention attribute that is marked [Obsolete] in the consumer's referenced OpenTelemetry.SemanticConventions package, the call site needs migration regardless of whether a typed constant is being used.",
        helpLinkUri: HelpLinkBase + "OTSC0012.md");

    public static readonly DiagnosticDescriptor DeprecatedSemconvValue = new(
        id: "OTSC0014",
        title: "Deprecated semantic-convention value",
        messageFormat: "Value \"{0}\" of attribute '{1}' is deprecated: {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A constant string passed as the value of a known semantic-convention attribute matches a value member that is marked [Obsolete] in the consumer's referenced *Values enum class.",
        helpLinkUri: HelpLinkBase + "OTSC0014.md");

    public static readonly DiagnosticDescriptor IncubatingSemconvInLibrary = new(
        id: "OTSC0021",
        title: "Incubating semantic-convention member used in a library",
        messageFormat: "Member '{0}' from an Incubating namespace forces every consumer onto its exact package version; copy the constant locally in libraries",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Members under any *.SemanticConventions.Incubating namespace may rename or change values across minor package releases. Library projects (non-exe, non-test) baking direct references push that volatility onto every downstream consumer.",
        helpLinkUri: HelpLinkBase + "OTSC0021.md");
}
