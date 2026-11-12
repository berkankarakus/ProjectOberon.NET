using ProjectOberon.Emulator;

internal static class MemoryTests
{
    public static void Run()
    {
        LoadsRomTextWithComments();
        RejectsInvalidRomFiles();
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

            TestAssertions.Throws<InvalidDataException>(() => memory.LoadRomFile(path));
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
}
