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
| Compiler CLI output options are stable | Parse SMB/RSC output options and reject invalid command lines | `CompilerCliOptionTests.ParsesOutputOptions`, `CompilerCliOptionTests.RejectsMissingOutputValue`, `CompilerCliOptionTests.RejectsUnknownOption` |
| RSC reader rejects unsafe code layout | Validate command and body offsets when reading disk modules | `RscModuleTests.RejectsInvalidCodeLayout` |
| Compiler emitter is repeatable | Reset generated code and verify repeated emission is byte-for-byte deterministic | `OberonCompilerModuleEmitterTests.EmitsSameModuleDeterministically` |
| Compiler diagnostics are stable | Format source path, line, column, and multiple messages consistently | `CompilerDiagnosticTests.FormatsFileLocationAndMessage`, `CompilerDiagnosticTests.FormatsMultipleDiagnosticsOnSeparateLines` |
| Runtime module failures expose their code | Include `OberonModuleLoadException.Code` in diagnostics | `RuntimeDiagnosticsTests.FormatsModuleLoadCode` |
| Repeated compiler strings share one payload | Keep identical string literals from expanding module data | `OberonCompilerModuleEmitterTests.DeduplicatesRepeatedStringData` |
| Compiler RSC preserves string data | Emit string bytes into the RSC string section and round-trip them | `OberonCompilerRscTests.EmitsStringPayload` |
| RSC entry and fixup heads stay in bounds | Reject invalid code and type-section offsets before memory loading | `RscModuleTests.RejectsInvalidEntriesAndFixups` |
| Runtime exposes the active module data base | Initialize r13 alongside the module table and stack registers | `OberonModuleTests.MemoryImage` |
| RSC pointer tables stay inside module data | Reject negative, unaligned and out-of-range pointer slots | `RscModuleTests.RejectsInvalidPointerReferences` |
| RSC names remain ASCII | Reject non-ASCII bytes instead of silently replacing them | `RscModuleTests.RejectsNonAsciiStrings` |
| RSC files end at their trailer | Reject appended bytes that are not part of the encoded module | `RscModuleTests.RejectsTrailingBytes` |
| RSC fixup validation is overflow-safe | Reject extreme type fixup values without arithmetic overflow | `RscModuleTests.RejectsInvalidEntriesAndFixups` |
