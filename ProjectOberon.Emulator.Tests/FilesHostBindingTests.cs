using System.Text;
using ProjectOberon.Emulator;

internal static class FilesHostBindingTests
{
    public static void DispatchesReadServices()
    {
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-files-host-{Guid.NewGuid():N}.img");
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                file.SetLength(((long)OberonKernel.FileSystemOffset + 512) * DiskImage.SectorSize);
            }

            using var disk = new DiskImage();
            disk.Open(path);
            using var machine = new Machine();
            var kernel = new OberonKernel(machine.Memory, disk);
            var files = new OberonFiles(kernel, initializeDirectory: true);
            files.Register(new OberonFile("Demo.rsc", [4, 3, 2, 1, (byte)'D', (byte)'e', (byte)'m', (byte)'o', 0], date: 42));

            var symbol = new OberonSymbolModule(
                "Files",
                1,
                1,
                [
                    new OberonSymbolExport("Old", 1, 0x20),
                    new OberonSymbolExport("Set", 1, 0x24),
                    new OberonSymbolExport("ReadInt", 1, 0x28),
                    new OberonSymbolExport("ReadString", 1, 0x2c),
                    new OberonSymbolExport("Pos", 1, 0x30),
                    new OberonSymbolExport("Length", 1, 0x34),
                    new OberonSymbolExport("Date", 1, 0x38),
                    new OberonSymbolExport("Base", 1, 0x3c)
                ]);
            var binding = OberonFilesHostBinding.Create(symbol, files, 0x3000, 0x5000);
            var loader = new OberonModuleLoader(_ => null);
            loader.RegisterExternal(symbol);
            var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, loader);
            runtime.BindExternal(binding, machine.HostCalls);
            WriteCString(machine.Memory, 0x500, "Demo.rsc");
            machine.Cpu.SetRegister(15, 0x200);

            machine.Cpu.SetRegister(0, 0x500);
            machine.Cpu.SetProgramCounter(0x5020);
            machine.Step();
            var fileHandle = machine.Cpu.Register(0);
            TestAssertions.True(fileHandle != 0);

            machine.Cpu.SetRegister(0, 0x600);
            machine.Cpu.SetRegister(1, fileHandle);
            machine.Cpu.SetRegister(2, 0);
            machine.Cpu.SetProgramCounter(0x5024);
            machine.Step();

            machine.Cpu.SetRegister(0, 0x600);
            machine.Cpu.SetRegister(1, 0x700);
            machine.Cpu.SetProgramCounter(0x5028);
            machine.Step();
            TestAssertions.Equal(0x01020304u, machine.Memory.Read32(0x700));

            machine.Cpu.SetRegister(0, 0x600);
            machine.Cpu.SetRegister(1, 0x800);
            machine.Cpu.SetProgramCounter(0x502c);
            machine.Step();
            TestAssertions.Equal("Demo", ReadCString(machine.Memory, 0x800));

            machine.Cpu.SetRegister(0, 0x600);
            machine.Cpu.SetProgramCounter(0x5030);
            machine.Step();
            TestAssertions.Equal(9u, machine.Cpu.Register(0));

            machine.Cpu.SetRegister(0, fileHandle);
            machine.Cpu.SetProgramCounter(0x5034);
            machine.Step();
            TestAssertions.Equal(9u, machine.Cpu.Register(0));

            machine.Cpu.SetRegister(0, fileHandle);
            machine.Cpu.SetProgramCounter(0x5038);
            machine.Step();
            TestAssertions.Equal(42u, machine.Cpu.Register(0));

            machine.Cpu.SetRegister(0, 0x600);
            machine.Cpu.SetProgramCounter(0x503c);
            machine.Step();
            TestAssertions.Equal(fileHandle, machine.Cpu.Register(0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static void DispatchesWriteAndLifecycleServices()
    {
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-files-write-{Guid.NewGuid():N}.img");
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                file.SetLength(((long)OberonKernel.FileSystemOffset + 512) * DiskImage.SectorSize);
            }

            using var disk = new DiskImage();
            disk.Open(path);
            using var machine = new Machine();
            var kernel = new OberonKernel(machine.Memory, disk);
            var files = new OberonFiles(kernel, initializeDirectory: true);
            var symbol = new OberonSymbolModule(
                "Files",
                1,
                1,
                [
                    new OberonSymbolExport("New", 1, 0x20),
                    new OberonSymbolExport("Set", 1, 0x24),
                    new OberonSymbolExport("WriteInt", 1, 0x28),
                    new OberonSymbolExport("WriteString", 1, 0x2c),
                    new OberonSymbolExport("Register", 1, 0x30),
                    new OberonSymbolExport("Length", 1, 0x34),
                    new OberonSymbolExport("Rename", 1, 0x38),
                    new OberonSymbolExport("Delete", 1, 0x3c)
                ]);
            var binding = OberonFilesHostBinding.Create(symbol, files, 0x3000, 0x5000);
            var loader = new OberonModuleLoader(_ => null);
            loader.RegisterExternal(symbol);
            var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, loader);
            runtime.BindExternal(binding, machine.HostCalls);
            WriteCString(machine.Memory, 0x500, "Created.rsc");
            WriteCString(machine.Memory, 0x700, "Hello");
            WriteCString(machine.Memory, 0x800, "Created.rsc");
            WriteCString(machine.Memory, 0x900, "Renamed.rsc");
            machine.Cpu.SetRegister(15, 0x200);

            machine.Cpu.SetRegister(0, 0x500);
            machine.Cpu.SetProgramCounter(0x5020);
            machine.Step();
            var fileHandle = machine.Cpu.Register(0);
            TestAssertions.True(fileHandle != 0);

            machine.Cpu.SetRegister(0, 0x600);
            machine.Cpu.SetRegister(1, fileHandle);
            machine.Cpu.SetRegister(2, 0);
            machine.Cpu.SetProgramCounter(0x5024);
            machine.Step();

            machine.Cpu.SetRegister(0, 0x600);
            machine.Cpu.SetRegister(1, 0x1122_3344);
            machine.Cpu.SetProgramCounter(0x5028);
            machine.Step();
            machine.Cpu.SetRegister(0, 0x600);
            machine.Cpu.SetRegister(1, 0x700);
            machine.Cpu.SetProgramCounter(0x502c);
            machine.Step();

            machine.Cpu.SetRegister(0, fileHandle);
            machine.Cpu.SetProgramCounter(0x5030);
            machine.Step();

            machine.Cpu.SetRegister(0, fileHandle);
            machine.Cpu.SetProgramCounter(0x5034);
            machine.Step();
            TestAssertions.Equal(10u, machine.Cpu.Register(0));

            machine.Cpu.SetRegister(0, 0x800);
            machine.Cpu.SetRegister(1, 0x900);
            machine.Cpu.SetRegister(2, 0xa00);
            machine.Cpu.SetProgramCounter(0x5038);
            machine.Step();
            TestAssertions.Equal(0u, machine.Memory.Read32(0xa00));
            TestAssertions.True(files.Old("Renamed.rsc") is not null);

            machine.Cpu.SetRegister(0, 0x900);
            machine.Cpu.SetRegister(1, 0xa04);
            machine.Cpu.SetProgramCounter(0x503c);
            machine.Step();
            TestAssertions.Equal(0u, machine.Memory.Read32(0xa04));
            TestAssertions.True(files.Old("Renamed.rsc") is null);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WriteCString(Memory memory, uint address, string value)
    {
        var bytes = Encoding.Latin1.GetBytes(value);
        for (var index = 0; index < bytes.Length; index++) memory.Write8(address + (uint)index, bytes[index]);
        memory.Write8(address + (uint)bytes.Length, 0);
    }

    private static string ReadCString(Memory memory, uint address)
    {
        var bytes = new List<byte>();
        for (var index = 0; index < 256; index++)
        {
            var value = memory.Read8(address + (uint)index);
            if (value == 0) return Encoding.Latin1.GetString(bytes.ToArray());
            bytes.Add(value);
        }

        throw new InvalidOperationException("test string was not terminated");
    }
}
