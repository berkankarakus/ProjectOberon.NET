# ProjectOberon.NET architecture

The repository uses a one-way dependency graph. Production adapters depend on
the emulator core; the core does not depend on an adapter or on tests.

```mermaid
flowchart TD
    Core[ProjectOberon.Emulator]
    Cli[ProjectOberon.Cli]
    Compiler[ProjectOberon.CompilerCli]
    Fs[ProjectOberon.FsCli]
    Rsc[ProjectOberon.RscCli]
    Sdl[ProjectOberon.SdlUi]
    Tests[ProjectOberon.Emulator.Tests]

    Cli --> Core
    Compiler --> Core
    Fs --> Core
    Rsc --> Core
    Sdl --> Core
    Tests --> Core
    Tests --> Cli
    Tests --> Compiler
    Tests --> Fs
    Tests --> Rsc
    Tests --> Sdl
```

The arrows mean “references”. The test project is intentionally the only
consumer of the adapters, because the repository uses one executable test
runner for emulator, CLI, RSC, filesystem, and SDL helper coverage.

## Folder hierarchy

```text
src/
  ProjectOberon.Emulator/       core runtime, compiler, filesystem, RSC
  ProjectOberon.Cli/            machine/runtime CLI
  ProjectOberon.CompilerCli/   Oberon source compiler CLI
  ProjectOberon.FsCli/          disk filesystem CLI
  ProjectOberon.RscCli/         RSC inspection CLI
  ProjectOberon.SdlUi/          SDL frontend
tests/
  ProjectOberon.Emulator.Tests/ executable test runner
docs/
  architecture.md
```

The dependency rules are enforced by `ArchitectureTests` in the test runner.
Adding a project-to-project reference requires updating that test intentionally
instead of silently creating a reverse or adapter-to-adapter dependency.

Shared SDK settings (`net10.0`, nullable analysis, implicit usings, and warnings
as errors) live in the repository-level `Directory.Build.props`; individual
projects keep only project-specific settings and references.
