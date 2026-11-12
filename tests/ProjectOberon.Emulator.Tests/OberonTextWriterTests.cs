using ProjectOberon.Emulator;

internal static partial class OberonTextTests
{
    private static void WritesFormattedText()
    {
        var writer = new OberonTextWriter();
        writer.SetFont("Oberon12");
        writer.SetColor(15);
        writer.Write("value=");
        writer.WriteInt(-42, 5);
        writer.WriteLine();
        writer.WriteHex(0x2a);

        var text = new OberonText();
        text.Append(writer.Buffer);
        TestAssertions.Equal("value=  -42\r 0000002a", text.GetText());
        TestAssertions.Equal(new OberonTextStyle("Oberon12", 15, 0), text.Attributes(0));
    }

    private static void WritesNumericBoundaries()
    {
        var writer = new OberonTextWriter();
        writer.WriteInt(int.MinValue);
        writer.Write('|');
        writer.WriteInt(int.MaxValue, 4);
        writer.Write('|');
        writer.WriteHex(uint.MaxValue);

        var text = new OberonText();
        text.Append(writer.Buffer);

        TestAssertions.Equal("-2147483648|2147483647| ffffffff", text.GetText());
    }
}
