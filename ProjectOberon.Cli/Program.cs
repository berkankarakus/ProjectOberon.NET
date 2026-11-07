using ProjectOberon.Emulator;

try
{
    var options = Options.Parse(args);
    if (options.Help)
    {
        Options.PrintUsage();
        return;
    }

    using var machine = new Machine();
    if (options.Rom is not null) machine.Memory.LoadRomFile(options.Rom);
    if (options.Disk is not null) machine.OpenDisk(options.Disk, options.DiskWritable);
    var debugger = new MachineDebugger(machine);
    if (options.Breakpoint is uint breakpoint) debugger.AddBreakpoint(breakpoint);
    if (options.Trace is int traceSteps)
    {
        for (ulong index = 0; index < options.Steps; index++)
        {
            if (index < (ulong)traceSteps)
            {
                var raw = machine.Memory.ReadInstruction(machine.Cpu.PcByte);
                Console.WriteLine($"{index} pc={machine.Cpu.PcByte:x6} ir={raw:x8} {Risc5Disassembler.Format(raw)} r14={machine.Cpu.Register(14):x8} r15={machine.Cpu.Register(15):x8}");
            }
            if (!debugger.Step()) break;
        }
    }
    else debugger.Run(options.Steps);

    if (debugger.IsAtBreakpoint) Console.Error.WriteLine($"breakpoint reached at pc=0x{machine.Cpu.PcByte:x6}");

    var cpu = machine.Cpu;
    Console.WriteLine($"steps={cpu.Steps} pc=0x{cpu.PcByte:x6} r14=0x{cpu.Register(14):x8} r15=0x{cpu.Register(15):x8}");
}
catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or OverflowException)
{
    Console.Error.WriteLine($"project-oberon: {error.Message}");
    Environment.ExitCode = 1;
}

internal sealed record Options(string? Rom, string? Disk, bool DiskWritable, ulong Steps, bool Help, int? Trace, uint? Breakpoint)
{
    public static Options Parse(string[] args)
    {
        string? rom = null;
        string? disk = null;
        var diskWritable = true;
        ulong steps = 1000;
        var help = false;
        int? trace = null;
        uint? breakpoint = null;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--help":
                case "-h":
                    help = true;
                    break;
                case "--rom":
                    rom = Next(args, ref index, "--rom");
                    break;
                case "--steps":
                    if (!ulong.TryParse(Next(args, ref index, "--steps"), out steps))
                        throw new ArgumentException("--steps must be an unsigned integer");
                    break;
                case "--disk":
                    disk = Next(args, ref index, "--disk");
                    break;
                case "--read-only":
                    diskWritable = false;
                    break;
                case "--trace":
                    if (!int.TryParse(Next(args, ref index, "--trace"), out var traceValue) || traceValue < 0)
                        throw new ArgumentException("--trace must be a non-negative integer");
                    trace = traceValue;
                    break;
                case "--breakpoint":
                    var breakpointText = Next(args, ref index, "--breakpoint").Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
                    if (!uint.TryParse(breakpointText, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var breakpointValue))
                        throw new ArgumentException("--breakpoint must be a hexadecimal PC address");
                    breakpoint = breakpointValue;
                    break;
                default:
                    throw new ArgumentException($"unknown or incomplete command-line option: {args[index]}");
            }
        }
        if (!diskWritable && disk is null) throw new ArgumentException("--read-only requires --disk FILE");
        return new Options(rom, disk, diskWritable, steps, help, trace, breakpoint);
    }

    public static void PrintUsage() => Console.Error.WriteLine("usage: project-oberon [--rom FILE] [--disk FILE] [--read-only] [--steps N] [--trace N] [--breakpoint HEX-PC]");

    private static string Next(string[] args, ref int index, string option)
    {
        if (++index >= args.Length) throw new ArgumentException($"unknown or incomplete command-line option: {option}");
        return args[index];
    }
}
