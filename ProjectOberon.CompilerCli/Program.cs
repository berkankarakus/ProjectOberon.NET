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
        foreach (var diagnostic in result.Diagnostics)
        {
            Console.Error.WriteLine($"{options.SourcePath}:{diagnostic.Line}:{diagnostic.Column}: error: {diagnostic.Message}");
        }

        Environment.ExitCode = 1;
        return;
    }

    var generator = new OberonCompilerCodeGenerator();
    var emitter = new OberonCompilerModuleEmitter(generator);
    var code = emitter.Emit(result);

    Console.WriteLine($"module {result.ModuleName}");
    Console.WriteLine($"instructions: {code.Count}");
    Console.WriteLine($"module data: {emitter.ModuleData.Count} bytes");

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
catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine($"project-oberon-compiler: {error.Message}");
    Environment.ExitCode = 1;
}

internal sealed record CompilerOptions(string SourcePath, bool Disassemble, bool Help)
{
    public static CompilerOptions Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CompilerOptions(string.Empty, true, true);
        }

        string? source = null;
        var disassemble = true;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--help":
                case "-h":
                    return new CompilerOptions(string.Empty, true, true);
                case "--source":
                    source = ReadValue(args, ref index, "--source");
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

        return new CompilerOptions(source, disassemble, false);
    }

    public static void PrintUsage()
    {
        Console.WriteLine("usage: project-oberon-compiler SOURCE.Mod [--no-disassemble]");
        Console.WriteLine("       project-oberon-compiler --source SOURCE.Mod [--no-disassemble]");
        Console.WriteLine("compiles an Oberon module and prints its RISC5 instruction listing");
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
