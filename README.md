# Qyl.OpenTelemetry.SemanticConventions.Analyzers

Roslyn diagnostic analyzers for [`OpenTelemetry.SemanticConventions`](https://www.nuget.org/packages/OpenTelemetry.SemanticConventions) consumers, targeting the v1.41.0 spec.

This package keeps the referenced `OpenTelemetry.SemanticConventions` assembly as the primary source of truth. Live `[Obsolete]` metadata drives the main deprecation rules; a curated supplemental catalog covers migration cases that are not reliably visible through generated constants.

## Status and Installation

Pre-release incubation. The package ID is `Qyl.OpenTelemetry.SemanticConventions.Analyzers`; it is not yet published to NuGet.

When published, consume it as a development-only analyzer dependency:

```xml
<PackageReference Include="Qyl.OpenTelemetry.SemanticConventions.Analyzers"
                  Version="..."
                  PrivateAssets="all"
                  IncludeAssets="runtime; build; native; contentfiles; analyzers; buildtransitive" />
```

For local evaluation from this checkout, reference the analyzer project directly from a consumer project:

```xml
<ProjectReference Include="path/to/src/Qyl.OpenTelemetry.SemanticConventions.Analyzers/Qyl.OpenTelemetry.SemanticConventions.Analyzers.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

## Diagnostics

The package-level generated catalog is in [`docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md`](docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md). It includes titles, descriptions, severities, code-fix availability, examples, configuration, and the supplemental migration-catalog audit.

| ID | Severity | Code fix | Description |
|---|---|---|---|
| [`QYL0001`](docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md#qyl0001) | Warning | No | `gen_ai.execute_tool` span requires `gen_ai.tool.name` for v1.41.0 span naming |
| [`QYL0002`](docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md#qyl0002) | Info | No | `graphql.document` is opt-in because it may carry sensitive/high-cardinality user input |
| [`QYL0005`](docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md#qyl0005) | Warning | No | RPC server span sets `client.address`/`client.port`, which were removed from RPC server spans in v1.41.0 |
| [`QYL0010`](docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md#qyl0010) | Warning | Yes | Direct typed reference to a semantic-convention constant marked `[Obsolete]` |
| [`QYL0011`](docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md#qyl0011) | Info | No | Hard-coded key matches a typed semantic-convention constant |
| [`QYL0012`](docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md#qyl0012) | Warning | Yes | Hard-coded key matches a semantic-convention constant marked `[Obsolete]` |
| [`QYL0014`](docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md#qyl0014) | Warning | Yes | Hard-coded value matches a semantic-convention value constant marked `[Obsolete]` |
| [`QYL0021`](docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md#qyl0021) | Warning | No | Library directly references incubating semantic-convention members |
| [`QYL0030`](docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md#qyl0030) | Error | Exact replacements only | Supplemental catalog exact replacement in production telemetry emission |
| [`QYL0031`](docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md#qyl0031) | Warning | Exact replacements only | Supplemental catalog context-sensitive, manual-review, removed/no-replacement, or ambiguous payload migration |
| [`QYL0032`](docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md#qyl0032) | Info | Exact replacements only | Supplemental catalog item appears in test, fixture, compatibility, generated, translator, or catalog context |

## Examples

```csharp
activity.SetTag(HttpAttributes.AttributeHttpMethod, "GET"); // QYL0010 from live [Obsolete] metadata.
activity.SetTag("http.method", "GET");                     // QYL0012 when the referenced package marks the key [Obsolete].
meter.CreateHistogram<long>("system.memory.shared");        // QYL0030; use "system.memory.linux.shared".
activity.SetTag("error.message", message);                  // QYL0031; replacement is context-sensitive.
```

## Design

- **Live metadata first.** `QYL0010`, `QYL0012`, and `QYL0014` resolve `[Obsolete]` metadata from the consumer's referenced `OpenTelemetry.SemanticConventions` assembly via Roslyn's symbol model, including baggage, tag collection `Add`/indexer writes, collection expressions, and visible dictionary/initializer payload keys and values. The referenced package remains the primary source of truth.
- **Curated migration inventory.** Run `scripts/generate-docs.sh audit` or see the generated documentation for the authoritative count of changelog/model mentions, live `[Obsolete]` metadata rows, supplemental diagnostic rows, and attribute-value fallback rows tracked by the package.
- **Supplemental catalog only where metadata is insufficient.** `QYL0030`-`QYL0032` cover changelog/model entries that are not reliably visible through generated `[Obsolete]` constants, including metric names, removed events, context-sensitive migrations, compatibility payloads, and attribute-value fallbacks when live value metadata is absent. Deprecated generated values remain primarily covered by `QYL0014`.
- **Exact code fixes only.** Live `[Obsolete]` metadata fixes are offered only when the deprecation text exposes one exact replacement; typed constants are replaced with the replacement typed constant when resolvable. Supplemental fixes remain limited to exact one-to-one catalog replacements.
- **Production payload recognition.** Supplemental diagnostics inspect visible inline payloads, collection expressions, local dictionary/collection initializers, and mutable local dictionary writes when they flow into telemetry emission APIs such as `ActivitySource.StartActivity` tags, metric instrument `Add`/`Record` calls, `Measurement<T>` tags, `ActivityEvent`/`ActivityLink` tags, `ResourceBuilder.AddAttributes`, and `ILogger.Log`/`ILogger.BeginScope` state payloads.
- **Context-sensitive severity.** Production telemetry emission can be an error only when the supplemental catalog has an exact one-to-one replacement. Tests, fixtures, migration maps, schema translators, generated code, and compatibility shims downgrade to info. Ambiguous dictionaries and no-direct-replacement items stay warning/manual-review.
- **Per-type suppressor for compatibility shapes.** `SemconvLegacyContextSuppressor` recognises class/struct/record/method names matching `Legacy*` / `*CompatShim` / `*MigrationFixture` / `*SchemaTranslator` / `*DeprecatedSemconv*` and suppresses every `QYL*` diagnostic inside them — structured alternative to scattering `#pragma warning disable` across compatibility code.
- **Multi-hop rename resolution.** `SemconvMigrationCatalog.ResolveTerminalReplacement` chases `ExactRename` chains (e.g. `http.host` → `net.host.name` → `server.address`) so code-fixes land consumers on the terminal symbol, not on a still-deprecated mid-state. Cycles and chains over 8 hops bail at the last safe step.
- **Structured changelog provenance.** Every `SemconvMigrationCatalogEntry` may carry an optional `SemconvChangelogEvidence` (commit / version / url / quote) pinning the migration claim to an exact upstream commit, so the catalog is auditable without re-parsing CHANGELOG.md.
- **Catalog seeding from upstream.** `scripts/seed-catalog.sh <from-tag> <to-tag>` clones a shallow upstream `open-telemetry/semantic-conventions` checkout, slices CHANGELOG.md between two version tags, and emits C# (or `--format json`) skeleton entries with provenance pre-filled. Curate the `Kind`/`Domain`/`Signal`/`MigrationKind` fields by hand before merging.
- **Generated docs and audit.** Regenerate the package catalog with `scripts/generate-docs.sh generate`; validate with `scripts/generate-docs.sh validate`; print the current 156-entry coverage audit with `scripts/generate-docs.sh audit`. Every `QYL*` rule has a stable `#qyl0010` anchor in the generated docs that every `DiagnosticDescriptor.HelpLinkUri` resolves to.
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
| `production` | Default behavior. Production exact supplemental migrations can report `QYL0030` errors. |
| `compatibility` | Downgrades production supplemental errors to warnings and keeps fixture contexts informational. |
| `off` | Disables supplemental catalog diagnostics. Live `[Obsolete]` metadata rules remain enabled. |

`build_property.IsTestProject = true`, assembly names ending in `.Tests`, paths under `tests/`, and xUnit/NUnit/MSTest attributed methods or types are treated as test context for supplemental catalog severity.

| MSBuild property | Default | Behavior |
|---|---|---|
| `build_property.OtelSemConvNonAttributesTiers` | `false` | Extends `QYL0010` beyond `*Attributes` classes to also scan the four other Weaver tiers (`*Metrics`, `*Meters`, `*Events`, `*Activities`) under the SemConv namespace. Default `false` preserves the historic surface so existing consumers see no behaviour change. |

## Producer-agnostic seam

The analyzers bind to producers by namespace shape and type-name suffix, never via a project reference. Any assembly that places a class under one of the recognised SemConv namespace shapes — Weaver-generated, the official `OpenTelemetry.SemanticConventions` NuGet, a hand-rolled qyl helper, anything — is in scope.

### Recognised namespace shapes

`SemconvNamespace.IsInSemconvNamespace` matches four shapes against the root literal `OpenTelemetry.SemanticConventions`:

| Shape | Example namespace | Producer it captures |
|---|---|---|
| `s == Root` | `OpenTelemetry.SemanticConventions` | A producer that puts `*Attributes` directly under the bare root |
| `s.StartsWith(Root + ".")` | `OpenTelemetry.SemanticConventions.Attributes` | Upstream's conventional `.Attributes` sub-namespace |
| `s.Contains("." + Root + ".")` | `Qyl.OpenTelemetry.SemanticConventions.Http.Attributes` | A consumer-side nested layout |
| `s.EndsWith("." + Root)` | `Qyl.OpenTelemetry.SemanticConventions` | A consumer-side trailing layout |

Every branch is covered by a smoke test in `tests/.../DeprecatedSemconvAnalyzerTests.cs`; a regression in any of the four becomes a red test, not a silent loss of `QYL0010`/`0011`/`0012`/`0014` coverage for that shape.

### Type-name suffix constraint

By default `SemconvNamespace.IsAttributesType` requires `type.Name.EndsWith("Attributes")`. Weaver SourceGeneration emits five tiers per stability band (`*Attributes`, `*Metrics`, `*Meters`, `*Events`, `*Activities`); setting `build_property.OtelSemConvNonAttributesTiers = true` extends the suffix check to the other four.

### Accepted `[Obsolete]` note formats

`SemconvCodeFixHelpers.TryExtractExactReplacement` accepts these shapes (case-insensitive prefix, optional trailing period, optional backticks/quotes around the replacement):

- `Replaced by http.request.method.`
- `` Replaced by `http.request.method`. `` *(Weaver's default form)*
- `Use <c>http.request.method</c> instead.`

It explicitly rejects ambiguous or unfamiliar shapes (`Use 'X' instead.`, `Migrated to X.`, `Replaced by X and Y.`, `Deprecated.`, empty / whitespace). The diagnostic still fires for rejected shapes — only the code-fix is silently withheld, because guessing the replacement string from a freeform note would ship false fixes into consumer codebases. The full accept/reject contract is pinned in `tests/.../SemconvCodeFixHelpersTests.cs`.

### Manual Weaver round-trip

```bash
# 1. Validate the local registry against the OTel schema:
weaver registry check -r tests/WeaverRoundTrip/model

# 2. (optional) Inspect the resolved registry:
weaver registry resolve -r tests/WeaverRoundTrip/model --quiet

# 3. Regenerate tests/WeaverRoundTrip/generated/HttpAttributes.cs:
tests/WeaverRoundTrip/generate.sh

# 4. Confirm the analyzer fires QYL0010 against the regenerated file:
dotnet build Qyl.OpenTelemetry.SemanticConventions.Analyzers.slnx \
  -c Release -warnaserror:QYL0010
dotnet test tests/Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests/Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests.csproj \
  --filter 'FullyQualifiedName~WeaverRoundTrip'
```

CI runs `.github/workflows/weaver-roundtrip.yml` with the same sequence on every push that touches `src/**`, the fixture, or the test, plus `.github/workflows/supplemental-catalog-drift.yml` on a weekly cron so a silent upstream YAML addition surfaces between releases rather than at consumer-build time.

## Validation

Run the repository gates before changing diagnostics, catalog data, or generated documentation:

```bash
dotnet build Qyl.OpenTelemetry.SemanticConventions.Analyzers.slnx -c Release
dotnet test tests/Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests/Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests.csproj
scripts/generate-docs.sh validate
scripts/generate-docs.sh audit
git diff --check
```

## Incubation

This repository is the incubation home for what may eventually be proposed as an official `OpenTelemetry.SemanticConventions.Analyzers` companion to `opentelemetry-dotnet-contrib`. While here, it ships under the `ANcpLua.*` package ID and Apache-2.0 license.

## License

Apache-2.0. Compatible with future donation to OpenTelemetry.
