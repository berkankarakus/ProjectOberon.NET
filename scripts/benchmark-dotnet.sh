#!/usr/bin/env bash

set -euo pipefail

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
artifact_root="${PROJECT_OBERON_DOTNET_ARTIFACTS:-${TMPDIR:-/tmp}/projectoberon-dotnet-artifacts}"
test_binary="$artifact_root/bin/ProjectOberon.Emulator.Tests/release/ProjectOberon.Emulator.Tests"

dotnet build "$repo_root/ProjectOberon.NET.slnx" \
  --configuration Release \
  --disable-build-servers \
  -m:1 \
  -v:q \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="$artifact_root"

PROJECT_OBERON_REPO_ROOT="$repo_root" "$test_binary" --benchmark
