# Test quality review

## Scope reviewed

- `tests/ProjectOberon.Emulator.Tests/ArchitectureTests.cs`
- `tests/ProjectOberon.Emulator.Tests/OberonCompilerRscTests.cs`

## Review result

- Architecture tests use set equality for the allowed project graph, so an
  extra, missing, reverse, or adapter-to-adapter reference fails the suite.
- RSC tests assert module name, section sizes, code round-trip, imports,
  exported command offsets, body offsets, runtime command resolution, and
  rejection of invalid code offsets and unsupported descriptor types.
- Compiler CLI option tests cover successful SMB/RSC parsing and both missing
  value and unknown-option failures.
- RSC reader tests now cover invalid command/body layout before module loading.
- Runtime diagnostics now expose module-load error codes for actionable logs.
- Compiler string payloads are tested for deduplication and RSC round-trip
  preservation.
- RSC entry and fixup heads are tested for code/type-section bounds; the
  reader and writer share these validations with duplicate-name checks.
- CLI instruction traces now include the module table and module data-base
  registers used by compiler-generated code.
- Compiler-generated scalar and string RSC bodies are executed through the
  runtime loader in tests.
- No assertion-free or always-true assertions were added.
- The tests intentionally do not claim type descriptor or pointer-fixup
  coverage; those remain an explicit compiler gap.

## Validation

`dotnet build ProjectOberon.NET.slnx --disable-build-servers -m:1 -v:q` passed
with 0 warnings and 0 errors.

`dotnet run --project tests/ProjectOberon.Emulator.Tests/ProjectOberon.Emulator.Tests.csproj --no-restore --disable-build-servers -- --skip-disk-tests` passed all tests.

The repository-wide static pairing scan completed with 154 source files, 99
test files, 94 direct pairings, and 60 conservative unpaired reports. The
reported compiler/Symbols false negatives are caused by shared global usings;
the corresponding test suites already exist and were reviewed. No duplicate
test files were added based on this heuristic result.

## Continuation verification

- Added minimal-RSC descriptor boundary tests for direct records, pointers, and
  arrays of records, including diagnostic text assertions.
- Added repeated-emission and cross-mode isolation tests for one emitter
  instance.
- Added runtime fixtures for compiler-produced record and scalar-array copies,
  plus an external record argument passed through the RSC host-call path.
- `scripts/verify-dotnet.sh` now writes Release artifacts to a user-temporary
  directory. `PROJECT_OBERON_DOTNET_ARTIFACTS` overrides that location, and
  `PROJECT_OBERON_REPO_ROOT` lets architecture tests locate the repository when
  the test binary is outside the checkout.
- Release verification passed after each temporary mutation was restored.

### Verified pseudo-mutations

Two representative mutations were injected and then reverted immediately:

1. Record assignability changed from `expected.Size == actual.Size` to `!=`.
   The Release suite failed in `ParsesExternalRecordFunctionResult`.
2. Minimal-RSC descriptor detection stopped treating records as requiring a
   descriptor. The Release suite failed in
   `RejectsUnsupportedMinimalRscTypes`.

Both mutations were killed; the clean baseline passed afterward. The reviewed
assertion surface contains 1,441 custom assertions across the test sources,
using equality, boolean, negative, and exception categories. No assertion-free
test was introduced in this continuation.

CodeGraph was synchronized successfully after the changes; its SQLite index
required the sandbox-external `codegraph sync .` path because the workspace
sandbox could not open `.codegraph/codegraph.db`.
