using ProjectOberon.Emulator;

internal static partial class OberonTextTests
{
    private static void EditsAndReadsText()
    {
        var text = new OberonText("Hello");
        var buffer = new OberonTextBuffer();
        buffer.Append(", ");
        buffer.Append("Oberon");
        text.Insert(5, buffer);

        TestAssertions.Equal("Hello, Oberon", text.GetText());
        var reader = text.OpenReader(7);
        var result = new List<char>();
        while (reader.TryRead(out var character)) result.Add(character);

        TestAssertions.Equal("Oberon", new string(result.ToArray()));
        TestAssertions.True(reader.EndOfText);
    }

    private static void ValidatesTextPositions()
    {
        var text = new OberonText("abc");
        var reader = text.OpenReader(text.Length);

        TestAssertions.False(reader.TryRead(out _));
        TestAssertions.True(reader.EndOfText);
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => text.OpenReader(-1));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => text.OpenReader(text.Length + 1));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => text.Attributes(text.Length));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => text.Copy(2, 1));
    }

    private static void PreservesDeletedTextInBuffer()
    {
        var text = new OberonText("abc123xyz");
        var removed = text.Delete(3, 6);
        TestAssertions.Equal("abcxyz", text.GetText());

        var replacement = new OberonTextBuffer();
        replacement.CopyFrom(removed);
        text.Insert(3, replacement);
        TestAssertions.Equal("abc123xyz", text.GetText());
    }

    private static void AppliesStylesAndNotifies()
    {
        var text = new OberonText("abcd");
        var events = new List<(OberonTextOperation Operation, int Begin, int End)>();
        text.Notify = (_, operation, begin, end) => events.Add((operation, begin, end));
        var style = new OberonTextStyle("Oberon12", 3, -1);

        text.ChangeLooks(1, 3, style);
        TestAssertions.Equal(style, text.Attributes(1));
        TestAssertions.Equal(OberonTextStyle.Default, text.Attributes(0));
        TestAssertions.Equal(OberonTextOperation.Replace, events[0].Operation);
        TestAssertions.Equal(1, events[0].Begin);
        TestAssertions.Equal(3, events[0].End);
        TestAssertions.True(text.Changed);

        text.ClearChanged();
        TestAssertions.False(text.Changed);
        TestAssertions.Equal(OberonTextOperation.Unmark, events[1].Operation);
    }
}
