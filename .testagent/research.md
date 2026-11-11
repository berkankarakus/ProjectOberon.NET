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
