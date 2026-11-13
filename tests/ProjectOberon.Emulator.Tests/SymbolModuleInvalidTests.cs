using ProjectOberon.Emulator;

internal static partial class SymbolModuleTests
{
    private static void RejectsUnsupportedVersion()
    {
        var data = BuildSmb(_ => { });
        data[4 + sizeof(int) + "Kernel".Length + 1] = 2;

        TestAssertions.ThrowsInvalidData(() => OberonSymbolModuleReader.Parse(data));
    }

    private static void RejectsTruncatedModule()
    {
        var data = BuildSmb(writer =>
        {
            writer.AddByte(2);
            writer.AddString("heapOrg");
            writer.AddByte(0xfc);
            writer.AddByte(3);
            writer.AddByte(0);
        });

        TestAssertions.ThrowsInvalidData(() => OberonSymbolModuleReader.Parse(data[..^2]));
    }

    private static void RejectsInvalidTypeForm()
    {
        var data = BuildSmb(writer =>
        {
            writer.AddByte(1);
            writer.AddString("Invalid");
            writer.AddByte(0);
            writer.AddByte(99);
        });

        TestAssertions.ThrowsInvalidData(() => OberonSymbolModuleReader.Parse(data));
    }

    private static void RejectsUnknownTypeReference()
    {
        var data = BuildSmb(writer =>
        {
            writer.AddByte(5);
            writer.AddString("InvalidReference");
            writer.AddByte(0xf2);
            writer.AddString(string.Empty);
            writer.AddByte(0);
        });

        TestAssertions.ThrowsInvalidData(() => OberonSymbolModuleReader.Parse(data));
    }

    private static void RejectsNonAsciiStrings()
    {
        var data = BuildSmb(writer =>
        {
            writer.AddByte(2);
            writer.AddByte(0x80);
            writer.AddByte(0);
        });

        TestAssertions.ThrowsInvalidData(() => OberonSymbolModuleReader.Parse(data));
    }

    private static void RejectsTrailingBytes()
    {
        var data = BuildSmb(_ => { }).Append((byte)1).ToArray();

        TestAssertions.ThrowsInvalidData(() => OberonSymbolModuleReader.Parse(data));
    }

    private static void RejectsInvalidModuleNames()
    {
        TestAssertions.ThrowsInvalidData(() => OberonSymbolModuleReader.Parse(BuildSmb(_ => { }, "")));
        TestAssertions.ThrowsInvalidData(() => OberonSymbolModuleReader.Parse(
            BuildSmb(_ => { }, new string('A', 32))));
    }

    private static void RejectsOverlongNumbers()
    {
        var data = BuildSmb(writer =>
        {
            writer.AddByte(2);
            writer.AddString("Answer");
            writer.AddByte(0xfc);
            writer.AddByte(0x80);
            writer.AddByte(0x80);
            writer.AddByte(0x80);
            writer.AddByte(0x80);
            writer.AddByte(0x80);
        });

        TestAssertions.ThrowsInvalidData(() => OberonSymbolModuleReader.Parse(data));
    }
}
