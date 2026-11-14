var skipDiskTests = args.Contains("--skip-disk-tests", StringComparer.Ordinal);
var diskTests = new HashSet<string>(StringComparer.Ordinal)
{
    "Oberon boot consumes keyboard"
};

var tests = new (string Name, Action Test)[]
{
    ("arithmetic and flags", CpuTests.ArithmeticAndFlags),
    ("integer signedness and subtract borrow", CpuTests.IntegerSignednessAndSubtractBorrow),
    ("branch and link", CpuTests.BranchAndLink),
    ("memory word and byte access", CpuTests.MemoryWordAndByteAccess),
    ("interrupt vector and RTI", CpuTests.InterruptVectorAndRti),
    ("CPU reset and validation", CpuTests.ResetsStateAndValidatesAddresses),
    ("CPU execution state restore", CpuTests.RestoresCapturedExecutionState),
    ("memory loading and reset", MemoryTests.Run),
    ("MMIO devices", DeviceTests.MmioDevices),
    ("disk image read/write", DeviceTests.DiskImageReadWrite),
    ("disk image lifecycle", DeviceTests.DiskImageValidatesLifecycle),
    ("SPI disk read/write", DeviceTests.SpiDiskReadWrite),
    ("machine timer", DeviceTests.MachineTimer),
    ("debugger ring trace", DeviceTests.CapturesDebuggerRingTrace),
    ("machine run progress", DeviceTests.MachineRunReportsProgress),
    ("host call dispatcher", HostCallTests.DispatchesRegisteredHostCalls),
    ("host call failure diagnostics", HostCallTests.RecordsInstructionBeforeHostCallFailure),
    ("external host binding", HostCallTests.BindsExternalEntryHandlers),
    ("host call argument validation", HostCallTests.ValidatesHostCallContextArguments),
    ("host call string validation", HostCallTests.ValidatesHostCallStrings),
    ("host call registration validation", HostCallTests.ValidatesHostCallRegistration),
    ("Kernel host binding", KernelHostBindingTests.DispatchesClockAndSectorMapServices),
    ("Kernel install host binding", KernelHostBindingTests.DispatchesInstallService),
    ("Kernel heap host binding", KernelHostBindingTests.DispatchesHeapAllocationService),
    ("Kernel garbage collection host binding", KernelHostBindingTests.DispatchesGarbageCollectionServices),
    ("Kernel heap corruption diagnostics", KernelHostBindingTests.ReportsHeapCorruptionLocation),
    ("Kernel sector I/O host binding", KernelHostBindingTests.DispatchesSectorIoServices),
    ("Files host binding", FilesHostBindingTests.DispatchesReadServices),
    ("Files write host binding", FilesHostBindingTests.DispatchesWriteAndLifecycleServices),
    ("Modules host binding", ModulesHostBindingTests.DispatchesLoadAndCommandServices),
    ("Modules load error state", ModulesHostBindingTests.ReportsLoadErrorsThroughModuleState),
    ("module initializer limits", ModulesHostBindingTests.LimitsNonReturningInitializer),
    ("Oberon boot session", OberonBootSessionTests.InitializesAndStartsModule),
    ("Oberon dependency boot", OberonBootSessionTests.BootsDependenciesBeforeRoot),
    ("Oberon boot consumes keyboard", OberonBootSessionTests.BootConsumesKeyboardInput),
    ("core host binding composition", CoreHostBindingTests.BindsCoreExternalModules),
    ("runtime diagnostics", RuntimeDiagnosticsTests.FormatsMachineState),
    ("module load diagnostics", RuntimeDiagnosticsTests.FormatsModuleLoadCode),
    ("runtime diagnostics validation", RuntimeDiagnosticsTests.ValidatesArgumentsAndPreservesDetails),
    ("disassembler and breakpoint", DeviceTests.DisassemblerAndBreakpoint),
    ("display and input mapping", DeviceTests.DisplayAndInputMapping),
    ("input mapping helpers", InputMappingTests.Run),
    ("Oberon input decoder", DeviceTests.OberonInputDecoder),
    ("Oberon kernel sectors", DeviceTests.OberonKernelSectors),
    ("Oberon files and riders", OberonPersistenceTests.OberonFilesAndRiders),
    ("Oberon file directory", OberonPersistenceTests.OberonFileDirectoryBTree),
    ("Oberon file directory boundaries", OberonPersistenceTests.OberonFileDirectoryBoundaries),
    ("Oberon persistent files", OberonPersistenceTests.OberonPersistentFiles),
    ("Oberon failed file allocation rollback", OberonPersistenceTests.RollsBackFailedFileAllocation),
    ("Oberon module loader", OberonModuleTests.LoaderGraph),
    ("shared module dependency reference counts", OberonModuleTests.SharedDependencyReferenceCounts),
    ("Oberon module memory image", OberonModuleTests.MemoryImage),
    ("Oberon module runtime validation", OberonModuleTests.ValidatesRuntimeArguments),
    ("Oberon module memory validation", OberonModuleMemoryValidationTests.ValidatesLoaderAddresses),
    ("Oberon module fixups", OberonModuleTests.Fixups),
    ("Oberon module image unload", OberonModuleTests.UnloadsMemoryImagesInRootOrder),
    ("Oberon disk format", OberonDiskFormatTests.Run),
    ("Oberon file system", OberonFileSystemTests.Run),
    ("Oberon text model", OberonTextTests.Run),
    ("Oberon compiler scanner", OberonCompilerScannerTests.Run),
    ("Oberon compiler symbols", OberonCompilerSymbolsTests.Run),
    ("Oberon compiler register allocator", OberonCompilerRegisterAllocatorTests.Run),
    ("Oberon compiler control flow analyzer", OberonCompilerControlFlowAnalyzerTests.Run),
    ("Oberon compiler symbol files", OberonCompilerSymbolFileTests.Run),
    ("Oberon compiler parser", OberonCompilerParserTests.Run),
    ("Oberon compiler code generator", OberonCompilerCodeGeneratorTests.Run),
    ("Oberon compiler expression emitter", OberonCompilerExpressionEmitterTests.Run),
    ("Oberon compiler module emitter", OberonCompilerModuleEmitterTests.Run),
    ("Oberon compiler RSC output", OberonCompilerRscTests.Run),
    ("Oberon compiler optimization baseline", OberonCompilerOptimizationBaselineTests.Run),
    ("compiler CLI options", CompilerCliOptionTests.Run),
    ("compiler diagnostics", CompilerDiagnosticTests.Run),
    ("CLI instruction trace", CliTraceTests.Run),
    ("Oberon fonts", OberonFontTests.Run),
    ("Oberon text frames", OberonTextFrameTests.Run),
    ("Oberon text frame layout validation", OberonTextFrameTests.ValidatesLayoutBounds),
    ("Oberon viewers", OberonViewerTests.Run),
    ("Oberon menu viewers", OberonMenuViewerTests.Run),
    ("Oberon text frame viewers", OberonTextFrameViewerTests.Run),
    ("Oberon text viewport controller", OberonTextViewportControllerTests.Run),
    ("Oberon messages", OberonMessageTests.Run),
    ("Oberon event loop", OberonEventLoopTests.Run),
    ("Oberon task scheduler", OberonTaskSchedulerTests.Run),
    ("Oberon text codec", OberonTextCodecTests.Run),
    ("Oberon text editor", OberonTextEditorTests.Run),
    ("Oberon file name rules", OberonFileNameTests.Run),
    ("RSC module parser", RscModuleTests.Run),
    ("Oberon symbol module parser", SymbolModuleTests.Run),
    ("Oberon symbol module binding", OberonModuleTests.SymbolBinding),
    ("CLI options", CliOptionTests.Run),
    ("SDL UI helpers", SdlUiTests.Run),
    ("subcommand options", SubcommandOptionTests.Run),
    ("architecture dependencies", ArchitectureTests.Run)
};

foreach (var (name, test) in tests)
{
    if (skipDiskTests && diskTests.Contains(name))
    {
        Console.WriteLine($"[skip] {name} (disk fixture not requested)");
        continue;
    }

    test();
    Console.WriteLine($"[ok] {name}");
}

Console.WriteLine("project-oberon-emulator-tests: all tests passed");
