# Test-generation research

## Scope

This 25-batch continuation covers the C# compiler, symbol metadata, RSC
emission, and emulator integration paths in the existing solution. The C++
emulator is explicitly out of scope.

## Repository conventions

- SDK-style projects target the solution in `ProjectOberon.NET.slnx`.
- The repository uses a self-contained executable test project at
  `tests/ProjectOberon.Emulator.Tests`; tests are registered through its
  `Program.cs` runner rather than a framework test adapter.
- Existing tests use small named methods and `TestAssertions`, with each suite
  exposing a public `Run()` method.
- The deterministic validation gate is `bash scripts/verify-dotnet.sh`.

## Target inventory

- `src/ProjectOberon.Symbols/OberonSymbolModule.cs`: SMB metadata parsing and
  nested type mapping.
- `src/ProjectOberon.Compiler/OberonCompilerSymbols.cs`: imported type and
  parameter construction.
- `src/ProjectOberon.Compiler/OberonCompilerSemanticChecker.cs`: type
  compatibility and argument validation.
- `src/ProjectOberon.Compiler/OberonCompilerAddressEmitter.cs`: aggregate
  address lowering and explicit unsupported-path diagnostics.
- `src/ProjectOberon.Compiler/OberonCompilerModuleEmitter.cs`: RSC fixups,
  aggregate copying, and module data/code emission.
- `tests/ProjectOberon.Emulator.Tests/OberonCompilerSymbolFileTests.cs`:
  symbol-file round trips and malformed metadata coverage.
- `tests/ProjectOberon.Emulator.Tests/OberonCompilerSymbolsTests.cs`:
  imported type construction.
- `tests/ProjectOberon.Emulator.Tests/OberonCompilerParserTests.cs`:
  parser and semantic compatibility coverage.
- `tests/ProjectOberon.Emulator.Tests/OberonCompilerModuleEmitterTests.cs`:
  instruction-level compiler output.
- `tests/ProjectOberon.Emulator.Tests/OberonCompilerRscTests.cs`: RSC fixups
  and external call execution.

## Acceptance checklist

1. Preserve SMB nested record/array/pointer metadata without changing the
   origin C++ tree.
2. Cover imported type construction and qualified selectors for the supported
   combinations.
3. Cover external scalar, aggregate, `VAR`, pointer, and open-array call
   lowering where the current RSC representation supports it.
4. Keep unsupported aggregate function-result copying explicit and tested.
5. Run the narrow executable test suite and the repository verification gate.
6. Keep each coherent implementation increment in a local commit; do not push.
