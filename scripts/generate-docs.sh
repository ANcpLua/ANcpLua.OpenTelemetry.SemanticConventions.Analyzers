#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mode="${1:-generate}"

case "$mode" in
  generate)
    dotnet run -c Release --project "$repo_root/tools/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.DocsGenerator"
    ;;
  validate|check|--check)
    dotnet run -c Release --project "$repo_root/tools/ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.DocsGenerator" -- --check
    ;;
  *)
    echo "usage: scripts/generate-docs.sh [generate|validate]" >&2
    exit 2
    ;;
esac
