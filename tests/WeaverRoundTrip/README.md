# Weaver round-trip smoke test

End-to-end fixture that proves the analyzer's `[Obsolete]` reading still
matches what [Weaver](https://github.com/open-telemetry/weaver) actually
emits for a deprecated semantic-convention attribute. If Weaver changes its
default note format ("Replaced by `X`.") in a future release, the
`WeaverRoundTripTests` xUnit test goes red and the regression is visible
before it ships to consumers.

## Layout

```
tests/WeaverRoundTrip/
├── .weaver-version              # Pinned Weaver version (consumed by generate.sh)
├── generate.sh                  # Regenerates generated/ from model/ + templates/
├── model/
│   ├── manifest.yaml            # Registry manifest (schema_url)
│   └── http.yaml                # One deprecated attribute: http.method → http.request.method
├── templates/csharp/
│   ├── weaver.yaml              # Template config
│   └── HttpAttributes.cs.j2     # Jinja template emitting the [Obsolete] const
└── generated/
    └── HttpAttributes.cs        # COMMITTED output of `generate.sh` — drift-checked in CI
```

## Refreshing the fixture

```bash
tests/WeaverRoundTrip/generate.sh
```

The script aborts if the installed `weaver` version does not match
`.weaver-version`. To bump Weaver: update `.weaver-version`, re-run the
script, commit the new `generated/HttpAttributes.cs` together with the
version bump.

## What CI enforces

`.github/workflows/weaver-roundtrip.yml` runs the script in CI, asserts
`git diff --exit-code tests/WeaverRoundTrip/generated/` is clean, and runs
the xUnit test. A failure on either side is the early warning that
Weaver's output and our analyzer have drifted apart.
