# ANcpLua.OpenTelemetry.SemanticConventions.Analyzers

Roslyn diagnostic analyzers for [`OpenTelemetry.SemanticConventions`](https://www.nuget.org/packages/OpenTelemetry.SemanticConventions) consumers, targeting the v1.41.0 spec.

This package keeps the referenced `OpenTelemetry.SemanticConventions` assembly as the primary source of truth. Live `[Obsolete]` metadata drives the main deprecation rules; a curated supplemental catalog covers migration cases that are not reliably visible through generated constants.

## Status and Installation

Pre-release incubation. The package ID is `ANcpLua.OpenTelemetry.SemanticConventions.Analyzers`; it is not yet published to NuGet.

When published, consume it as a development-only analyzer dependency:

```xml
<PackageReference Include="ANcpLua.OpenTelemetry.SemanticConventions.Analyzers"
                  Version="..."
                  PrivateAssets="all"
                  IncludeAssets="runtime; build; native; contentfiles; analyzers; buildtransitive" />
```

For local evaluation from this checkout, reference the analyzer project directly from a consumer project:

```xml
<ProjectReference Include="path/to/src/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

## Diagnostics

The package-level generated catalog is in [`docs/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.md`](docs/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.md). It includes titles, descriptions, severities, code-fix availability, examples, configuration, and the supplemental migration-catalog audit.

| ID | Severity | Code fix | Description |
|---|---|---|---|
| [`OTSC0001`](docs/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.md#otsc0001) | Warning | No | `gen_ai.execute_tool` span requires `gen_ai.tool.name` for v1.41.0 span naming |
| [`OTSC0002`](docs/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.md#otsc0002) | Info | No | `graphql.document` is opt-in because it may carry sensitive/high-cardinality user input |
| [`OTSC0005`](docs/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.md#otsc0005) | Warning | No | RPC server span sets `client.address`/`client.port`, which were removed from RPC server spans in v1.41.0 |
| [`OTSC0010`](docs/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.md#otsc0010) | Warning | Yes | Direct typed reference to a semantic-convention constant marked `[Obsolete]` |
| [`OTSC0011`](docs/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.md#otsc0011) | Info | No | Hard-coded key matches a typed semantic-convention constant |
| [`OTSC0012`](docs/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.md#otsc0012) | Warning | Yes | Hard-coded key matches a semantic-convention constant marked `[Obsolete]` |
| [`OTSC0014`](docs/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.md#otsc0014) | Warning | Yes | Hard-coded value matches a semantic-convention value constant marked `[Obsolete]` |
| [`OTSC0021`](docs/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.md#otsc0021) | Warning | No | Library directly references incubating semantic-convention members |
| [`OTSC0030`](docs/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.md#otsc0030) | Error | Exact replacements only | Supplemental catalog exact replacement in production telemetry emission |
| [`OTSC0031`](docs/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.md#otsc0031) | Warning | Exact replacements only | Supplemental catalog context-sensitive, manual-review, removed/no-replacement, or ambiguous payload migration |
| [`OTSC0032`](docs/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.md#otsc0032) | Info | Exact replacements only | Supplemental catalog item appears in test, fixture, compatibility, generated, translator, or catalog context |

## Examples

```csharp
activity.SetTag(HttpAttributes.AttributeHttpMethod, "GET"); // OTSC0010 from live [Obsolete] metadata.
activity.SetTag("http.method", "GET");                     // OTSC0012 when the referenced package marks the key [Obsolete].
meter.CreateHistogram<long>("system.memory.shared");        // OTSC0030; use "system.memory.linux.shared".
activity.SetTag("error.message", message);                  // OTSC0031; replacement is context-sensitive.
```

## Design

- **Live metadata first.** `OTSC0010`, `OTSC0012`, and `OTSC0014` resolve `[Obsolete]` metadata from the consumer's referenced `OpenTelemetry.SemanticConventions` assembly via Roslyn's symbol model, including baggage, tag collection `Add`/indexer writes, collection expressions, and visible dictionary/initializer payload keys and values. The referenced package remains the primary source of truth.
- **Curated migration inventory.** Run `scripts/generate-docs.sh audit` or see the generated documentation for the authoritative count of changelog/model mentions, live `[Obsolete]` metadata rows, supplemental diagnostic rows, and attribute-value fallback rows tracked by the package.
- **Supplemental catalog only where metadata is insufficient.** `OTSC0030`-`OTSC0032` cover changelog/model entries that are not reliably visible through generated `[Obsolete]` constants, including metric names, removed events, context-sensitive migrations, compatibility payloads, and attribute-value fallbacks when live value metadata is absent. Deprecated generated values remain primarily covered by `OTSC0014`.
- **Exact code fixes only.** Live `[Obsolete]` metadata fixes are offered only when the deprecation text exposes one exact replacement; typed constants are replaced with the replacement typed constant when resolvable. Supplemental fixes remain limited to exact one-to-one catalog replacements.
- **Production payload recognition.** Supplemental diagnostics inspect visible inline payloads, collection expressions, local dictionary/collection initializers, and mutable local dictionary writes when they flow into telemetry emission APIs such as `ActivitySource.StartActivity` tags, metric instrument `Add`/`Record` calls, `Measurement<T>` tags, `ActivityEvent`/`ActivityLink` tags, `ResourceBuilder.AddAttributes`, and `ILogger.Log`/`ILogger.BeginScope` state payloads.
- **Context-sensitive severity.** Production telemetry emission can be an error only when the supplemental catalog has an exact one-to-one replacement. Tests, fixtures, migration maps, schema translators, generated code, and compatibility shims downgrade to info. Ambiguous dictionaries and no-direct-replacement items stay warning/manual-review.
- **Per-type suppressor for compatibility shapes.** `SemconvLegacyContextSuppressor` recognises class/struct/record/method names matching `Legacy*` / `*CompatShim` / `*MigrationFixture` / `*SchemaTranslator` / `*DeprecatedSemconv*` and suppresses every `OTSC*` diagnostic inside them — structured alternative to scattering `#pragma warning disable` across compatibility code.
- **Multi-hop rename resolution.** `SemconvMigrationCatalog.ResolveTerminalReplacement` chases `ExactRename` chains (e.g. `http.host` → `net.host.name` → `server.address`) so code-fixes land consumers on the terminal symbol, not on a still-deprecated mid-state. Cycles and chains over 8 hops bail at the last safe step.
- **Structured changelog provenance.** Every `SemconvMigrationCatalogEntry` may carry an optional `SemconvChangelogEvidence` (commit / version / url / quote) pinning the migration claim to an exact upstream commit, so the catalog is auditable without re-parsing CHANGELOG.md.
- **Catalog seeding from upstream.** `scripts/seed-catalog.sh <from-tag> <to-tag>` clones a shallow upstream `open-telemetry/semantic-conventions` checkout, slices CHANGELOG.md between two version tags, and emits C# (or `--format json`) skeleton entries with provenance pre-filled. Curate the `Kind`/`Domain`/`Signal`/`MigrationKind` fields by hand before merging.
- **Generated docs and audit.** Regenerate the package catalog with `scripts/generate-docs.sh generate`; validate with `scripts/generate-docs.sh validate`; print the current 156-entry coverage audit with `scripts/generate-docs.sh audit`. Every `OTSC*` rule has a stable `#otsc0010` anchor in the generated docs that every `DiagnosticDescriptor.HelpLinkUri` resolves to.
- **netstandard2.0** only — required by Roslyn analyzer host. Microsoft.CodeAnalysis.* dependencies only.
- **Multi-version friendly.** A consumer on SemConv 1.39.0 gets live metadata diagnostics scoped to the 1.39.0 surface; upgrading expands those diagnostics automatically. The supplemental catalog is a conservative v1.41.0 migration aid.

## Configuration

Set `build_property.OtelSemConvLegacyMode` in analyzer config:

```ini
is_global = true
build_property.OtelSemConvLegacyMode = production
```

| Value | Behavior |
|---|---|
| `production` | Default behavior. Production exact supplemental migrations can report `OTSC0030` errors. |
| `compatibility` | Downgrades production supplemental errors to warnings and keeps fixture contexts informational. |
| `off` | Disables supplemental catalog diagnostics. Live `[Obsolete]` metadata rules remain enabled. |

`build_property.IsTestProject = true`, assembly names ending in `.Tests`, paths under `tests/`, and xUnit/NUnit/MSTest attributed methods or types are treated as test context for supplemental catalog severity.

## Validation

Run the repository gates before changing diagnostics, catalog data, or generated documentation:

```bash
dotnet build ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.slnx -c Release
dotnet test tests/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.Tests/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.Tests.csproj
scripts/generate-docs.sh validate
scripts/generate-docs.sh audit
git diff --check
```

## Incubation

This repository is the incubation home for what may eventually be proposed as an official `OpenTelemetry.SemanticConventions.Analyzers` companion to `opentelemetry-dotnet-contrib`. While here, it ships under the `ANcpLua.*` package ID and Apache-2.0 license.

## License

Apache-2.0. Compatible with future donation to OpenTelemetry.
