#!/usr/bin/env bash

set -euo pipefail

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
artifact_root="${PROJECT_OBERON_DOTNET_ARTIFACTS:-${TMPDIR:-/tmp}/projectoberon-dotnet-artifacts}"
rom_path="${PROJECT_OBERON_ROM:-$repo_root/prom.mem}"
disk_path="${PROJECT_OBERON_DISK:-$repo_root/RISC-work.img}"
frames="${PROJECT_OBERON_SDL_BENCHMARK_FRAMES:-120}"
steps="${PROJECT_OBERON_SDL_BENCHMARK_STEPS:-1000}"

if [[ ! -f "$rom_path" || ! -f "$disk_path" ]]; then
  echo "Set PROJECT_OBERON_ROM and PROJECT_OBERON_DISK to run the SDL benchmark." >&2
  exit 2
fi

dotnet build "$repo_root/src/ProjectOberon.SdlUi/ProjectOberon.SdlUi.csproj" \
  --configuration Release \
  --disable-build-servers \
  -m:1 \
  -v:q \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="$artifact_root"

ui_binary="$artifact_root/bin/ProjectOberon.SdlUi/release/project-oberon-ui"
SDL_VIDEODRIVER=dummy /usr/bin/time -p "$ui_binary" \
  --rom "$rom_path" \
  --disk "$disk_path" \
  --read-only \
  --frames "$frames" \
  --steps-per-frame "$steps" \
  --scale 1
