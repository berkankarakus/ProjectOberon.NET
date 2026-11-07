namespace ProjectOberon.Emulator;

public sealed class MachineDebugger
{
    private readonly HashSet<uint> _breakpoints = [];

    public MachineDebugger(Machine machine) => Machine = machine ?? throw new ArgumentNullException(nameof(machine));

    public Machine Machine { get; }
    public IReadOnlySet<uint> Breakpoints => _breakpoints;
    public bool IsAtBreakpoint => _breakpoints.Contains(Machine.Cpu.PcByte);

    public void AddBreakpoint(uint pcByte) => _breakpoints.Add(pcByte & Memory.AddressMask);
    public bool RemoveBreakpoint(uint pcByte) => _breakpoints.Remove(pcByte & Memory.AddressMask);

    public bool Step()
    {
        if (IsAtBreakpoint) return false;
        Machine.Step();
        return true;
    }

    public ulong Run(ulong maxSteps)
    {
        ulong executed = 0;
        while (executed < maxSteps && Step()) executed++;
        return executed;
    }
}
