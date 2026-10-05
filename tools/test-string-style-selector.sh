#!/usr/bin/env bash
# Local acceptance command for the explicit string style selector.
# Runs only the StringStyleSelector emitter integration tests and the sample.
set -euo pipefail
cd "$(dirname "$0")/.."

FRAMEWORK="${FRAMEWORK:-net10.0}"

echo "== StringStyleSelector unit and Emitter integration tests ($FRAMEWORK) =="
dotnet test YamlDotNet.Test/YamlDotNet.Test.csproj \
    -f "$FRAMEWORK" \
    --filter "FullyQualifiedName~StringStyleSelector" "$@"

echo "== UseStringStyleSelector sample =="
dotnet test YamlDotNet.Samples/YamlDotNet.Samples.csproj \
    --filter "FullyQualifiedName~UseStringStyleSelector" "$@"
