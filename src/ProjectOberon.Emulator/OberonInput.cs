namespace ProjectOberon.Emulator;

public sealed class OberonKeyboard
{
    private readonly Queue<byte> _scanCodes = new();
    private bool _up;
    private bool _shift;
    private bool _ctrl;
    private bool _extended;
    private bool _received;
    private byte _keyCode;

    public bool Shift => _shift;
    public bool Ctrl => _ctrl;
    public bool Extended => _extended;

    public void Enqueue(byte scanCode) => _scanCodes.Enqueue(scanCode);

    public int Available()
    {
        Peek();
        return _received ? 1 : 0;
    }

    public bool TryRead(out char character)
    {
        while (!_received && _scanCodes.Count != 0) Peek();
        if (!_received)
        {
            character = '\0';
            return false;
        }

        var tableIndex = (byte)(_keyCode | (_shift || _ctrl ? 0x80 : 0));
        character = (char)TranslationTable[tableIndex];
        if (_ctrl)
        {
            character = (char)(character % 0x20);
        }
        _received = false;
        return true;
    }

    public void Reset()
    {
        _scanCodes.Clear();
        _up = _shift = _ctrl = _extended = _received = false;
        _keyCode = 0;
    }

    private void Peek()
    {
        if (_received || _scanCodes.Count == 0)
        {
            return;
        }

        var scanCode = _scanCodes.Dequeue();
        if (scanCode == 0xf0)
        {
            _up = true;
            return;
        }
        if (scanCode == 0xe0)
        {
            _extended = true;
            return;
        }

        if (scanCode is 0x12 or 0x59)
        {
            _shift = !_up;
        }
        else if (scanCode == 0x14)
        {
            _ctrl = !_up;
        }
        else if (!_up)
        {
            _keyCode = scanCode;
            _received = true;
        }

        _up = false;
        _extended = false;
    }

    private static readonly byte[] TranslationTable = CreateTranslationTable();

    private static byte[] CreateTranslationTable()
    {
        var table = new byte[256];
        Add(table, 0x1c, 'a', 'A'); Add(table, 0x32, 'b', 'B'); Add(table, 0x21, 'c', 'C');
        Add(table, 0x23, 'd', 'D'); Add(table, 0x24, 'e', 'E'); Add(table, 0x2b, 'f', 'F');
        Add(table, 0x34, 'g', 'G'); Add(table, 0x33, 'h', 'H'); Add(table, 0x43, 'i', 'I');
        Add(table, 0x3b, 'j', 'J'); Add(table, 0x42, 'k', 'K'); Add(table, 0x4b, 'l', 'L');
        Add(table, 0x3a, 'm', 'M'); Add(table, 0x31, 'n', 'N'); Add(table, 0x44, 'o', 'O');
        Add(table, 0x4d, 'p', 'P'); Add(table, 0x15, 'q', 'Q'); Add(table, 0x2d, 'r', 'R');
        Add(table, 0x1b, 's', 'S'); Add(table, 0x2c, 't', 'T'); Add(table, 0x3c, 'u', 'U');
        Add(table, 0x2a, 'v', 'V'); Add(table, 0x1d, 'w', 'W'); Add(table, 0x22, 'x', 'X');
        Add(table, 0x35, 'y', 'Y'); Add(table, 0x1a, 'z', 'Z');
        Add(table, 0x16, '1', '!'); Add(table, 0x1e, '2', '@'); Add(table, 0x26, '3', '#');
        Add(table, 0x25, '4', '$'); Add(table, 0x2e, '5', '%'); Add(table, 0x36, '6', '^');
        Add(table, 0x3d, '7', '&'); Add(table, 0x3e, '8', '*'); Add(table, 0x46, '9', '(');
        Add(table, 0x45, '0', ')'); Add(table, 0x4e, '-', '_'); Add(table, 0x55, '=', '+');
        Add(table, 0x54, '[', '{'); Add(table, 0x5b, ']', '}'); Add(table, 0x5d, '\\', '|');
        Add(table, 0x4c, ';', ':'); Add(table, 0x52, '\'', '"'); Add(table, 0x0e, '`', '~');
        Add(table, 0x41, ',', '<'); Add(table, 0x49, '.', '>'); Add(table, 0x4a, '/', '?');
        Add(table, 0x29, ' ', ' '); Add(table, 0x5a, '\r', '\r'); Add(table, 0x0d, '\t', '\t');
        Add(table, 0x66, '\b', '\b'); Add(table, 0x76, '\u001b', '\u001b');
        return table;
    }

    private static void Add(byte[] table, byte scanCode, char normal, char shifted)
    {
        table[scanCode] = (byte)normal;
        table[scanCode | 0x80] = (byte)shifted;
    }
}
