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

test_args=(--skip-disk-tests)
if [[ "${PROJECT_OBERON_RUN_DISK_TESTS:-0}" == "1" ]]; then
  if [[ -z "${PROJECT_OBERON_DISK:-}" || ! -f "$PROJECT_OBERON_DISK" ]]; then
    echo "PROJECT_OBERON_DISK must point to a compatible RISC-work.img when PROJECT_OBERON_RUN_DISK_TESTS=1" >&2
    exit 1
  fi

  test_args=()
fi

if (( ${#test_args[@]} == 0 )); then
  PROJECT_OBERON_REPO_ROOT="$repo_root" "$test_binary"
else
  PROJECT_OBERON_REPO_ROOT="$repo_root" "$test_binary" "${test_args[@]}"
fi
