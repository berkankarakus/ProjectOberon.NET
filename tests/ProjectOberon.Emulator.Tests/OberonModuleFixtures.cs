internal static partial class OberonModuleTests
{
    private static byte[] BuildRsc(
        string name,
        int key,
        (string Name, int Key)[] imports,
        string command,
        byte[]? typePayload = null,
        int variableBytes = 0,
        byte[]? stringPayload = null,
        uint[]? code = null,
        int[]? entries = null,
        int fixupProgram = 0,
        int fixupData = 0,
        int fixupTypes = 0,
        int bodyOffset = 0)
    {
        typePayload ??= [];
        stringPayload ??= [];
        code ??= [0xc700_0000u];
        entries ??= [0];
        var data = new List<byte>();
        TestFixtures.AddString(data, name);
        TestFixtures.AddInt(data, key);
        data.Add(1);
        TestFixtures.AddInt(data, 0);
        foreach (var import in imports)
        {
            TestFixtures.AddString(data, import.Name);
            TestFixtures.AddInt(data, import.Key);
        }
        TestFixtures.AddString(data, string.Empty);
        TestFixtures.AddInt(data, typePayload.Length);
        data.AddRange(typePayload);
        TestFixtures.AddInt(data, variableBytes);
        TestFixtures.AddInt(data, stringPayload.Length);
        data.AddRange(stringPayload);
        TestFixtures.AddInt(data, code.Length);
        foreach (var word in code) TestFixtures.AddInt(data, unchecked((int)word));
        TestFixtures.AddString(data, command);
        TestFixtures.AddInt(data, 0);
        TestFixtures.AddString(data, string.Empty);
        TestFixtures.AddInt(data, entries.Length);
        foreach (var entry in entries) TestFixtures.AddInt(data, entry);
        TestFixtures.AddInt(data, -1);
        TestFixtures.AddInt(data, fixupProgram);
        TestFixtures.AddInt(data, fixupData);
        TestFixtures.AddInt(data, fixupTypes);
        TestFixtures.AddInt(data, bodyOffset);
        data.Add((byte)'O');
        return data.ToArray();
    }

    private static byte[] BuildSmb(string name, int key, string exportName, int exportValue)
    {
        var data = new List<byte>();
        TestFixtures.AddInt(data, 0);
        TestFixtures.AddInt(data, key);
        TestFixtures.AddString(data, name);
        data.Add(1);
        data.Add(2);
        TestFixtures.AddString(data, exportName);
        data.Add(0xfc);
        data.Add((byte)exportValue);
        data.Add(0);
        return data.ToArray();
    }
}
