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
| Unsupported `IS` syntax is diagnosed explicitly | Parse the relation and report the missing runtime type-test lowering | `OberonCompilerParserTests.ReportsUnsupportedTypeTests` |
| Compiler RSC body uses the module data base | Execute a minimal compiled assignment through the runtime loader | `OberonCompilerRscTests.ExecutesCompilerProducedBody` |
| Runtime stack cannot overlap module allocations | Reject explicitly supplied stack pointers below the next free module address | `OberonModuleTests.MemoryImage` |
| Compiler RSC code starts word-aligned | Pad the generated string section before placing RISC5 code | `OberonCompilerRscTests.EmitsStringPayload` |
| Compiler RSC string assignments execute | Run generated character-copy code through the module loader | `OberonCompilerRscTests.ExecutesCompilerProducedStringBody` |
| RSC data sections produce aligned code addresses | Reject modules whose type, variable and string sections end unaligned | `RscModuleTests.RejectsUnalignedDataSections` |
| RSC module names fit runtime descriptors | Reject names that cannot fit the 32-byte Oberon descriptor field | `RscModuleTests.RejectsLongModuleNames` |
| RSC writer mirrors module name validation | Reject oversized module names before serialization | `RscModuleTests.RejectsLongModuleName` |
| Memory materialization failures carry module-load codes | Wrap malformed fixup-chain failures with module context | `OberonModuleTests.Fixups` |
| Test exceptions can be inspected | Return the captured exception from the assertion helper | `OberonModuleTests.Fixups` |
| CLI traces expose module state | Include r12/r13 alongside stack and return registers | `CliTraceTests.FormatsModuleAndStackRegisters` |
| External module entry offsets are aligned | Reject host bindings that would create unaligned code targets | `OberonModuleTests.SymbolBinding` |
| Host-call registration validates before mutation | Reject unaligned host offsets before binding or dispatcher registration | `HostCallTests.BindsExternalEntryHandlers` |
| Compiler outputs cannot overwrite each other | Reject equivalent SMB/RSC output paths during option parsing | `CompilerCliOptionTests.RejectsSharedOutputPath` |
| Compiler CLI accepts one source only | Reject ambiguous positional and `--source` combinations | `CompilerCliOptionTests.RejectsMultipleSourcePaths` |
| RSC CLI supports the conventional short help flag | Treat `-h` like `--help` before parsing other options | `SubcommandOptionTests.HandlesRscHelp` |
| Filesystem CLI supports the conventional short help flag | Treat `-h` like `--help` before parsing other options | `SubcommandOptionTests.HandlesFileSystemHelp` |
| Main CLI help is a short-circuit | Print help even when extra arguments follow `-h` or `--help` | `CliOptionTests.UsesSafeDefaults` |
| Compiler parser phases are isolated | Keep declarations, statements and expressions in focused partials instead of one parser file | `OberonCompilerParserTests` |
| Compiler assembly is independent | Keep compiler implementation out of the emulator assembly while depending only on runtime instruction and RSC contracts | `ArchitectureTests.CompilerDependsOnlyOnContracts`, solution build |
| Compiler namespace matches assembly ownership | Expose compiler symbols from `ProjectOberon.Compiler` instead of the emulator namespace | compiler build and compiler tests |
| Shared contracts have no runtime dependency | Keep instruction encoding and RSC model contracts below both emulator and compiler assemblies | `ArchitectureTests.CoreDependsOnlyOnContracts`, `ArchitectureTests.CompilerDependsOnlyOnContracts` |
| RSC format assembly is independent | Keep RSC reader, writer and validation above contracts and separate from runtime implementation | `ArchitectureTests.RscDependsOnlyOnContracts`, solution build |
| SMB format assembly is independent | Keep symbol-module models and reader reusable without runtime dependencies | `ArchitectureTests.SymbolsHaveNoProjectReferences`, solution build |
| Compiler can consume external SMB procedures | Load imported symbol modules, preserve procedure arity, and resolve qualified calls | `OberonCompilerParserTests.ImportsExternalProcedures`, `OberonCompilerParserTests.RejectsWrongExternalProcedureArity`, `CompilerCliOptionTests.ParsesImportedSymbols` |
| External compiler calls fail explicitly | Report the pending RSC import lowering instead of a missing internal code label | `OberonCompilerModuleEmitterTests.ReportsExternalProcedureLoweringGap` |
| Compiler label ownership is independent | Move labels out of the concrete code generator file to remove the writer/generator file cycle | `OberonCompilerModuleEmitterTests` |
| Host binding assembly is shared | Centralize export, entry-offset and handler packaging for Files/Kernel/Modules | Existing host binding suites |
| Memory map ownership is independent | Move address constants into a shared map so device code does not depend on `Memory` | `DeviceMmioTests`, `DeviceStorageTests`, `OberonModuleTests` |
| RSC model ownership is independent | Move RSC records out of the reader so model and validation dependencies remain one-directional | `RscModuleTests`, `OberonCompilerRscTests` |
| File-system model ownership is independent | Move directory and file-header records out of the filesystem service so the header codec does not depend on its consumer | `OberonFileSystemTests`, `OberonFileHeaderTests` |
| Compiler import names are validated early | Reject non-identifier SMB aliases during option parsing | `CompilerCliOptionTests.RejectsInvalidImportName` |
| Compiler import aliases are trustworthy | Reject an alias that does not match the SMB module descriptor | `CompilerCliOptionTests.RejectsImportAliasMismatch`, `CompilerCliOptionTests.LoadsMatchingImport` |
| SMB exports are unique | Reject duplicate export names instead of silently shadowing them | `SymbolModuleTests.RejectsDuplicateExports` |
| Compiler CLI declares its format dependency | Reference the standalone Symbols assembly directly and enforce it in architecture tests | `ArchitectureTests.AdaptersDependOnlyOnCore`, solution build |
| RSC table counts are payload-bounded | Reject code and entry counts before allocating arrays larger than the remaining file | `RscModuleTests.RejectsCodeAndEntryCountsLargerThanThePayload` |
| SMB numbers have bounded encodings | Reject variable-length numbers with more than four continuation bytes | `SymbolModuleTests.RejectsOverlongNumbers` |
| SMB writer preserves text fidelity | Reject non-ASCII module, export, and nested type names before serialization | `OberonCompilerSymbolFileTests.RejectsInvalidModuleNames`, `OberonCompilerSymbolFileTests.RejectsNonAsciiNestedNames` |
| Host-call register arguments are explicit | Reject register numbers outside r0-r15 instead of relying on CPU masking | `HostCallTests.ValidatesHostCallContextArguments` |
| Kernel branch installation is safe | Reject unaligned addresses and out-of-range branch displacements | `KernelHostBindingTests.DispatchesInstallService` |
| Memory and disk lifecycles are covered | Validate ROM parsing, reset behavior, closed disks, and invalid sectors | `MemoryTests`, `CpuTests.ResetsStateAndValidatesAddresses`, `DeviceTests.DiskImageValidatesLifecycle` |
| Release verification is repeatable | Run the same Release build and deterministic test flow locally and in CI | `scripts/verify-dotnet.sh` |
| Host-call strings have explicit limits | Validate Latin-1 round-trips, NUL termination, and positive maximum lengths | `HostCallTests.ValidatesHostCallStrings` |
| Host-call registrations are unique | Reject duplicate, masked-collision, and unaligned dispatcher addresses | `HostCallTests.ValidatesHostCallRegistration` |
| Module loader failures rollback cleanly | Report missing/invalid modules without leaving loaded state behind | `OberonModuleTests.LoaderGraph` |
| RSC writer validates encoded text | Reject non-ASCII, NUL-containing names, and invalid trailers before serialization | `RscModuleTests.RejectsInvalidNamesAndTrailer` |
| Core memory control flow stays readable | Keep Memory guards explicit while preserving ROM/RAM/MMIO behavior | `MemoryTests`, solution build |
