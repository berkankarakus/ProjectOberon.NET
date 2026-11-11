using ProjectOberon.Emulator;

internal static partial class RscModuleTests
{
    private static void WritesRoundTripModule()
    {
        var source = BuildRsc(
            name: "Demo",
            key: 42,
            imports: [("Kernel", 0x1234)],
            command: "Start",
            typePayload: [1, 2, 3],
            variableBytes: 16,
            stringPayload: [(byte)'x', 0],
            code: [0xc700_0010u, 0xc700_0020u],
            entries: [0, 4],
            pointerReferences: [8, 12],
            fixupProgram: 1,
            fixupData: 2,
            fixupTypes: 3,
            bodyOffset: 4);

        var module = RscModuleReader.Parse(source);
        var roundTrip = RscModuleReader.Parse(RscModuleWriter.Write(module));

        TestAssertions.Equal(module.Name, roundTrip.Name);
        TestAssertions.Equal(module.Key, roundTrip.Key);
        TestAssertions.Equal(module.Imports.Count, roundTrip.Imports.Count);
        TestAssertions.Equal(module.TypeDescriptorPayload.Length, roundTrip.TypeDescriptorPayload.Length);
        TestAssertions.Equal(module.VariableBytes, roundTrip.VariableBytes);
        TestAssertions.Equal(module.StringPayload.Length, roundTrip.StringPayload.Length);
        TestAssertions.True(module.Code.SequenceEqual(roundTrip.Code));
        TestAssertions.Equal(module.Commands.Count, roundTrip.Commands.Count);
        TestAssertions.True(module.Entries.SequenceEqual(roundTrip.Entries));
        TestAssertions.True(module.PointerReferences.SequenceEqual(roundTrip.PointerReferences));
        TestAssertions.Equal(module.BodyOffset, roundTrip.BodyOffset);
    }

    private static void RejectsMismatchedCodeCount()
    {
        var module = new RscModule("Demo", 1, 1, 0, [], 0, 0, 0, 1, [], [], [], [], 0, 0, 0, 0, (byte)'O');
        TestAssertions.ThrowsInvalidData(() => RscModuleWriter.Write(module));
    }

    private static void RejectsMismatchedPayloadCount()
    {
        var module = new RscModule("Demo", 1, 1, 0, [], 1, 0, 0, 0, [], [], [], [], 0, 0, 0, 0, (byte)'O');
        TestAssertions.ThrowsInvalidData(() => RscModuleWriter.Write(module));
    }
}
