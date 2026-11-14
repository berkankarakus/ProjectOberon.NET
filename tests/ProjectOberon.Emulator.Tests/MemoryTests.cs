using ProjectOberon.Emulator;

internal static class MemoryTests
{
    public static void Run()
    {
        LoadsRomTextWithComments();
        RejectsInvalidRomFiles();
        ReadsUnalignedRamWordsAcrossBoundaries();
        WritesUnalignedRamWordsAcrossBoundaries();
        CollectsOptionalWordAccessStatistics();
        ClearsRamRomAndDevices();
    }

    private static void LoadsRomTextWithComments()
    {
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-rom-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "12345678 # first word\n; ignored\nabcdef01\n");
        try
        {
            var memory = new Memory();
            memory.LoadRomFile(path);

            TestAssertions.Equal(0x1234_5678u, memory.Read32(Memory.RomBase));
            TestAssertions.Equal(0xabcd_ef01u, memory.Read32(Memory.RomBase + 4));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void RejectsInvalidRomFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-rom-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "12345678\nnot-hex\n");
        try
        {
            var memory = new Memory();
            var originalRom = new uint[Memory.RomWords];
            originalRom[0] = 0xdead_beefu;
            memory.LoadRomWords(originalRom);

            var error = TestAssertions.Throws<InvalidDataException>(() => memory.LoadRomFile(path));
            TestAssertions.True(error.Message.Contains("line 2", StringComparison.Ordinal));
            TestAssertions.Equal(0xdead_beefu, memory.Read32(Memory.RomBase));
            TestAssertions.Throws<ArgumentException>(() => new Memory().LoadRomWords([1u]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void ClearsRamRomAndDevices()
    {
        var memory = new Memory();
        memory.Write32(0x100, 0xfeed_beefu);
        memory.LoadRomWords(new uint[Memory.RomWords]);
        memory.Write32(DeviceBus.IoBase + (uint)MmioRegister.Timer, 99);

        memory.Clear();

        TestAssertions.Equal(0u, memory.Read32(0x100));
        TestAssertions.Equal(0u, memory.Read32(Memory.RomBase));
        TestAssertions.Equal(0u, memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.Timer));
    }

    private static void CollectsOptionalWordAccessStatistics()
    {
        var memory = new Memory();
        memory.CollectStatistics = true;
        memory.Write32(0x100, 0x1234_5678);
        _ = memory.Read32(0x100);
        _ = memory.Read32(Memory.RomBase);
        memory.Write32(Memory.RomBase, 1);
        _ = memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.Timer);
        memory.Write32(DeviceBus.IoBase + (uint)MmioRegister.Leds, 1);

        var statistics = memory.GetStatistics();
        TestAssertions.Equal(3ul, statistics.Read32Count);
        TestAssertions.Equal(3ul, statistics.Write32Count);
        TestAssertions.Equal(1ul, statistics.RomRead32Count);
        TestAssertions.Equal(1ul, statistics.MmioRead32Count);
        TestAssertions.Equal(1ul, statistics.RomWrite32Count);
        TestAssertions.Equal(1ul, statistics.MmioWrite32Count);

        memory.ResetStatistics();
        TestAssertions.Equal(0ul, memory.GetStatistics().Read32Count);
    }

    private static void ReadsUnalignedRamWordsAcrossBoundaries()
    {
        var memory = new Memory(8);
        memory.Write8(1, 0x11);
        memory.Write8(2, 0x22);
        memory.Write8(3, 0x33);
        memory.Write8(4, 0x44);

        TestAssertions.Equal(0x4433_2211u, memory.Read32(1));

        memory.Write8(6, 0x55);
        memory.Write8(7, 0x66);
        memory.Write8(0, 0x77);
        memory.Write8(1, 0x88);

        TestAssertions.Equal(0x8877_6655u, memory.Read32(6));
        TestAssertions.Equal(0x8877_6655u, memory.ReadInstruction(6));
    }

    private static void WritesUnalignedRamWordsAcrossBoundaries()
    {
        var memory = new Memory(8);

        memory.Write32(1, 0x4433_2211u);

        TestAssertions.Equal(0x11u, memory.Read8(1));
        TestAssertions.Equal(0x22u, memory.Read8(2));
        TestAssertions.Equal(0x33u, memory.Read8(3));
        TestAssertions.Equal(0x44u, memory.Read8(4));

        memory.Write32(6, 0x8877_6655u);

        TestAssertions.Equal(0x55u, memory.Read8(6));
        TestAssertions.Equal(0x66u, memory.Read8(7));
        TestAssertions.Equal(0x77u, memory.Read8(0));
        TestAssertions.Equal(0x88u, memory.Read8(1));
    }
}
