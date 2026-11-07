namespace ProjectOberon.Emulator;

public sealed class Machine : IDisposable
{
    public Machine(int ramBytes = 1 << 20)
    {
        Memory = new Memory(ramBytes);
        Cpu = new Risc5Cpu(Memory);
    }

    public Memory Memory { get; }
    public Risc5Cpu Cpu { get; }
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
        Cpu.Step(irq || _timerPhase == TimerInterruptPeriod - 1);
        _timerPhase = (_timerPhase + 1) % TimerInterruptPeriod;
        Memory.AdvanceTime();
    }

    public void Run(ulong steps)
    {
        for (ulong index = 0; index < steps; index++) Step();
    }

    public void Dispose() => CloseDisk();
}
