using System.Text;

namespace ProjectOberon.Emulator;

public static class OberonRuntimeDiagnostics
{
    public static string Format(string applicationName, Machine? machine, Exception error)
    {
        ArgumentException.ThrowIfNullOrEmpty(applicationName);
        ArgumentNullException.ThrowIfNull(error);

        var output = new StringBuilder();
        output.AppendLine($"{applicationName}: unhandled runtime error");
        output.AppendLine($"exception: {error.GetType().FullName}: {error.Message}");
        if (machine is not null)
        {
            var cpu = machine.Cpu;
            output.AppendLine($"cpu: steps={cpu.Steps} pc=0x{cpu.PcByte:x6} high=0x{cpu.High:x8}");
            output.AppendLine($"registers: r0=0x{cpu.Register(0):x8} r1=0x{cpu.Register(1):x8} "
                + $"r12=0x{cpu.Register(12):x8} r14=0x{cpu.Register(14):x8} r15=0x{cpu.Register(15):x8}");
            output.AppendLine($"flags: N={cpu.Negative} Z={cpu.Zero} C={cpu.Carry} V={cpu.Overflow}");
        }

        output.AppendLine("exception details:");
        output.AppendLine(error.ToString());
        return output.ToString().TrimEnd();
    }
}
