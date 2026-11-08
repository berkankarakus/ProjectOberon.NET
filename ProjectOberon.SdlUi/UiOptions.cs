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
    bool Help)
{
    public static UiOptions Parse(string[] args)
    {
        string? rom = null, disk = null;
        var writable = true;
        var steps = 100_000UL;
        var scale = 1.25;
        var flipY = true;
        string? editor = null, font = null;
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
                default:
                    throw new ArgumentException($"unknown or incomplete command-line option: {args[index]}");
            }
        }
        if (!writable && disk is null)
            throw new ArgumentException("--read-only requires --disk FILE");
        if (font is not null && editor is null)
            throw new ArgumentException("--font requires --editor FILE");
        return new UiOptions(rom, disk, writable, steps, scale, flipY, editor, font, help);
    }

    public static void PrintUsage() => Console.Error.WriteLine("usage: project-oberon-ui [--rom FILE] [--disk FILE] [--read-only] [--steps-per-frame N] [--scale N] [--no-flip-y] [--editor FILE --font FILE]");
}
