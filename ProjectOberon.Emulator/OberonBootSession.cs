using System.Text;

namespace ProjectOberon.Emulator;

public sealed class OberonBootSession
{
    private const uint ModuleNameAddress = 0x600;
    private const uint DescriptorAddress = 0x700;
    private const int ModuleNameLength = 32;

    private OberonBootSession(
        Machine machine,
        OberonModuleLoader modules,
        OberonModuleRuntime runtime)
    {
        Machine = machine;
        Modules = modules;
        Runtime = runtime;
    }

    public Machine Machine { get; }
    public OberonModuleLoader Modules { get; }
    public OberonModuleRuntime Runtime { get; }

    public static OberonBootSession Create(Machine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        var disk = machine.Disk ?? throw new InvalidOperationException(
            "an open disk is required to create an Oberon boot session");
        var files = new OberonFiles(new OberonFileSystem(disk));
        var modules = new OberonModuleLoader(files, ["Kernel", "Files", "Modules"]);
        var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, modules);
        OberonCoreHostBinding.Bind(modules, runtime,
            new OberonKernel(machine.Memory, disk), files, machine.HostCalls);
        return new OberonBootSession(machine, modules, runtime);
    }

    public static OberonBootSession Create(
        Machine machine,
        OberonModuleLoader modules,
        OberonModuleRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(runtime);
        return new OberonBootSession(machine, modules, runtime);
    }

    public uint Initialize(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!Modules.TryGetExternalSymbol("Modules", out var symbol))
        {
            throw new OberonModuleLoadException(5, "Modules symbol module is not available");
        }

        var load = symbol.Exports.FirstOrDefault(export =>
            export.Name == "Load" && (export.IsProcedure ?? export.Class == 1));
        if (load is null)
        {
            throw new OberonModuleLoadException(5, "Modules.Load export is not available");
        }

        WriteCString(ModuleNameAddress, name);
        Machine.Cpu.SetRegister(0, ModuleNameAddress);
        Machine.Cpu.SetRegister(1, DescriptorAddress);
        Machine.Cpu.SetRegister(15, Memory.RomBase);
        Machine.Cpu.SetProgramCounter(
            OberonCoreHostBinding.ModulesCodeAddress + checked((uint)load.Value * 4));
        Machine.Step();

        var result = unchecked((int)Machine.Memory.Read32(
            OberonCoreHostBinding.ModulesDataAddress + 20));
        if (result != 0)
        {
            var importing = ReadCString(OberonCoreHostBinding.ModulesDataAddress + 24);
            var imported = ReadCString(OberonCoreHostBinding.ModulesDataAddress + 56);
            var detail = imported.Length == 0 ? importing : $"{importing} imports {imported}";
            var reason = result == 7 ? "initializer exceeded the step budget" : $"error code {result}";
            throw new OberonModuleLoadException(result,
                $"Modules.Load failed for {name}: {detail}; {reason}");
        }

        return Machine.Memory.Read32(DescriptorAddress);
    }

    public OberonModuleImage Start(string name, string? command = null, uint? stackPointer = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return command is null
            ? Runtime.Start(name, stackPointer)
            : Runtime.StartCommand(name, command, stackPointer);
    }

    private void WriteCString(uint address, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        if (bytes.Length >= ModuleNameLength)
        {
            throw new ArgumentException(
                $"Oberon module name must be shorter than {ModuleNameLength} characters",
                nameof(value));
        }

        for (var index = 0; index < bytes.Length; index++)
        {
            Machine.Memory.Write8(address + checked((uint)index), bytes[index]);
        }

        Machine.Memory.Write8(address + checked((uint)bytes.Length), 0);
    }

    private string ReadCString(uint address)
    {
        var bytes = new List<byte>();
        for (var index = 0; index < ModuleNameLength; index++)
        {
            var value = Machine.Memory.Read8(address + checked((uint)index));
            if (value == 0)
            {
                return Encoding.ASCII.GetString(bytes.ToArray());
            }

            bytes.Add(value);
        }

        return Encoding.ASCII.GetString(bytes.ToArray());
    }
}
