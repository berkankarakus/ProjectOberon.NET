using ProjectOberon.Emulator;

try
{
    var options = CompilerOptions.Parse(args);
    if (options.Help)
    {
        CompilerOptions.PrintUsage();
        return;
    }

    var source = File.ReadAllText(options.SourcePath);
    var result = new OberonCompilerParser(source).ParseModule();
    if (!result.Succeeded)
    {
        Console.Error.WriteLine(OberonCompilerDiagnostics.Format(options.SourcePath, result.Diagnostics));

        Environment.ExitCode = 1;
        return;
    }

    var generator = new OberonCompilerCodeGenerator();
    var emitter = new OberonCompilerModuleEmitter(generator);
    var rsc = options.RscPath is null ? null : emitter.EmitRsc(result);
    var code = rsc?.Code ?? emitter.Emit(result);

    Console.WriteLine($"module {result.ModuleName}");
    Console.WriteLine($"instructions: {code.Count}");
    Console.WriteLine($"module data: {emitter.ModuleData.Count} bytes");

    if (rsc is not null)
    {
        File.WriteAllBytes(options.RscPath!, RscModuleWriter.Write(rsc));
        Console.WriteLine($"rsc: {options.RscPath}");
    }

    if (options.SymbolPath is not null)
    {
        var exports = result.ModuleScope!.Objects
            .Where(symbol => symbol.Exported)
            .Where(symbol => symbol.Class is
                OberonCompilerObjectClass.Constant or
                OberonCompilerObjectClass.Variable or
                OberonCompilerObjectClass.Type or
                OberonCompilerObjectClass.Procedure or
                OberonCompilerObjectClass.Function)
            .ToArray();
        File.WriteAllBytes(options.SymbolPath,
            OberonCompilerSymbolFileWriter.Write(result.ModuleName!, exports));
        Console.WriteLine($"symbols: {options.SymbolPath}");
    }

    if (!options.Disassemble)
    {
        return;
    }

    for (var index = 0; index < code.Count; index++)
    {
        var instruction = code[index];
        Console.WriteLine($"{index * 4:x6}: {instruction:x8} {Risc5Disassembler.Format(instruction)}");
    }
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
    or InvalidOperationException or NotSupportedException)
{
    Console.Error.WriteLine($"project-oberon-compiler: {error.Message}");
    Environment.ExitCode = 1;
}

internal sealed record CompilerOptions(
    string SourcePath,
    string? SymbolPath,
    string? RscPath,
    bool Disassemble,
    bool Help)
{
    public static CompilerOptions Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CompilerOptions(string.Empty, null, null, true, true);
        }

        string? source = null;
        string? symbolPath = null;
        string? rscPath = null;
        var disassemble = true;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--help":
                case "-h":
                    return new CompilerOptions(string.Empty, null, null, true, true);
                case "--source":
                    source = ReadValue(args, ref index, "--source");
                    break;
                case "--symbols":
                    symbolPath = ReadValue(args, ref index, "--symbols");
                    break;
                case "--rsc":
                    rscPath = ReadValue(args, ref index, "--rsc");
                    break;
                case "--no-disassemble":
                    disassemble = false;
                    break;
                default:
                    if (args[index].StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"unknown option: {args[index]}");
                    }

                    source ??= args[index];
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("source file is required");
        }

        if (symbolPath is not null && rscPath is not null
            && string.Equals(Path.GetFullPath(symbolPath), Path.GetFullPath(rscPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new ArgumentException("--symbols and --rsc must point to different files");
        }

        return new CompilerOptions(source, symbolPath, rscPath, disassemble, false);
    }

    public static void PrintUsage()
    {
        Console.WriteLine("usage: project-oberon-compiler SOURCE.Mod [--symbols FILE] [--rsc FILE] [--no-disassemble]");
        Console.WriteLine("       project-oberon-compiler --source SOURCE.Mod [--symbols FILE] [--rsc FILE] [--no-disassemble]");
        Console.WriteLine("compiles an Oberon module, prints its RISC5 listing, and optionally writes SMB/RSC files");
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (++index >= args.Length || args[index].StartsWith("-", StringComparison.Ordinal))
        {
            throw new ArgumentException($"value expected after {option}");
        }

        return args[index];
    }
}
