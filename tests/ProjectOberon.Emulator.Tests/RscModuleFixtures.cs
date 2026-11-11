using System.Buffers.Binary;

internal static partial class RscModuleTests
{
    private static byte[] BuildRsc(
        string name,
        int key,
        (string Name, int Key)[] imports,
        string command,
        int commandOffset = 0,
        int size = 0,
        byte[]? typePayload = null,
        int variableBytes = 0,
        byte[]? stringPayload = null,
        int? stringBytes = null,
        uint[]? code = null,
        int? codeWordCount = null,
        int[]? entries = null,
        int[]? pointerReferences = null,
        int? entryCount = null,
        int fixupProgram = 0,
        int fixupData = 0,
        int fixupTypes = 0,
        int bodyOffset = 0,
        string[]? additionalCommands = null)
    {
        typePayload ??= [];
        stringPayload ??= [];
        code ??= [0xc700_0000u];
        entries ??= [0];
        pointerReferences ??= [];

        var data = new List<byte>();
        AddString(data, name);
        AddInt(data, key);
        data.Add(1);
        AddInt(data, size);

        foreach (var import in imports)
        {
            AddString(data, import.Name);
            AddInt(data, import.Key);
        }

        AddString(data, string.Empty);
        AddInt(data, typePayload.Length);
        data.AddRange(typePayload);
        AddInt(data, variableBytes);
        AddInt(data, stringBytes ?? stringPayload.Length);
        data.AddRange(stringPayload);
        AddInt(data, codeWordCount ?? code.Length);
        foreach (var word in code)
        {
            AddInt(data, unchecked((int)word));
        }

        AddString(data, command);
        AddInt(data, commandOffset);
        foreach (var additionalCommand in additionalCommands ?? [])
        {
            AddString(data, additionalCommand);
            AddInt(data, commandOffset);
        }
        AddString(data, string.Empty);
        AddInt(data, entryCount ?? entries.Length);
        foreach (var entry in entries)
        {
            AddInt(data, entry);
        }

        foreach (var pointerReference in pointerReferences)
        {
            AddInt(data, pointerReference);
        }

        AddInt(data, -1);
        AddInt(data, fixupProgram);
        AddInt(data, fixupData);
        AddInt(data, fixupTypes);
        AddInt(data, bodyOffset);
        data.Add((byte)'O');
        return data.ToArray();
    }

    private static void AddString(List<byte> data, string value)
    {
        data.AddRange(System.Text.Encoding.ASCII.GetBytes(value));
        data.Add(0);
    }

    private static void AddInt(List<byte> data, int value)
    {
        var bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        data.AddRange(bytes);
    }
}
