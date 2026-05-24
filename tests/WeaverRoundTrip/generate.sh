#!/usr/bin/env bash
# Regenerates tests/WeaverRoundTrip/generated/HttpAttributes.cs from
# tests/WeaverRoundTrip/model/ using the Weaver version pinned in
# tests/WeaverRoundTrip/.weaver-version.
#
# Run manually after editing the model or the template; CI verifies the
# committed output stays in sync by re-running this script and asserting
# `git diff --exit-code tests/WeaverRoundTrip/generated/`.
#
# Usage:
#   tests/WeaverRoundTrip/generate.sh
#
# Requires `weaver` on PATH; install instructions:
#   https://github.com/open-telemetry/weaver
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PINNED="$(cat "$HERE/.weaver-version")"
INSTALLED="$(weaver --version 2>/dev/null | awk '{print $2}')"

if [ -z "$INSTALLED" ]; then
  echo "weaver not found on PATH; install from https://github.com/open-telemetry/weaver" >&2
  exit 2
fi

if [ "$INSTALLED" != "$PINNED" ]; then
  echo "weaver version mismatch: pinned $PINNED, installed $INSTALLED" >&2
  echo "either install the pinned version or update tests/WeaverRoundTrip/.weaver-version" >&2
  exit 2
fi

# Weaver writes to OUTPUT/<target>/... — flatten to generated/ for diff-friendliness.
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

weaver registry generate \
  -r "$HERE/model" \
  -t "$HERE/templates" \
  csharp "$TMP" \
  --quiet

mkdir -p "$HERE/generated"
cp "$TMP/HttpAttributes.cs" "$HERE/generated/HttpAttributes.cs"
echo "regenerated: $HERE/generated/HttpAttributes.cs"
