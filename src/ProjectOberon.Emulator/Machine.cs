namespace ProjectOberon.Emulator;

public readonly record struct MachineRunProgress(ulong ExecutedSteps, uint ProgramCounter);

public sealed class Machine : IDisposable
{
    public Machine(int ramBytes = 1 << 20)
    {
        Memory = new Memory(ramBytes);
        Cpu = new Risc5Cpu(Memory);
    }

    public Memory Memory { get; }
    public Risc5Cpu Cpu { get; }
    public OberonHostCallDispatcher HostCalls { get; } = new();
    public DiskImage? Disk => _disk;
    public const ulong TimerInterruptPeriod = 1000;
    private DiskImage? _disk;
    private ulong _timerPhase;

    public void OpenDisk(string path, bool writable = true)
    {
        CloseDisk();
        _disk = new DiskImage();
        _disk.Open(path, writable);
        Memory.AttachDisk(_disk);
    }

    public void CloseDisk()
    {
        Memory.DetachDisk();
        _disk?.Dispose();
        _disk = null;
    }

    public void Step(bool irq = false)
    {
        if (!HostCalls.TryDispatch(Memory, Cpu))
        {
            Cpu.Step(irq || _timerPhase == TimerInterruptPeriod - 1);
        }

        _timerPhase++;
        if (_timerPhase == TimerInterruptPeriod)
        {
            _timerPhase = 0;
        }

        Memory.AdvanceTime();
    }

    public void Run(ulong steps)
    {
        Run(steps, progress: null);
    }

    public void Run(
        ulong steps,
        Action<MachineRunProgress>? progress,
        ulong progressInterval = 100_000)
    {
        if (progress is not null && progressInterval == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(progressInterval));
        }

        for (ulong index = 0; index < steps; index++)
        {
            Step();
            var executedSteps = index + 1;
            if (progress is not null
                && (executedSteps % progressInterval == 0 || executedSteps == steps))
            {
                progress(new MachineRunProgress(executedSteps, Cpu.PcByte));
            }
        }
    }

    public void Dispose() => CloseDisk();
}
