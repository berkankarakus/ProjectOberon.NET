namespace ProjectOberon.Emulator;

public sealed record OberonDirectoryEntry(string Name, int HeaderAddress);

public sealed record OberonFileHeader(
    string Name,
    int AllocatedPages,
    int LastPageLength,
    int Date,
    int[] ExtensionAddresses,
    int[] SectorAddresses)
{
    public const int HeaderSize = OberonFileHeaderCodec.HeaderSize;
    public int TotalLength => checked(AllocatedPages * OberonFileSystem.SectorLength + LastPageLength);
    public int ContentLength => Math.Max(0, TotalLength - HeaderSize);
}
