namespace ProjectOberon.Emulator;

public readonly record struct MachineTraceEntry(ulong Step, uint ProgramCounter, uint Instruction);

public sealed class MachineDebugger
{
    private readonly HashSet<uint> _breakpoints = [];
    private readonly Queue<MachineTraceEntry> _trace = [];
    private int _traceCapacity;

    public MachineDebugger(Machine machine)
    {
        Machine = machine ?? throw new ArgumentNullException(nameof(machine));
    }

    public Machine Machine { get; }
    public IReadOnlySet<uint> Breakpoints => _breakpoints;
    public IReadOnlyCollection<MachineTraceEntry> Trace => _trace;
    public bool IsAtBreakpoint => _breakpoints.Contains(Machine.Cpu.PcByte);

    public void AddBreakpoint(uint pcByte) => _breakpoints.Add(pcByte & Memory.AddressMask);
    public bool RemoveBreakpoint(uint pcByte) => _breakpoints.Remove(pcByte & Memory.AddressMask);

    public void EnableTrace(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _traceCapacity = capacity;
        _trace.Clear();
    }

    public void ClearTrace() => _trace.Clear();

    public bool Step()
    {
        if (IsAtBreakpoint)
        {
            return false;
        }

        var entry = new MachineTraceEntry(
            Machine.Cpu.Steps,
            Machine.Cpu.PcByte,
            Machine.Memory.ReadInstruction(Machine.Cpu.PcByte));
        try
        {
            Machine.Step();
        }
        finally
        {
            AppendTrace(entry);
        }

        return true;
    }

    public ulong Run(ulong maxSteps)
    {
        if (_breakpoints.Count == 0 && _traceCapacity == 0)
        {
            Machine.Run(maxSteps);

            return maxSteps;
        }

        ulong executed = 0;
        while (executed < maxSteps && Step())
        {
            executed++;
        }

        return executed;
    }

    private void AppendTrace(MachineTraceEntry entry)
    {
        if (_traceCapacity == 0)
        {
            return;
        }

        if (_trace.Count == _traceCapacity)
        {
            _trace.Dequeue();
        }

        _trace.Enqueue(entry);
    }
}
