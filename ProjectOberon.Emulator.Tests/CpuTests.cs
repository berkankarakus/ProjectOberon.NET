using ProjectOberon.Emulator;

internal static class CpuTests
{
    public static void ArithmeticAndFlags()
    {
        var memory = new Memory();
        memory.LoadRomWords(TestFixtures.RomWith(TestFixtures.MovImmediate(1, 7), TestFixtures.MovImmediate(2, 5), TestFixtures.Alu(AluOperation.Add, 3, 1, 2)));
        var cpu = new Risc5Cpu(memory);
        cpu.Step(); cpu.Step(); cpu.Step();
        TestAssertions.Equal(12u, cpu.Register(3));
        TestAssertions.False(cpu.Zero);
        TestAssertions.False(cpu.Negative);
    }

    public static void IntegerSignednessAndSubtractBorrow()
    {
        var memory = new Memory();
        memory.LoadRomWords(TestFixtures.RomWith(TestFixtures.MovImmediate(1, 0xffff, true), TestFixtures.MovImmediate(2, 2), TestFixtures.Alu(AluOperation.Mul, 3, 1, 2)));
        var cpu = new Risc5Cpu(memory);
        cpu.Step(); cpu.Step(); cpu.Step();
        TestAssertions.Equal(0xffff_fffeu, cpu.Register(3));
        TestAssertions.Equal(0xffff_ffffu, cpu.High);

        memory = new Memory();
        memory.LoadRomWords(TestFixtures.RomWith(TestFixtures.MovImmediate(1, 3), TestFixtures.MovImmediate(2, 5), TestFixtures.Alu(AluOperation.Sub, 3, 1, 2)));
        cpu = new Risc5Cpu(memory);
        cpu.Step(); cpu.Step(); cpu.Step();
        TestAssertions.Equal(0xffff_fffeu, cpu.Register(3));
        TestAssertions.True(cpu.Carry);
    }

    public static void BranchAndLink()
    {
        var memory = new Memory();
        memory.LoadRomWords(TestFixtures.RomWith(TestFixtures.BranchRelative(ConditionCode.Always, 2, true), TestFixtures.MovImmediate(1, 0xdead), TestFixtures.MovImmediate(1, 0xbeef), TestFixtures.MovImmediate(2, 42)));
        var cpu = new Risc5Cpu(memory);
        cpu.Step(); cpu.Step();
        TestAssertions.Equal(0u, cpu.Register(1));
        TestAssertions.Equal(42u, cpu.Register(2));
        TestAssertions.Equal((Risc5Cpu.ResetPc + 1) * 4, cpu.Register(15));
    }

    public static void MemoryWordAndByteAccess()
    {
        var memory = new Memory();
        memory.LoadRomWords(TestFixtures.RomWith(TestFixtures.MovImmediate(1, 0x100), TestFixtures.MovImmediate(2, 0xaa), TestFixtures.MemoryOp(true, true, 2, 1, 0x20), TestFixtures.MemoryOp(false, true, 3, 1, 0x20)));
        var cpu = new Risc5Cpu(memory);
        for (var index = 0; index < 4; index++) cpu.Step();
        TestAssertions.Equal(0xaau, cpu.Register(3));
    }

    public static void InterruptVectorAndRti()
    {
        var memory = new Memory();
        memory.LoadRomWords(TestFixtures.RomWith(0xc700_0021u, 0xc700_0010u));
        memory.Write32(4, 0xc700_0010u);
        var cpu = new Risc5Cpu(memory);
        cpu.Step(); cpu.Step(true); cpu.Step(false);
        TestAssertions.Equal(1u, cpu.PcWord);
        cpu.Step(false);
        TestAssertions.Equal(Risc5Cpu.ResetPc + 3, cpu.PcWord);
    }
}
