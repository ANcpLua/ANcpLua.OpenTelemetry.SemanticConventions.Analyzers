# ANcpLua.OpenTelemetry.SemanticConventions.Analyzers

Roslyn diagnostic analyzers for [`OpenTelemetry.SemanticConventions`](https://www.nuget.org/packages/OpenTelemetry.SemanticConventions) consumers, targeting the v1.41.0 spec.

This is the **incubation home** for what may eventually be proposed as an official `OpenTelemetry.SemanticConventions.Analyzers` companion to `opentelemetry-dotnet-contrib`. While here, it ships under the `ANcpLua.*` namespace and Apache-2.0 license, ready for relicensing/donation when ripe.

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| `OTSC0010` | Warning | Use of `[Obsolete]` semantic-convention constant from `OpenTelemetry.SemanticConventions.Attributes.*` |
| `OTSC0005` | Warning | RPC server span sets `client.address`/`client.port` (removed from RPC server in v1.41.0) |

More rules planned: see `AnalyzerReleases.Unshipped.md`.

## Design

- **No catalog generation.** Diagnostics resolve attribute metadata from the consumer's referenced `OpenTelemetry.SemanticConventions` assembly via Roslyn's symbol model. The package's existing `[Obsolete]` markers are the source of truth — no parallel deprecation table to maintain.
- **netstandard2.0** only — required by Roslyn analyzer host. Microsoft.CodeAnalysis.* dependencies only.
- **Multi-version friendly.** A consumer on SemConv 1.39.0 gets diagnostics scoped to the 1.39.0 surface; upgrading to 1.41.0 expands the catalog automatically.

## Status

Pre-release incubation. Not yet published to NuGet.

## License

Apache-2.0. Compatible with future donation to OpenTelemetry.
