var tests = new (string Name, Action Test)[]
{
    ("arithmetic and flags", CpuTests.ArithmeticAndFlags),
    ("integer signedness and subtract borrow", CpuTests.IntegerSignednessAndSubtractBorrow),
    ("branch and link", CpuTests.BranchAndLink),
    ("memory word and byte access", CpuTests.MemoryWordAndByteAccess),
    ("interrupt vector and RTI", CpuTests.InterruptVectorAndRti),
    ("MMIO devices", DeviceTests.MmioDevices),
    ("disk image read/write", DeviceTests.DiskImageReadWrite),
    ("SPI disk read/write", DeviceTests.SpiDiskReadWrite),
    ("machine timer", DeviceTests.MachineTimer),
    ("host call dispatcher", HostCallTests.DispatchesRegisteredHostCalls),
    ("external host binding", HostCallTests.BindsExternalEntryHandlers),
    ("Kernel host binding", KernelHostBindingTests.DispatchesClockAndSectorMapServices),
    ("Kernel host binding guards", KernelHostBindingTests.RejectsUnsupportedKernelProcedure),
    ("Kernel heap host binding", KernelHostBindingTests.DispatchesHeapAllocationService),
    ("Kernel sector I/O host binding", KernelHostBindingTests.DispatchesSectorIoServices),
    ("Files host binding", FilesHostBindingTests.DispatchesReadServices),
    ("runtime diagnostics", RuntimeDiagnosticsTests.FormatsMachineState),
    ("disassembler and breakpoint", DeviceTests.DisassemblerAndBreakpoint),
    ("display and input mapping", DeviceTests.DisplayAndInputMapping),
    ("Oberon input decoder", DeviceTests.OberonInputDecoder),
    ("Oberon kernel sectors", DeviceTests.OberonKernelSectors),
    ("Oberon files and riders", OberonPersistenceTests.OberonFilesAndRiders),
    ("Oberon file directory", OberonPersistenceTests.OberonFileDirectoryBTree),
    ("Oberon file directory boundaries", OberonPersistenceTests.OberonFileDirectoryBoundaries),
    ("Oberon persistent files", OberonPersistenceTests.OberonPersistentFiles),
    ("Oberon module loader", OberonModuleTests.LoaderGraph),
    ("Oberon module memory image", OberonModuleTests.MemoryImage),
    ("Oberon module fixups", OberonModuleTests.Fixups),
    ("Oberon disk format", OberonDiskFormatTests.Run),
    ("Oberon file system", OberonFileSystemTests.Run),
    ("Oberon text model", OberonTextTests.Run),
    ("Oberon fonts", OberonFontTests.Run),
    ("Oberon text frames", OberonTextFrameTests.Run),
    ("Oberon viewers", OberonViewerTests.Run),
    ("Oberon menu viewers", OberonMenuViewerTests.Run),
    ("Oberon text frame viewers", OberonTextFrameViewerTests.Run),
    ("Oberon text viewport controller", OberonTextViewportControllerTests.Run),
    ("Oberon messages", OberonMessageTests.Run),
    ("Oberon event loop", OberonEventLoopTests.Run),
    ("Oberon text codec", OberonTextCodecTests.Run),
    ("Oberon text editor", OberonTextEditorTests.Run),
    ("Oberon file name rules", OberonFileNameTests.Run),
    ("RSC module parser", RscModuleTests.Run),
    ("Oberon symbol module parser", SymbolModuleTests.Run),
    ("Oberon symbol module binding", OberonModuleTests.SymbolBinding),
    ("CLI options", CliOptionTests.Run),
    ("SDL UI helpers", SdlUiTests.Run),
    ("subcommand options", SubcommandOptionTests.Run)
};

foreach (var (name, test) in tests)
{
    test();
    Console.WriteLine($"[ok] {name}");
}

Console.WriteLine("project-oberon-emulator-tests: all tests passed");
