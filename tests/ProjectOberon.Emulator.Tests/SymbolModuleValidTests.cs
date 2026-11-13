using ProjectOberon.Emulator;

internal static partial class SymbolModuleTests
{
    private static void ParsesBuiltinTypeReference()
    {
        var data = BuildSmb(writer =>
        {
            writer.AddByte(2);
            writer.AddString("value");
            writer.AddByte(0xfc);
            writer.AddByte(3);
            writer.AddByte(0);
        });

        var module = OberonSymbolModuleReader.Parse(data);

        TestAssertions.Equal(4, module.Exports[0].TypeForm);
        TestAssertions.Equal(3, module.Exports[0].Value);
    }

    private static void ParsesExportedSymbol()
    {
        var data = BuildSmb(writer =>
        {
            writer.AddByte(2);
            writer.AddString("heapOrg");
            writer.AddByte(0xfc);
            writer.AddByte(3);
            writer.AddByte(0);
        });

        var module = OberonSymbolModuleReader.Parse(data);

        TestAssertions.Equal("Kernel", module.Name);
        TestAssertions.Equal(0x1234_5678, module.Key);
        TestAssertions.Equal(1, module.Exports.Count);
        TestAssertions.Equal("heapOrg", module.Exports[0].Name);
        TestAssertions.Equal(2, module.Exports[0].Class);
        TestAssertions.Equal(3, module.Exports[0].Value);
        TestAssertions.False(module.Exports[0].IsProcedure ?? false);
        TestAssertions.Equal(0, module.Exports[0].ParameterCount);
    }

    private static void ParsesTypeFixupsAndReexport()
    {
        var data = BuildSmb(writer =>
        {
            writer.AddByte(5);
            writer.AddString("Type");
            writer.AddByte(0);
            writer.AddByte(1);
            writer.AddString("Base");
            writer.AddInt(77);
            writer.AddString("BaseType");
            writer.AddByte(1);
            writer.AddByte(0);
            writer.AddByte(0);
        });

        var module = OberonSymbolModuleReader.Parse(data);

        TestAssertions.Equal(1, module.Exports.Count);
        TestAssertions.Equal("Type", module.Exports[0].Name);
        TestAssertions.Equal(5, module.Exports[0].Class);
        TestAssertions.Equal(0, module.Exports[0].Value);
    }

    private static void ParsesRecordAndProcedureTypes()
    {
        var data = BuildSmb(writer =>
        {
            writer.AddByte(3);
            writer.AddString("Record");
            writer.AddByte(0);
            writer.AddByte(13);
            writer.AddByte(0xfc);
            writer.AddByte(1);
            writer.AddByte(2);
            writer.AddByte(3);
            writer.AddByte(0);
            writer.AddString(string.Empty);

            writer.AddByte(4);
            writer.AddString("Procedure");
            writer.AddByte(0);
            writer.AddByte(10);
            writer.AddByte(0xfc);
            writer.AddByte(1);
            writer.AddByte(0);
            writer.AddByte(0xfc);
            writer.AddByte(0);
            writer.AddString(string.Empty);
        });

        var module = OberonSymbolModuleReader.Parse(data);

        TestAssertions.Equal(2, module.Exports.Count);
        TestAssertions.Equal("Record", module.Exports[0].Name);
        TestAssertions.False(module.Exports[0].IsProcedure ?? false);
        TestAssertions.True(module.Exports[1].IsProcedure ?? false);
        TestAssertions.Equal(1, module.Exports[1].ParameterCount);
        TestAssertions.Equal("Procedure", module.Exports[1].Name);
    }
}
