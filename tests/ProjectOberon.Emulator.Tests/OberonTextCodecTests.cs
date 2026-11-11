using ProjectOberon.Emulator;

internal static class OberonTextCodecTests
{
    public static void Run()
    {
        RoundTripsStyledText();
        CreatesAndLoadsTextFiles();
        RejectsInvalidTextBlocks();
        RejectsUnrepresentableLatin1Text();
    }

    private static void RoundTripsStyledText()
    {
        var text = new OberonText("Hello");
        text.ChangeLooks(1, 4, new OberonTextStyle("Oberon12", 15, -1));
        var decoded = OberonTextCodec.Decode(OberonTextCodec.Encode(text));

        TestAssertions.Equal("Hello", decoded.GetText());
        TestAssertions.Equal(new OberonTextStyle("Oberon12", 15, -1), decoded.Attributes(1));
        TestAssertions.Equal(OberonTextStyle.Default, decoded.Attributes(0));
        TestAssertions.False(decoded.Changed);
    }

    private static void CreatesAndLoadsTextFiles()
    {
        var source = new OberonText("stored text");
        var file = OberonTextCodec.CreateFile("Demo.Text", source, date: 42);
        var loaded = OberonTextCodec.Load(file);

        TestAssertions.Equal("Demo.Text", file.Name);
        TestAssertions.Equal(42, file.Date);
        TestAssertions.Equal("stored text", loaded.GetText());
    }

    private static void RejectsInvalidTextBlocks()
    {
        TestAssertions.ThrowsInvalidData(() => OberonTextCodec.Decode([0, 0, 0, 0, 0]));
        TestAssertions.ThrowsInvalidData(() => OberonTextCodec.Decode([OberonTextCodec.TextTag, 0, 0, 0, 0, 1]));
    }

    private static void RejectsUnrepresentableLatin1Text()
    {
        TestAssertions.ThrowsInvalidData(() => OberonTextCodec.Encode(new OberonText("😀")));

        var text = new OberonText("ok");
        text.ChangeLooks(0, 2, new OberonTextStyle("Bad\0Font", 1, 0));
        TestAssertions.ThrowsInvalidData(() => OberonTextCodec.Encode(text));
    }
}
