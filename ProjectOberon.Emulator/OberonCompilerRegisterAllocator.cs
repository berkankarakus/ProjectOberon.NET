namespace ProjectOberon.Emulator;

public sealed class OberonCompilerRegisterAllocator
{
    public const int FirstExpressionRegister = 0;
    public const int LastExpressionRegister = 11;

    private readonly int[] _reservations = new int[LastExpressionRegister + 1];

    public int AcquireTemporary(int excluded = -1)
    {
        ValidateExpressionRegister(excluded, allowExcluded: true);
        for (var register = FirstExpressionRegister; register <= LastExpressionRegister; register++)
        {
            if (register != excluded && _reservations[register] == 0)
            {
                _reservations[register]++;
                return register;
            }
        }

        throw new InvalidOperationException("no expression register is available");
    }

    public void ReleaseTemporary(int register)
    {
        ValidateExpressionRegister(register);
        if (_reservations[register] == 0)
            throw new InvalidOperationException("expression register is not allocated");
        _reservations[register]--;
    }

    public IDisposable ReserveRange(int firstRegister, int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0) return NoopDisposable.Instance;
        ValidateExpressionRegister(firstRegister);
        ValidateExpressionRegister(firstRegister + count - 1);
        for (var register = firstRegister; register < firstRegister + count; register++)
            _reservations[register]++;
        return new Reservation(this, firstRegister, count);
    }

    public static void ValidateExpressionRegister(int register, bool allowExcluded = false)
    {
        if (allowExcluded && register == -1) return;
        if (register is < FirstExpressionRegister or > LastExpressionRegister)
            throw new ArgumentOutOfRangeException(nameof(register),
                "expression registers must not overlap module base, stack pointer, or link register");
    }

    private void ReleaseRange(int firstRegister, int count)
    {
        for (var register = firstRegister; register < firstRegister + count; register++)
            _reservations[register]--;
    }

    private sealed class Reservation(
        OberonCompilerRegisterAllocator allocator,
        int firstRegister,
        int count) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            allocator.ReleaseRange(firstRegister, count);
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();
        public void Dispose() { }
    }
}
