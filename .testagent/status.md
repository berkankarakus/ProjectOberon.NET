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
- No assertion-free or always-true assertions were added.
- The tests intentionally do not claim type descriptor or pointer-fixup
  coverage; those remain an explicit compiler gap.

## Validation

`dotnet build ProjectOberon.NET.slnx --disable-build-servers -m:1 -v:q` passed
with 0 warnings and 0 errors.

`dotnet run --project tests/ProjectOberon.Emulator.Tests/ProjectOberon.Emulator.Tests.csproj --no-restore --disable-build-servers -- --skip-disk-tests` passed all tests.
