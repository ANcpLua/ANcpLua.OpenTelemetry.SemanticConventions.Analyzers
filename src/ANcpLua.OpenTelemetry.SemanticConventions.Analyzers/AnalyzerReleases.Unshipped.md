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
