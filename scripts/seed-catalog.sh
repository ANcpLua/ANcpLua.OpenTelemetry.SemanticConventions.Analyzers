#!/usr/bin/env bash
#
# seed-catalog.sh — extract semantic-convention deprecation / rename / removal
# entries from the upstream open-telemetry/semantic-conventions CHANGELOG.md
# between two version tags. Produces a C# skeleton you can drop into
# SemconvMigrationCatalog.cs (or a structured JSON sketch for review).
#
# This is the *seeding* tool — it gives you a starting catalog with provenance
# already filled in (commit SHA, version tag, evidence quote). You curate the
# Kind/Domain/Signal/MigrationKind values by hand because changelog text alone
# isn't precise enough to infer them, and a mis-guess here would ship false
# QYL0030 errors into every consumer's build.
#
# Usage:
#   scripts/seed-catalog.sh <from-tag> <to-tag> [--format csharp|json] [--out <path>]
#
# Examples:
#   scripts/seed-catalog.sh v1.40.0 v1.41.0
#   scripts/seed-catalog.sh v1.0.0 v1.41.0 --format json --out /tmp/seed.json
#
# Requirements: git, awk, sed.  Optional: jq for prettier JSON output.
#
# The script clones (or reuses) a shallow checkout of the semconv repo in
# .tools/semconv-upstream-seed/.  No internet round-trip after the first run.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CACHE_DIR="${REPO_ROOT}/.tools/semconv-upstream-seed"
UPSTREAM_URL="https://github.com/open-telemetry/semantic-conventions.git"

FROM_TAG=""
TO_TAG=""
FORMAT="csharp"
OUT=""

while [ $# -gt 0 ]; do
  case "$1" in
    --format) FORMAT="$2"; shift 2 ;;
    --out)    OUT="$2";    shift 2 ;;
    -h|--help)
      sed -n '2,/^set -euo pipefail/p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    -*)
      echo "unknown flag: $1" >&2; exit 2 ;;
    *)
      if   [ -z "$FROM_TAG" ]; then FROM_TAG="$1"
      elif [ -z "$TO_TAG"   ]; then TO_TAG="$1"
      else echo "unexpected positional: $1" >&2; exit 2
      fi
      shift
      ;;
  esac
done

if [ -z "$FROM_TAG" ] || [ -z "$TO_TAG" ]; then
  echo "usage: $0 <from-tag> <to-tag> [--format csharp|json] [--out <path>]" >&2
  exit 2
fi

case "$FORMAT" in
  csharp|json) ;;
  *) echo "--format must be csharp or json (got: $FORMAT)" >&2; exit 2 ;;
esac

# --- 1. Fetch or refresh the upstream checkout -------------------------------
if [ ! -d "$CACHE_DIR/.git" ]; then
  mkdir -p "$(dirname "$CACHE_DIR")"
  git clone --filter=blob:none --no-checkout "$UPSTREAM_URL" "$CACHE_DIR" >&2
fi

git -C "$CACHE_DIR" fetch --tags --force >&2
git -C "$CACHE_DIR" rev-parse "$FROM_TAG^{commit}" >/dev/null \
  || { echo "tag not found in upstream: $FROM_TAG" >&2; exit 1; }
git -C "$CACHE_DIR" rev-parse "$TO_TAG^{commit}" >/dev/null \
  || { echo "tag not found in upstream: $TO_TAG" >&2; exit 1; }

TO_SHA="$(git -C "$CACHE_DIR" rev-parse "$TO_TAG^{commit}")"

# --- 2. Pull just the CHANGELOG.md slice between the two tags ---------------
CHANGELOG_TMP="$(mktemp)"
trap 'rm -f "$CHANGELOG_TMP"' EXIT

git -C "$CACHE_DIR" show "${TO_TAG}:CHANGELOG.md" > "$CHANGELOG_TMP"

# Slice the changelog from the TO_TAG section back to (but not including) the
# FROM_TAG section. Sections are GitHub-flavored `## v1.41.0` headings.
SLICE="$(awk -v from="$FROM_TAG" -v to="$TO_TAG" '
  BEGIN { capturing = 0 }
  /^## / {
    if ($0 ~ ("v" substr(to,2)) || $0 ~ to) { capturing = 1; next }
    if ($0 ~ ("v" substr(from,2)) || $0 ~ from) { exit }
  }
  capturing == 1 { print }
' "$CHANGELOG_TMP")"

if [ -z "$SLICE" ]; then
  echo "no changelog slice found between $FROM_TAG and $TO_TAG" >&2
  echo "(does the upstream CHANGELOG.md use a different heading format?)" >&2
  exit 1
fi

# --- 3. Find candidate lines that describe a migration ----------------------
# Heuristic match — false positives are expected; reviewer's job is to triage.
CANDIDATES="$(printf '%s\n' "$SLICE" | grep -nE \
  -e 'Renam(e|ed|ing)' \
  -e 'Remov(e|ed)' \
  -e 'Deprecat(e|ed)' \
  -e 'Replac(e|ed)' \
  -e 'Move(d)? .* to' \
  -e 'Supersed(e|ed)' \
  || true)"

if [ -z "$CANDIDATES" ]; then
  echo "no migration-shaped lines found in $FROM_TAG..$TO_TAG" >&2
  exit 0
fi

EVIDENCE_URL_BASE="https://github.com/open-telemetry/semantic-conventions/blob/${TO_SHA}/CHANGELOG.md"

# --- 4. Emit the requested format ------------------------------------------
emit_csharp() {
  printf '// Seeded by scripts/seed-catalog.sh from %s..%s @ %s.\n' "$FROM_TAG" "$TO_TAG" "$TO_SHA"
  printf '// Each entry is a SKELETON. Curate OldName, Kind, Signal, Domain, MigrationKind,\n'
  printf '// and ReplacementNames before merging into SemconvMigrationCatalog.cs.\n'
  printf '\n'
  printf '%s\n' "$CANDIDATES" | while IFS= read -r line; do
    [ -z "$line" ] && continue
    ln="${line%%:*}"
    text="${line#*:}"
    text_escaped="$(printf '%s' "$text" | sed 's/"/\\"/g')"
    cat <<CSHARP
new SemconvMigrationCatalogEntry(
    oldName: "TODO_old_name",
    kind: SemconvMigrationItemKind.AttributeKey,    // TODO: AttributeKey | AttributeValue | MetricName | EventName | SpanName | ResourceAttribute | EnumValue | Namespace | Group | GuidanceOnly
    signal: "any",                                  // TODO: trace | metric | log | event | resource | any
    domain: "TODO_domain",                          // TODO: rpc | http | db | messaging | gen_ai | k8s | jvm | system | process | cloud | azure | faas | vcs | feature_flag | otel
    sinceVersion: "${TO_TAG#v}",
    replacementNames: ImmutableArray<string>.Empty, // TODO: fill if ExactRename/ExactValueRename
    migrationKind: SemconvMigrationKind.ManualReview, // TODO: ExactRename | ExactValueRename | RemovedNoReplacement | ContextSensitive | ManualReview | DeprecatedButGenerated
    changelogVersion: "${TO_TAG#v}",
    changelogEvidence: "${text_escaped}",
    defaultProductionSeverity: DiagnosticSeverity.Warning,
    fixtureSeverity: DiagnosticSeverity.Info),
// provenance: ${EVIDENCE_URL_BASE}#L${ln}

CSHARP
  done
}

emit_json() {
  printf '{\n'
  printf '  "fromTag": "%s",\n' "$FROM_TAG"
  printf '  "toTag": "%s",\n' "$TO_TAG"
  printf '  "toCommit": "%s",\n' "$TO_SHA"
  printf '  "entries": [\n'

  first=1
  printf '%s\n' "$CANDIDATES" | while IFS= read -r line; do
    [ -z "$line" ] && continue
    ln="${line%%:*}"
    text="${line#*:}"
    text_escaped="$(printf '%s' "$text" | sed 's/\\/\\\\/g; s/"/\\"/g; s/\t/\\t/g')"
    [ $first -eq 1 ] || printf ',\n'
    first=0
    cat <<JSON
    {
      "oldName": "TODO_old_name",
      "kind": "AttributeKey",
      "signal": "any",
      "domain": "TODO_domain",
      "sinceVersion": "${TO_TAG#v}",
      "replacementNames": [],
      "migrationKind": "ManualReview",
      "changelogVersion": "${TO_TAG#v}",
      "evidence": {
        "commit": "${TO_SHA}",
        "version": "${TO_TAG#v}",
        "url": "${EVIDENCE_URL_BASE}#L${ln}",
        "quote": "${text_escaped}"
      }
    }
JSON
  done

  printf '\n  ]\n'
  printf '}\n'
}

if [ -n "$OUT" ]; then
  mkdir -p "$(dirname "$OUT")"
  if [ "$FORMAT" = "csharp" ]; then emit_csharp > "$OUT"; else emit_json > "$OUT"; fi
  echo "wrote $(wc -l < "$OUT" | tr -d ' ') lines to $OUT" >&2
else
  if [ "$FORMAT" = "csharp" ]; then emit_csharp; else emit_json; fi
fi
