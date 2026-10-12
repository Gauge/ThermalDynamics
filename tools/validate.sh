#!/usr/bin/env bash
# Compile Thermodynamics and its local test projects without deploying any mod.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
filter='scope!=integration'
fast=false
build_only=false
for arg in "$@"; do
  case "$arg" in
    --integration) filter='' ;;
    --fast) fast=true ;;
    --build-only) build_only=true ;;
    --help) echo 'Usage: bash tools/validate.sh [--integration] [--fast] [--build-only]'; exit 0 ;;
    *) echo "Unknown option: $arg" >&2; exit 2 ;;
  esac
done
if "$fast"; then
  if [[ -n "$filter" ]]; then filter="$filter&speed!=slow"; else filter='speed!=slow'; fi
fi
if ! "$build_only"; then
  command -v systemd-run >/dev/null || { echo 'Tests require systemd-run for the memory cap (docs/rules.md O1).' >&2; exit 2; }
fi
dotnet build tests/Thermodynamics.slnx --no-restore -m:1 -nr:false -p:MdkBuildConfiguration=NoPackage -v quiet
if "$build_only"; then exit 0; fi
args=(dotnet test tests/Thermodynamics.Tests --no-build --no-restore -v quiet
  --results-directory out/validation --logger 'trx;LogFileName=validation.trx')
if [[ -n "$filter" ]]; then args+=(--filter "$filter"); fi
systemd-run --user --scope -p "MemoryMax=${THERMAL_TEST_MEMORY_LIMIT:-24G}" -p MemorySwapMax=0 --quiet "${args[@]}"
