using System.Numerics;

namespace ProjectOberon.Emulator;

public sealed partial class Risc5Cpu
{
    public const uint ResetPc = 0x003f_f800u;

    private readonly IMemoryBus _memory;
    private readonly uint[] _registers = new uint[32];
    private uint _pc = ResetPc;
    private uint _high;
    private bool _negative;
    private bool _zero;
    private bool _carry;
    private bool _overflow;
    private bool _irqLevel;
    private bool _interruptEnabled;
    private bool _interruptPending;
    private bool _interruptMode;
    private uint _savedPc;
    private bool _savedNegative;
    private bool _savedZero;
    private bool _savedCarry;
    private bool _savedOverflow;

    public Risc5Cpu(IMemoryBus memory)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    }

    public uint PcWord => _pc;
    public uint PcByte => (_pc << 2) & Memory.AddressMask;
    public uint High => _high;
    public bool Negative => _negative;
    public bool Zero => _zero;
    public bool Carry => _carry;
    public bool Overflow => _overflow;
    public ulong Steps { get; private set; }
    public uint Register(int index) => _registers[index & 15];

    public void SetRegister(int index, uint value)
    {
        if (index is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "RISC5 has 16 general-purpose registers");
        }

        WriteRegister(index, value);
    }

    public void SetProgramCounter(uint byteAddress)
    {
        if ((byteAddress & 3) != 0)
        {
            throw new ArgumentException("program counter must be word-aligned", nameof(byteAddress));
        }

        if (byteAddress > Memory.AddressMask)
        {
            throw new ArgumentOutOfRangeException(nameof(byteAddress));
        }

        _pc = (byteAddress >> 2) & InstructionBits.BranchDisplacementMask;
    }

    public void Reset()
    {
        Array.Clear(_registers);
        _pc = ResetPc;
        _high = 0;
        _negative = _zero = _carry = _overflow = false;
        _irqLevel = _interruptEnabled = _interruptPending = _interruptMode = false;
        _savedPc = 0;
        _savedNegative = _savedZero = _savedCarry = _savedOverflow = false;
        Steps = 0;
    }

    public void Step() => Step(false);

    public void Step(bool irq)
    {
        var instruction = Decode(_memory.ReadInstruction(PcByte));
        var nextPc = (_pc + 1u) & InstructionBits.BranchDisplacementMask;
        var interruptAck = _interruptPending && _interruptEnabled && !_interruptMode;
        var irqRising = irq && !_irqLevel;
        _irqLevel = irq;

        if (irqRising) _interruptPending = true;
        _pc = nextPc;

        if (!instruction.P)
        {
            ExecuteRegister(instruction);
        }
        else if (!instruction.Q)
        {
            ExecuteMemory(instruction);
        }
        else if (instruction.U || instruction.V || (instruction.Raw & 0x30u) == 0)
        {
            ExecuteBranch(instruction, nextPc);
        }
        else
        {
            if ((instruction.Raw & 0x10u) != 0 && _interruptMode) RestoreInterruptState();
            if ((instruction.Raw & 0x20u) != 0) _interruptEnabled = (instruction.Raw & 1u) != 0;
        }

        if (interruptAck)
        {
            _savedPc = _pc;
            _savedNegative = _negative;
            _savedZero = _zero;
            _savedCarry = _carry;
            _savedOverflow = _overflow;
            _pc = 1;
            _interruptPending = false;
            _interruptMode = true;
        }

        Steps++;
    }

    internal void CountHostCall()
    {
        Steps++;
    }

}
