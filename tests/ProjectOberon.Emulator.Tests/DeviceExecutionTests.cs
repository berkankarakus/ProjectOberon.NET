using ProjectOberon.Emulator;

internal static partial class DeviceTests
{
    public static void MachineTimer()
    {
        var machine = new Machine();
        machine.Run(Machine.TimerInterruptPeriod);
        TestAssertions.Equal(1000u, machine.Memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.Timer));
    }

    public static void CapturesDebuggerRingTrace()
    {
        using var machine = new Machine();
        var instruction = TestFixtures.MovImmediate(1, 42);
        machine.Memory.LoadRomWords(TestFixtures.RomWith(instruction, instruction, instruction));
        var debugger = new MachineDebugger(machine);

        TestAssertions.Throws<ArgumentOutOfRangeException>(() => debugger.EnableTrace(-1));
        debugger.EnableTrace(2);
        debugger.Run(3);

        TestAssertions.Equal(2, debugger.Trace.Count);
        TestAssertions.Equal(1ul, debugger.Trace.First().Step);
        TestAssertions.Equal(machine.Cpu.LastPcByte, debugger.Trace.Last().ProgramCounter);
        TestAssertions.Equal(instruction, debugger.Trace.Last().Instruction);
        debugger.ClearTrace();
        TestAssertions.Equal(0, debugger.Trace.Count);
    }

    public static void MachineRunReportsProgress()
    {
        using var machine = new Machine();
        var reports = new List<MachineRunProgress>();

        machine.Run(5, reports.Add, progressInterval: 2);

        TestAssertions.Equal(3, reports.Count);
        TestAssertions.Equal(2ul, reports[0].ExecutedSteps);
        TestAssertions.Equal(4ul, reports[1].ExecutedSteps);
        TestAssertions.Equal(5ul, reports[2].ExecutedSteps);
        TestAssertions.Equal(machine.Cpu.PcByte, reports[^1].ProgramCounter);
        TestAssertions.Throws<ArgumentOutOfRangeException>(() =>
            machine.Run(1, reports.Add, progressInterval: 0));
    }

    public static void DisassemblerAndBreakpoint()
    {
        TestAssertions.Equal("BT 337", Risc5Disassembler.Format(0xe700_0151u));
        TestAssertions.Equal("MOV r1, 0x0007", Risc5Disassembler.Format(TestFixtures.MovImmediate(1, 7)));
        TestAssertions.Equal("LDR.B r3, [r1, 32]", Risc5Disassembler.Format(TestFixtures.MemoryOp(false, true, 3, 1, 0x20)));

        var machine = new Machine();
        machine.Memory.LoadRomWords(TestFixtures.RomWith(TestFixtures.MovImmediate(1, 42)));
        var debugger = new MachineDebugger(machine);
        var resetPc = (Risc5Cpu.ResetPc << 2) & Memory.AddressMask;
        debugger.AddBreakpoint(resetPc);
        TestAssertions.Equal(0ul, debugger.Run(10));
        TestAssertions.Equal(resetPc, machine.Cpu.PcByte);
        TestAssertions.True(debugger.RemoveBreakpoint(resetPc));
        TestAssertions.Equal(1ul, debugger.Run(1));
        TestAssertions.Equal(42u, machine.Cpu.Register(1));
    }
}
