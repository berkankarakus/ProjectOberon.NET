using ProjectOberon.Compiler;

internal static class OberonCompilerRegisterAllocatorTests
{
    public static void Run()
    {
        AcquiresAvailableRegisters();
        HonorsExcludedRegisters();
        ReleasesTemporaryRegisters();
        ReleasesReservedRangesIdempotently();
        RejectsInvalidReservations();
    }

    private static void AcquiresAvailableRegisters()
    {
        var allocator = new OberonCompilerRegisterAllocator();

        TestAssertions.Equal(0, allocator.AcquireTemporary());
        TestAssertions.Equal(1, allocator.AcquireTemporary());
    }

    private static void HonorsExcludedRegisters()
    {
        var allocator = new OberonCompilerRegisterAllocator();

        TestAssertions.Equal(1, allocator.AcquireTemporary(excluded: 0));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => allocator.AcquireTemporary(12));
    }

    private static void ReleasesTemporaryRegisters()
    {
        var allocator = new OberonCompilerRegisterAllocator();
        var register = allocator.AcquireTemporary();

        allocator.ReleaseTemporary(register);

        TestAssertions.Throws<InvalidOperationException>(() => allocator.ReleaseTemporary(register));
        TestAssertions.Equal(register, allocator.AcquireTemporary());
    }

    private static void ReleasesReservedRangesIdempotently()
    {
        var allocator = new OberonCompilerRegisterAllocator();
        var reservation = allocator.ReserveRange(0, 2);

        TestAssertions.Equal(2, allocator.AcquireTemporary());
        reservation.Dispose();
        reservation.Dispose();

        TestAssertions.Equal(0, allocator.AcquireTemporary());
    }

    private static void RejectsInvalidReservations()
    {
        var allocator = new OberonCompilerRegisterAllocator();

        TestAssertions.Throws<ArgumentOutOfRangeException>(() => allocator.ReserveRange(0, -1));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => allocator.ReserveRange(11, 2));
    }
}
