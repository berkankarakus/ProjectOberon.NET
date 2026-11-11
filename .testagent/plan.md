# Architecture test plan

| Requirement | Implementation | Verification |
|---|---|---|
| Core has no project dependencies | Read all SDK project references and assert the core set is empty | `ArchitectureTests.CoreHasNoProjectReferences` |
| Adapters depend only on core | Assert each adapter's references equal the core project | `ArchitectureTests.AdaptersDependOnlyOnCore` |
| Tests are the only consumer of adapter projects | Assert production projects do not reference tests and adapters do not reference one another | `ArchitectureTests.ProductionProjectsDoNotDependOnTests` and `ArchitectureTests.AdaptersDoNotDependOnEachOther` |
| Direction is documented | Add `docs/architecture.md` with Mermaid graph and hierarchy | File review plus solution build |
| Folder hierarchy is real and usable | Move projects to `src/` and `tests/`, update project and solution paths | Solution build and test runner |
| Minimal compiler RSC output is round-trippable | Build `RscModule` from compiler output and parse the writer result | `OberonCompilerRscTests.EmitsMinimalRscModule` |
| Imports and exported commands survive compilation | Map imports and top-level exported procedure labels to RSC metadata | `OberonCompilerRscTests.EmitsImportsAndExportedCommands` |
| Compiler output is accepted by the runtime loader | Materialize generated RSC and resolve an exported command address | `OberonCompilerRscTests.LoadsCompilerProducedRsc` |
| RSC code offsets are safe | Reject unaligned and out-of-range command/body offsets before writing | `RscModuleTests.RejectsInvalidCodeOffsets` |
| Minimal RSC limitation is explicit | Reject record/pointer descriptor data before producing an incomplete module | `OberonCompilerRscTests.RejectsUnsupportedMinimalRscTypes` |
