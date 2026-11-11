using ProjectOberon.Emulator;

internal static partial class RscModuleTests
{
    private static void ParsesCompleteModule()
    {
        var data = BuildRsc(
            name: "Demo",
            key: 42,
            imports: [("Kernel", 0x1234)],
            command: "Start",
            typePayload: [1, 2, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            variableBytes: 16,
            stringPayload: [(byte)'x', 0],
            code: [0xc700_0010u, 0xc700_0020u, 0],
            entries: [0, 4],
            pointerReferences: [8, 12],
            fixupProgram: 1,
            fixupData: 2,
            fixupTypes: 3,
            bodyOffset: 4);

        var module = RscModuleReader.Parse(data);

        TestAssertions.Equal("Demo", module.Name);
        TestAssertions.Equal(42, module.Key);
        TestAssertions.Equal(1, module.Imports.Count);
        TestAssertions.Equal("Kernel", module.Imports[0].Name);
        TestAssertions.Equal(0x1234, module.Imports[0].Key);
        TestAssertions.Equal(16, module.VariableBytes);
        TestAssertions.Equal(3, module.Code.Count);
        TestAssertions.Equal(0xc700_0020u, module.Code[1]);
        TestAssertions.Equal(1, module.Commands.Count);
        TestAssertions.Equal("Start", module.Commands[0].Name);
        TestAssertions.Equal(2, module.Entries.Count);
        TestAssertions.Equal(2, module.PointerReferences.Count);
        TestAssertions.Equal(12, module.PointerReferences[1]);
        TestAssertions.Equal(1, module.FixupProgram);
        TestAssertions.Equal(2, module.FixupData);
        TestAssertions.Equal(3, module.FixupTypes);
        TestAssertions.Equal(4, module.BodyOffset);
        TestAssertions.Equal((byte)'O', module.Trailer);
        TestAssertions.Equal(16, module.TypeDescriptorPayload.Length);
        TestAssertions.Equal(2, module.StringPayload.Length);
    }
}
