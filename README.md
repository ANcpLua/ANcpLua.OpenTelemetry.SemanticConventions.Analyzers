# ANcpLua.OpenTelemetry.SemanticConventions.Analyzers

Roslyn diagnostic analyzers for [`OpenTelemetry.SemanticConventions`](https://www.nuget.org/packages/OpenTelemetry.SemanticConventions) consumers, targeting the v1.41.0 spec.

This is the **incubation home** for what may eventually be proposed as an official `OpenTelemetry.SemanticConventions.Analyzers` companion to `opentelemetry-dotnet-contrib`. While here, it ships under the `ANcpLua.*` namespace and Apache-2.0 license, ready for relicensing/donation when ripe.

## Diagnostics

The package-level generated catalog is in
[`docs/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.md`](docs/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.md).

| ID | Severity | Description |
|---|---|---|
| `OTSC0001` | Warning | `gen_ai.execute_tool` span requires `gen_ai.tool.name` for v1.41.0 span naming |
| `OTSC0002` | Info | `graphql.document` is opt-in because it may carry sensitive/high-cardinality user input |
| `OTSC0005` | Warning | RPC server span sets `client.address`/`client.port` (removed from RPC server in v1.41.0) |
| `OTSC0010` | Warning | Direct typed reference to a semantic-convention constant marked `[Obsolete]` |
| `OTSC0011` | Info | Hard-coded key matches a typed semantic-convention constant |
| `OTSC0012` | Warning | Hard-coded key matches a semantic-convention constant marked `[Obsolete]` |
| `OTSC0014` | Warning | Hard-coded value matches a semantic-convention value constant marked `[Obsolete]` |
| `OTSC0021` | Warning | Library directly references incubating semantic-convention members |
| `OTSC0030` | Error | Supplemental catalog exact replacement in production telemetry emission |
| `OTSC0031` | Warning | Supplemental catalog context-sensitive, manual-review, removed/no-replacement, or ambiguous payload migration |
| `OTSC0032` | Info | Supplemental catalog item appears in test, fixture, compatibility, generated, translator, or catalog context |

## Design

- **Live metadata first.** `OTSC0010`, `OTSC0012`, and `OTSC0014` resolve `[Obsolete]` metadata from the consumer's referenced `OpenTelemetry.SemanticConventions` assembly via Roslyn's symbol model, including baggage, tag collections, and visible dictionary/initializer payload keys and values. The referenced package remains the primary source of truth.
- **Curated migration inventory.** The package tracks 156 changelog/model mentions and separates rows covered by generated `[Obsolete]` metadata from rows that need supplemental analyzer diagnostics.
- **Supplemental catalog only where metadata is insufficient.** `OTSC0030`-`OTSC0032` cover changelog/model entries that are not reliably visible through generated `[Obsolete]` constants, including metric names, removed events, context-sensitive migrations, compatibility payloads, and attribute-value fallbacks when live value metadata is absent. Deprecated generated values remain primarily covered by `OTSC0014`.
- **Production payload recognition.** Supplemental diagnostics inspect visible dictionary, collection-initializer, and `KeyValuePair<string, object?>` payloads when they flow into telemetry emission APIs such as `ActivitySource.StartActivity` tags, metric instrument `Add`/`Record` calls, `Measurement<T>` tags, `ActivityEvent` tags, and `ResourceBuilder.AddAttributes`.
- **Context-sensitive severity.** Production telemetry emission can be an error only when the supplemental catalog has an exact one-to-one replacement. Tests, fixtures, migration maps, schema translators, generated code, and compatibility shims downgrade to info. Ambiguous dictionaries and no-direct-replacement items stay warning/manual-review.
- **Configurable legacy mode.** Set `build_property.OtelSemConvLegacyMode` to `production` (default), `compatibility`, or `off`. `off` disables only supplemental catalog diagnostics; live `[Obsolete]` metadata rules remain enabled.
- **Generated docs.** Regenerate the package catalog with `scripts/generate-docs.sh generate`; validate with `scripts/generate-docs.sh validate`.
- **netstandard2.0** only — required by Roslyn analyzer host. Microsoft.CodeAnalysis.* dependencies only.
- **Multi-version friendly.** A consumer on SemConv 1.39.0 gets live metadata diagnostics scoped to the 1.39.0 surface; upgrading expands those diagnostics automatically. The supplemental catalog is a conservative v1.41.0 migration aid.

## Status

Pre-release incubation. Not yet published to NuGet.

## License

Apache-2.0. Compatible with future donation to OpenTelemetry.
