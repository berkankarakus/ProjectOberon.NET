#!/usr/bin/env bash

set -euo pipefail

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
solution="$repo_root/ProjectOberon.NET.slnx"
artifact_root="${PROJECT_OBERON_DOTNET_ARTIFACTS:-${TMPDIR:-/tmp}/projectoberon-dotnet-artifacts}"
test_binary="$artifact_root/bin/ProjectOberon.Emulator.Tests/release/ProjectOberon.Emulator.Tests"

dotnet build "$solution" \
  --configuration Release \
  --disable-build-servers \
  -m:1 \
  -v:q \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="$artifact_root"
if [[ ! -x "$test_binary" ]]; then
  echo "Release test runner was not produced: $test_binary" >&2
  exit 1
fi

PROJECT_OBERON_REPO_ROOT="$repo_root" "$test_binary" --skip-disk-tests
