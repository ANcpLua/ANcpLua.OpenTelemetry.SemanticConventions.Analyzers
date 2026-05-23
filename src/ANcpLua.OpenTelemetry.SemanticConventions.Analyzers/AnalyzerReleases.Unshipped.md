; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID  | Category                          | Severity | Notes
---------|-----------------------------------|----------|-----------------------------------------------------------------------------
OTSC0001 | OpenTelemetry.SemanticConventions | Warning  | gen_ai.execute_tool span requires gen_ai.tool.name (v1.41.0).
OTSC0002 | OpenTelemetry.SemanticConventions | Info     | graphql.document is opt-in; verify explicit enablement and sanitization.
OTSC0005 | OpenTelemetry.SemanticConventions | Warning  | RPC server span must not set client.address or client.port (v1.41.0).
OTSC0010 | OpenTelemetry.SemanticConventions | Warning  | Use of deprecated semantic-convention constant.
OTSC0011 | OpenTelemetry.SemanticConventions | Info     | Prefer typed semantic-convention constant over equivalent string literal.
OTSC0012 | OpenTelemetry.SemanticConventions | Warning  | String literal in a tag-setter call matches a deprecated semantic-convention attribute name.
OTSC0014 | OpenTelemetry.SemanticConventions | Warning  | Constant value of a known semantic-convention attribute matches a deprecated value member.
OTSC0021 | OpenTelemetry.SemanticConventions | Warning  | Incubating semantic-convention member referenced from a library project.
OTSC0030 | OpenTelemetry.SemanticConventions | Error    | Supplemental migration catalog item has an exact one-to-one replacement in production telemetry emission.
OTSC0031 | OpenTelemetry.SemanticConventions | Warning  | Supplemental migration catalog item requires context-sensitive/manual review or appears in an ambiguous payload.
OTSC0032 | OpenTelemetry.SemanticConventions | Info     | Supplemental migration catalog item appears in test, fixture, generated, translator, compatibility, or catalog context.
