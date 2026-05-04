; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID  | Category                         | Severity | Notes
---------|----------------------------------|----------|-----------------------------------------------------------------------------
OTSC0005 | OpenTelemetry.SemanticConventions | Warning  | RPC server span must not set `client.address` or `client.port` (v1.41.0).
OTSC0010 | OpenTelemetry.SemanticConventions | Warning  | Use of deprecated semantic-convention constant.
