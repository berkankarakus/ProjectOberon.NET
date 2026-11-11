internal static partial class SymbolModuleTests
{
    private static void RejectsDuplicateExports()
    {
        var data = BuildSmb(writer =>
        {
            AddIntegerExport(writer, "Answer", 1);
            AddIntegerExport(writer, "Answer", 2);
        });

        TestAssertions.Throws<InvalidDataException>(() => OberonSymbolModuleReader.Parse(data));
    }

    private static void AddIntegerExport(SymbolWriter writer, string name, int value)
    {
        writer.AddByte(2);
        writer.AddString(name);
        writer.AddByte(0xfc);
        writer.AddByte(3);
        writer.AddByte((byte)value);
    }
}
