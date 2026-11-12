# Architecture test research

## Scope

This is a broad, repository-wide architecture-test request covering all seven
SDK-style projects and the project-level dependency graph.

## Codegraph findings

The current project graph has one dependency root and five production adapters:

- `ProjectOberon.Emulator` has no project references and is the core runtime,
  storage, compiler, and module-format library.
- `ProjectOberon.Cli`, `ProjectOberon.CompilerCli`, `ProjectOberon.FsCli`,
  `ProjectOberon.RscCli`, and `ProjectOberon.SdlUi` each depend on the emulator.
- `ProjectOberon.Emulator.Tests` depends on the emulator and every production
  adapter so the repository's executable test runner can exercise all surfaces.
- No production project may depend on the test project.

Codegraph was synchronized before implementation. Its `status`/`files` commands
currently report an SQLite open error even though `codegraph sync .` succeeds;
the dependency direction was therefore cross-checked against the indexed
project-reference declarations.

## Acceptance checklist

1. Add an executable architecture test that checks the allowed project graph.
2. Reject production-to-test references and adapter-to-adapter references.
3. Document the dependency direction and folder layout.
4. Move production projects under `src/` and tests under `tests/`, updating all
   solution, project-reference, CI, and README paths.
5. Build the solution and run the complete repository test runner.

## Follow-up batch

The compiler emitter now exposes a minimal RSC artifact: code, variable/string
data, imports, exported top-level procedure commands, and body offset. Type
descriptor and pointer-fixup generation are intentionally not claimed yet.

## Repository-wide pairing scan

The static `Find-UntestedSources` scan was rerun after the refactor batch:

- 154 production C# files and 99 test files were discovered.
- 94 production files were paired directly and 60 were reported as unpaired.
- The unpaired list is conservative rather than a coverage measurement. The
  test project centralizes compiler, RSC, and Symbols namespaces in
  `CompilerGlobalUsings.cs`, while the scanner only considers `using` directives
  inside each individual test file. Consequently, existing suites such as
  `OberonCompilerSymbolsTests`, `OberonCompilerScannerTests`,
  `OberonCompilerSymbolFileTests`, and `SymbolModuleTests` appear in the
  unpaired list even though they exercise the corresponding source types.
- The scan was used as a review aid, not as an instruction to add duplicate
  test files. Existing test suites were checked for the highest-declaration
  candidates, and no new untested production surface was identified from this
  report alone.

Architecture tests and the executable test runner remain the authoritative
verification for this repository because they compile against the actual
project references and execute behavior rather than inferring pairing from
syntax alone.

## Current continuation scope

The current 25-batch continuation targets the remaining compiler/RSC gaps while
keeping the C++ emulator untouched. The static pairing scan was rerun with the
Roslyn analyzer: 154 source files and 99 test files were found; 94 paired and
60 conservatively unpaired. The unpaired result is affected by centralized
global usings, so existing suite coverage remains authoritative.

Primary targets are `OberonSymbolModule`, imported compiler symbols, semantic
type compatibility, address/code emission, and external RSC call paths. The
remaining explicit product gaps are type descriptors/pointer fixups, aggregate
function-result copying, aggregate selector execution coverage, and `IS`
lowering.
