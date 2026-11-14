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
        if (error is OberonModuleLoadException moduleError)
        {
            output.AppendLine($"module-load: code={moduleError.Code}");
        }

        if (machine is not null)
        {
            var cpu = machine.Cpu;
            output.AppendLine($"cpu: steps={cpu.Steps} pc=0x{cpu.PcByte:x6} high=0x{cpu.High:x8}");
            output.AppendLine($"last: pc=0x{cpu.LastPcByte:x6} instruction=0x{cpu.LastInstruction:x8} "
                + Risc5Disassembler.Format(cpu.LastInstruction));
            var registers = new StringBuilder("registers:");
            for (var index = 0; index < 16; index++)
            {
                registers.Append($" r{index}=0x{cpu.Register(index):x8}");
            }
            output.AppendLine(registers.ToString());
            output.AppendLine($"flags: N={cpu.Negative} Z={cpu.Zero} C={cpu.Carry} V={cpu.Overflow}");
        }

        output.AppendLine("exception details:");
        output.AppendLine(error.ToString());
        return output.ToString().TrimEnd();
    }
}
