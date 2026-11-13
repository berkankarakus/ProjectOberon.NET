namespace ProjectOberon.Compiler;

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
        {
            throw new InvalidOperationException("expression register is not allocated");
        }

        _reservations[register]--;
    }

    public IDisposable ReserveRange(int firstRegister, int count)
    {
        ReserveRangeCore(firstRegister, count);

        if (count == 0)
        {
            return NoopDisposable.Instance;
        }

        return new Reservation(this, firstRegister, count);
    }

    internal ReservationScope ReserveRangeScope(int firstRegister, int count)
    {
        ReserveRangeCore(firstRegister, count);

        if (count == 0)
        {
            return default;
        }

        return new ReservationScope(this, firstRegister, count);
    }

    private void ReserveRangeCore(int firstRegister, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 0)
        {
            return;
        }

        ValidateExpressionRegister(firstRegister);
        ValidateExpressionRegister(firstRegister + count - 1);
        for (var register = firstRegister; register < firstRegister + count; register++)
        {
            _reservations[register]++;
        }
    }

    public static void ValidateExpressionRegister(int register, bool allowExcluded = false)
    {
        if (allowExcluded && register == -1)
        {
            return;
        }

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
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            allocator.ReleaseRange(firstRegister, count);
        }
    }

    internal struct ReservationScope : IDisposable
    {
        private OberonCompilerRegisterAllocator? _allocator;
        private readonly int _firstRegister;
        private readonly int _count;

        internal ReservationScope(
            OberonCompilerRegisterAllocator allocator,
            int firstRegister,
            int count
        )
        {
            _allocator = allocator;
            _firstRegister = firstRegister;
            _count = count;
        }

        public void Dispose()
        {
            var allocator = _allocator;
            if (allocator is null)
            {
                return;
            }

            _allocator = null;
            allocator.ReleaseRange(_firstRegister, _count);
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();
        public void Dispose() { }
    }
}
