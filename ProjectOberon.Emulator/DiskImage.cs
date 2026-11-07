namespace ProjectOberon.Emulator;

public sealed class DiskImage : IDisposable
{
    public const int SectorSize = 512;
    private FileStream? _file;
    private uint _sectorCount;
    private bool _writable;

    public uint SectorCount => _sectorCount;
    public string? Path { get; private set; }
    public bool Writable => _writable;

    public void Open(string path, bool writable = true)
    {
        Close();
        var access = writable ? FileAccess.ReadWrite : FileAccess.Read;
        _file = new FileStream(path, FileMode.Open, access, FileShare.Read);
        if (_file.Length % SectorSize != 0 || _file.Length / SectorSize > uint.MaxValue)
        {
            Close();
            throw new InvalidDataException($"disk image size is not a whole number of 512-byte sectors: {path}");
        }
        _sectorCount = (uint)(_file.Length / SectorSize);
        _writable = writable;
        Path = path;
    }

    public byte[] Read(uint sector)
    {
        CheckSector(sector);
        var data = new byte[SectorSize];
        _file!.Seek((long)sector * SectorSize, SeekOrigin.Begin);
        _file.ReadExactly(data);
        return data;
    }

    public void Write(uint sector, ReadOnlySpan<byte> data)
    {
        CheckSector(sector);
        if (!_writable) throw new InvalidOperationException("disk image is read-only");
        if (data.Length != SectorSize) throw new ArgumentException($"sector must contain {SectorSize} bytes", nameof(data));
        _file!.Seek((long)sector * SectorSize, SeekOrigin.Begin);
        _file.Write(data);
        _file.Flush();
    }

    public void Close()
    {
        _file?.Dispose();
        _file = null;
        _sectorCount = 0;
        _writable = false;
        Path = null;
    }

    public void Dispose() => Close();

    private void CheckSector(uint sector)
    {
        if (_file is null) throw new InvalidOperationException("disk image is not open");
        if (sector >= _sectorCount) throw new ArgumentOutOfRangeException(nameof(sector));
    }
}
