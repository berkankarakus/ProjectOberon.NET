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

    private static void RejectsCodeAndEntryCountsLargerThanThePayload()
    {
        var code = BuildRsc("Demo", 1, [], "Start", codeWordCount: int.MaxValue);
        var entries = BuildRsc("Demo", 1, [], "Start", entryCount: int.MaxValue);

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(code));
        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(entries));
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

    private static void RejectsNegativeSize()
    {
        var data = BuildRsc("Demo", 1, [], "Start", size: -1);
        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(data));
    }

    private static void RejectsDuplicateNames()
    {
        var duplicateImports = BuildRsc("Demo", 1, [("Files", 1), ("Files", 2)], "Start");
        var duplicateCommands = BuildRsc("Demo", 1, [], "Start", additionalCommands: ["Start"]);

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(duplicateImports));
        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(duplicateCommands));
    }

    private static void RejectsInvalidEntriesAndFixups()
    {
        var invalidEntry = BuildRsc("Demo", 1, [], "Start", entries: [8]);
        var invalidProgramFixup = BuildRsc("Demo", 1, [], "Start", fixupProgram: 1);
        var invalidTypeFixup = BuildRsc("Demo", 1, [], "Start", typePayload: [0, 0, 0, 0], fixupTypes: 1);
        var overflowingTypeFixup = BuildRsc("Demo", 1, [], "Start", typePayload: [0, 0, 0, 0], fixupTypes: int.MaxValue);

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(invalidEntry));
        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(invalidProgramFixup));
        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(invalidTypeFixup));
        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(overflowingTypeFixup));
    }

    private static void RejectsInvalidPointerReferences()
    {
        var negative = BuildRsc("Demo", 1, [], "Start", variableBytes: 4, pointerReferences: [-1]);
        var outside = BuildRsc("Demo", 1, [], "Start", variableBytes: 4, pointerReferences: [4]);

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(negative));
        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(outside));
    }

    private static void RejectsNonAsciiStrings()
    {
        var data = BuildRsc("Demo", 1, [], "Start");
        data[0] = 0xff;

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(data));
    }

    private static void RejectsTrailingBytes()
    {
        var data = BuildRsc("Demo", 1, [], "Start");
        Array.Resize(ref data, data.Length + 1);

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(data));
    }

    private static void RejectsUnalignedDataSections()
    {
        var data = BuildRsc("Demo", 1, [], "Start", variableBytes: 1);

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(data));
    }

    private static void RejectsLongModuleNames()
    {
        var data = BuildRsc(new string('D', 32), 1, [], "Start");

        TestAssertions.ThrowsInvalidData(() => RscModuleReader.Parse(data));
    }
}
