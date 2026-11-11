#!/usr/bin/env bash

set -euo pipefail

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
solution="$repo_root/ProjectOberon.NET.slnx"
test_binary="$repo_root/tests/ProjectOberon.Emulator.Tests/bin/Release/net10.0/ProjectOberon.Emulator.Tests"

dotnet build "$solution" --configuration Release --disable-build-servers -m:1 -v:q
"$test_binary" --skip-disk-tests
