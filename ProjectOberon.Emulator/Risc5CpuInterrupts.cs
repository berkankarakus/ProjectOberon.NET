namespace ProjectOberon.Emulator;

public sealed partial class Risc5Cpu
{
    private void RestoreInterruptState()
    {
        _pc = _savedPc;
        _negative = _savedNegative;
        _zero = _savedZero;
        _carry = _savedCarry;
        _overflow = _savedOverflow;
        _interruptMode = false;
    }
}
