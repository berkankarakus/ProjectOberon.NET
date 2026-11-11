# Architecture test plan

| Requirement | Implementation | Verification |
|---|---|---|
| Core has no project dependencies | Read all SDK project references and assert the core set is empty | `ArchitectureTests.CoreHasNoProjectReferences` |
| Adapters depend only on core | Assert each adapter's references equal the core project | `ArchitectureTests.AdaptersDependOnlyOnCore` |
| Tests are the only consumer of adapter projects | Assert production projects do not reference tests and adapters do not reference one another | `ArchitectureTests.ProductionProjectsDoNotDependOnTests` and `ArchitectureTests.AdaptersDoNotDependOnEachOther` |
| Direction is documented | Add `docs/architecture.md` with Mermaid graph and hierarchy | File review plus solution build |
| Folder hierarchy is real and usable | Move projects to `src/` and `tests/`, update project and solution paths | Solution build and test runner |
