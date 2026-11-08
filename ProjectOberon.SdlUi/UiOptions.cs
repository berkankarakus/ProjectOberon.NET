using System.Globalization;

namespace ProjectOberon.SdlUi;

internal sealed record UiOptions(
    string? Rom,
    string? Disk,
    bool DiskWritable,
    ulong StepsPerFrame,
    double Scale,
    bool FlipY,
    string? Editor,
    string? Font,
    string? StartModule,
    bool Initialize,
    bool Boot,
    string? Command,
    bool Help)
{
    public static UiOptions Parse(string[] args)
    {
        string? rom = null, disk = null;
        var writable = true;
        var steps = 100_000UL;
        var scale = 1.25;
        var flipY = true;
        string? editor = null, font = null, startModule = null, command = null;
        var initialize = false;
        var boot = false;
        var help = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--help":
                case "-h":
                    help = true;
                    break;
                case "--rom" when index + 1 < args.Length:
                    rom = args[++index];
                    break;
                case "--disk" when index + 1 < args.Length:
                    disk = args[++index];
                    break;
                case "--read-only":
                    writable = false;
                    break;
                case "--steps-per-frame" when index + 1 < args.Length:
                    if (!ulong.TryParse(args[++index], out steps))
                        throw new ArgumentException("--steps-per-frame must be an unsigned integer");
                    break;
                case "--scale" when index + 1 < args.Length:
                    if (!double.TryParse(args[++index], NumberStyles.Float, CultureInfo.InvariantCulture, out scale) || scale < 0.5 || scale > 4.0)
                        throw new ArgumentException("--scale must be between 0.5 and 4.0");
                    break;
                case "--no-flip-y":
                    flipY = false;
                    break;
                case "--editor" when index + 1 < args.Length:
                    editor = args[++index];
                    break;
                case "--font" when index + 1 < args.Length:
                    font = args[++index];
                    break;
                case "--start-module" when index + 1 < args.Length:
                    startModule = args[++index];
                    break;
                case "--initialize":
                    initialize = true;
                    break;
                case "--boot":
                    boot = true;
                    break;
                case "--command" when index + 1 < args.Length:
                    command = args[++index];
                    break;
                default:
                    throw new ArgumentException($"unknown or incomplete command-line option: {args[index]}");
            }
        }
        if (!writable && disk is null)
            throw new ArgumentException("--read-only requires --disk FILE");
        if (font is not null && editor is null)
            throw new ArgumentException("--font requires --editor FILE");
        if (startModule is not null && disk is null)
            throw new ArgumentException("--start-module requires --disk FILE");
        if (initialize && startModule is null)
            throw new ArgumentException("--initialize requires --start-module NAME");
        if (boot && startModule is null)
            throw new ArgumentException("--boot requires --start-module NAME");
        if (initialize && boot)
            throw new ArgumentException("--initialize and --boot cannot be used together");
        if (command is not null && startModule is null)
            throw new ArgumentException("--command requires --start-module NAME");
        return new UiOptions(rom, disk, writable, steps, scale, flipY, editor, font,
            startModule, initialize, boot, command, help);
    }

    public static void PrintUsage() => Console.Error.WriteLine(
        "usage: project-oberon-ui [--rom FILE] [--disk FILE] [--read-only] [--steps-per-frame N] [--scale N] [--no-flip-y] [--editor FILE --font FILE] [--start-module NAME [--initialize|--boot] [--command NAME]]");
}
