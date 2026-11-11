using ProjectOberon.Emulator;

internal static partial class OberonPersistenceTests
{
    public static void OberonFilesAndRiders()
    {
        var file = new OberonFile("Test.Mod", new byte[2]);
        var writer = file.Set();
        writer.WriteInt(unchecked((int)0x1234_5678));
        writer.WriteString("Oberon");
        writer.WriteNum(42);
        writer.WriteNum(300);
        writer.WriteNum(-100);
        TestAssertions.Equal(16, file.Length);
        var reader = file.Set();
        TestAssertions.True(reader.ReadInt(out var integer));
        TestAssertions.Equal(unchecked((int)0x1234_5678), integer);
        TestAssertions.Equal("Oberon", reader.ReadString());
        TestAssertions.True(reader.ReadNum(out integer));
        TestAssertions.Equal(42, integer);
        TestAssertions.True(reader.ReadNum(out integer));
        TestAssertions.Equal(300, integer);
        TestAssertions.True(reader.ReadNum(out integer));
        TestAssertions.Equal(-100, integer);
        TestAssertions.False(reader.ReadByte(out _));
        TestAssertions.True(reader.Eof);
        TestAssertions.Equal(16, reader.Position);
    }
}
