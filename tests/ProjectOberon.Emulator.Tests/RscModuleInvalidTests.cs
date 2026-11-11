using ProjectOberon.Emulator;

internal static partial class RscModuleTests
{
    private static void RejectsUnsupportedVersion()
    {
        var data = BuildRsc("Demo", 1, [], "Start");
        data["Demo".Length + 1 + sizeof(int)] = 2;

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(data));
    }

    private static void RejectsTruncatedModule()
    {
        var data = BuildRsc("Demo", 1, [], "Start");

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(data[..^1]));
    }

    private static void RejectsNegativeVariableSpace()
    {
        var data = BuildRsc("Demo", 1, [], "Start", variableBytes: -1);

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(data));
    }

    private static void RejectsNegativeStringSpace()
    {
        var data = BuildRsc("Demo", 1, [], "Start", stringBytes: -1);

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(data));
    }

    private static void RejectsNegativeCodeWordCount()
    {
        var data = BuildRsc("Demo", 1, [], "Start", codeWordCount: -1);

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(data));
    }

    private static void RejectsNegativeEntryCount()
    {
        var data = BuildRsc("Demo", 1, [], "Start", entryCount: -1);

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(data));
    }

    private static void RejectsInvalidTrailer()
    {
        var data = BuildRsc("Demo", 1, [], "Start");
        data[^1] = (byte)'X';

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(data));
    }

    private static void RejectsInvalidCodeLayout()
    {
        var invalidCommand = BuildRsc("Demo", 1, [], "Start", commandOffset: 4);
        var invalidBody = BuildRsc("Demo", 1, [], "Start", bodyOffset: 2);

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(invalidCommand));
        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(invalidBody));
    }
}
