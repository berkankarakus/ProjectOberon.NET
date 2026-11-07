namespace ProjectOberon.Emulator;

public sealed class SpiDevice
{
    public const byte Card0 = 1;
    public const byte Fast = 4;

    private readonly byte[] _writeBuffer = new byte[DiskImage.SectorSize];
    private readonly byte[] _command = new byte[6];
    private readonly Queue<byte> _response = new();
    private DiskImage? _disk;
    private byte _control;
    private uint _received = uint.MaxValue;
    private bool _selected;
    private bool _appCommand;
    private bool _writeActive;
    private uint _writeSector;
    private int _writeOffset;
    private int _commandSize;

    public bool HasDisk => _disk is not null;
    public uint ReadData() => _received;
    public uint ReadStatus() => 1;

    public void AttachDisk(DiskImage disk) => _disk = disk ?? throw new ArgumentNullException(nameof(disk));
    public void DetachDisk() => _disk = null;

    public void Reset()
    {
        _control = 0;
        _received = uint.MaxValue;
        _selected = _appCommand = _writeActive = false;
        _writeSector = 0;
        _writeOffset = _commandSize = 0;
        Array.Clear(_writeBuffer);
        Array.Clear(_command);
        _response.Clear();
    }

    public void WriteData(uint value) => _received = ExchangeWord(value);

    public void WriteControl(uint value)
    {
        var next = (byte)value;
        _selected = (next & Card0) != 0;
        if (!_selected)
        {
            _commandSize = 0;
            _response.Clear();
            _writeActive = false;
        }
        _control = next;
    }

    private byte ExchangeByte(byte value)
    {
        if (!_selected || _disk is null) return 0xff;
        if (_response.Count > 0) return _response.Dequeue();
        if (_writeActive)
        {
            if (_writeOffset == 0)
            {
                if (value == 0xfe) _writeOffset = 1;
                return 0xff;
            }
            if (_writeOffset <= DiskImage.SectorSize)
            {
                _writeBuffer[_writeOffset - 1] = value;
                _writeOffset++;
                return 0xff;
            }
            if (_writeOffset == DiskImage.SectorSize + 1)
            {
                _writeOffset++;
                return 0xff;
            }
            _disk.Write(_writeSector, _writeBuffer);
            _writeActive = false;
            _writeOffset = 0;
            return 0x05;
        }

        if (_commandSize == 0 && value == 0xff) return 0xff;
        _command[_commandSize++] = value;
        if (_commandSize == _command.Length) CommandComplete();
        return 0xff;
    }

    private uint ExchangeWord(uint value)
    {
        if ((_control & Fast) == 0) return ExchangeByte((byte)value);
        uint result = 0;
        for (var index = 0; index < 4; index++)
            result |= (uint)ExchangeByte((byte)(value >> (index * 8))) << (index * 8);
        return result;
    }

    private void CommandComplete()
    {
        var command = _command[0] & 0x3f;
        var argument = ((uint)_command[1] << 24) | ((uint)_command[2] << 16) |
                       ((uint)_command[3] << 8) | _command[4];
        _commandSize = 0;
        switch (command)
        {
            case 0: _response.Enqueue(0x01); break;
            case 8: Enqueue(0x01, 0, 0, 1, 0xaa); break;
            case 55: _appCommand = true; _response.Enqueue(0x01); break;
            case 41: _response.Enqueue(_appCommand ? (byte)0x00 : (byte)0x05); _appCommand = false; break;
            case 58: Enqueue(0x00, 0x40, 0xff, 0x80, 0x00); break;
            case 17:
                _response.Enqueue(0x00);
                var sector = argument < _disk!.SectorCount ? argument : 0;
                _response.Enqueue(0xfe);
                foreach (var value in _disk.Read(sector)) _response.Enqueue(value);
                _response.Enqueue(0xff);
                _response.Enqueue(0xff);
                break;
            case 24:
                _response.Enqueue(0x00);
                _writeActive = true;
                _writeSector = argument < _disk!.SectorCount ? argument : 0;
                _writeOffset = 0;
                Array.Clear(_writeBuffer);
                break;
            default: _response.Enqueue(0x04); break;
        }
    }

    private void Enqueue(params byte[] values)
    {
        foreach (var value in values) _response.Enqueue(value);
    }
}
