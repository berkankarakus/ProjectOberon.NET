namespace ProjectOberon.Emulator;

public enum MmioRegister : uint
{
    Timer = 0x00, Leds = 0x04, UartData = 0x08, UartControl = 0x0c,
    SpiData = 0x10, SpiControl = 0x14, KeyboardStatus = 0x18,
    KeyboardData = 0x1c, GpioData = 0x20, GpioControl = 0x24
}

public sealed class DeviceBus
{
    public const uint IoBase = MemoryMap.IoBase;
    private readonly Queue<byte> _uartRx = new();
    private readonly Queue<byte> _keyboard = new();
    private readonly SpiDevice _spi = new();
    private uint _milliseconds;
    private byte _leds;
    private byte _gpioOut;
    private byte _gpioControl;
    private byte _uartControl;
    private ushort _mouseX;
    private ushort _mouseY;
    private byte _mouseButtons;
    private readonly System.Text.StringBuilder _uartOutput = new();

    public bool Handles(uint address)
    {
        var masked = address & MemoryMap.AddressMask;
        return masked >= IoBase && masked < IoBase + 0x28;
    }

    public uint Read(uint address)
    {
        switch ((MmioRegister)((address - IoBase) & 0x3f))
        {
            case MmioRegister.Timer: return _milliseconds;
            case MmioRegister.Leds: return _leds;
            case MmioRegister.UartData: return _uartRx.Count == 0 ? 0u : _uartRx.Dequeue();
            case MmioRegister.UartControl: return (uint)(_uartRx.Count == 0 ? 0 : 1) | (1u << 1);
            case MmioRegister.SpiData: return _spi.ReadData();
            case MmioRegister.SpiControl: return _spi.ReadStatus();
            case MmioRegister.KeyboardStatus:
                return ((uint)(_mouseButtons & 0x07) << 24) | ((uint)(_mouseY & 0x03ff) << 12) |
                       (_mouseX & 0x03ffu) | ((uint)(_keyboard.Count == 0 ? 0 : 1) << 28);
            case MmioRegister.KeyboardData: return _keyboard.Count == 0 ? 0u : _keyboard.Dequeue();
            case MmioRegister.GpioData: return _gpioOut;
            case MmioRegister.GpioControl: return _gpioControl;
            default: return 0;
        }
    }

    public void Write(uint address, uint value)
    {
        switch ((MmioRegister)((address - IoBase) & 0x3f))
        {
            case MmioRegister.Leds: _leds = (byte)value; break;
            case MmioRegister.UartData: _uartOutput.Append((char)(value & 0xff)); break;
            case MmioRegister.UartControl: _uartControl = (byte)value; break;
            case MmioRegister.SpiData: _spi.WriteData(value); break;
            case MmioRegister.SpiControl: _spi.WriteControl(value); break;
            case MmioRegister.GpioData: _gpioOut = (byte)value; break;
            case MmioRegister.GpioControl: _gpioControl = (byte)value; break;
        }
    }

    public void Reset()
    {
        _milliseconds = 0;
        _leds = _gpioOut = _gpioControl = _uartControl = 0;
        _spi.Reset();
        _uartRx.Clear();
        _keyboard.Clear();
        _mouseX = _mouseY = 0;
        _mouseButtons = 0;
        _uartOutput.Clear();
    }

    public void AdvanceTime() => _milliseconds++;
    public void AttachDisk(DiskImage disk) => _spi.AttachDisk(disk);
    public void DetachDisk() => _spi.DetachDisk();
    public bool HasDisk => _spi.HasDisk;
    public void EnqueueUartRx(byte value) => _uartRx.Enqueue(value);
    public void EnqueueKeyboard(byte value) => _keyboard.Enqueue(value);
    public void SetMouse(ushort x, ushort y, byte buttons)
    {
        _mouseX = (ushort)(x & 0x03ff);
        _mouseY = (ushort)(y & 0x03ff);
        _mouseButtons = (byte)(buttons & 7);
    }

    public string TakeUartOutput()
    {
        var output = _uartOutput.ToString();
        _uartOutput.Clear();
        return output;
    }
    public byte Leds => _leds;
}
