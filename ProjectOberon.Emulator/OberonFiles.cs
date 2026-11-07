using System.Buffers.Binary;
using System.Text;
using System.Numerics;

namespace ProjectOberon.Emulator;

public sealed class OberonFiles
{
    private readonly OberonFileSystem? _fileSystem;
    private readonly OberonKernel? _kernel;
    private readonly OberonFileDirectory? _directory;

    public OberonFiles(OberonFileSystem fileSystem) => _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    public OberonFiles(OberonKernel kernel, bool initializeDirectory = false)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _directory = new OberonFileDirectory(kernel);
        if (initializeDirectory) _directory.Initialize();
        else _directory.SyncSectorMap();
    }

    public OberonFile? Old(string name)
    {
        ValidateName(name);
        if (_fileSystem is not null)
        {
            var entry = _fileSystem.Enumerate().FirstOrDefault(item => item.Name == name);
            return entry is null ? null : new OberonFile(entry.Name, _fileSystem.ReadFile(entry), writable: false, _fileSystem.ReadHeader(entry.HeaderAddress).Date);
        }

        var address = _directory!.Search(name);
        return address == 0 ? null : ReadPersisted(name, address);
    }

    public OberonFile New(string name) => new(name, ReadOnlySpan<byte>.Empty, writable: true, _kernel?.Clock ?? 0);

    public void Register(OberonFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (_kernel is null || _directory is null) throw new InvalidOperationException("persistent file operations require a Kernel-backed Files instance");
        var totalLength = checked(OberonFileHeader.HeaderSize + file.Length);
        var fullPages = (totalLength - 1) / OberonFileSystem.SectorLength;
        var lastPageLength = totalLength - fullPages * OberonFileSystem.SectorLength;
        var pageAddresses = new int[fullPages + 1];
        var hint = 0;
        for (var index = 0; index < pageAddresses.Length; index++)
        {
            pageAddresses[index] = _kernel.AllocSector(hint);
            hint = pageAddresses[index];
        }

        var extensionCount = (fullPages + (256 - 64)) / 256;
        var extensionAddresses = new int[Math.Max(0, extensionCount)];
        for (var index = 0; index < extensionAddresses.Length; index++)
        {
            extensionAddresses[index] = _kernel.AllocSector(hint);
            hint = extensionAddresses[index];
            var extension = new byte[OberonFileSystem.SectorLength];
            for (var entry = 0; entry < 256; entry++)
            {
                var pageIndex = 64 + index * 256 + entry;
                if (pageIndex >= pageAddresses.Length) break;
                BinaryPrimitives.WriteInt32LittleEndian(extension.AsSpan(entry * 4), pageAddresses[pageIndex]);
            }
            _kernel.PutSector(extensionAddresses[index], extension);
        }

        var header = new byte[OberonFileSystem.SectorLength];
        OberonFileHeaderCodec.Write(header, file.Name, fullPages, lastPageLength, file.Date, extensionAddresses, pageAddresses);
        var content = file.Content;
        var firstPageLength = Math.Min(content.Length, OberonFileSystem.SectorLength - OberonFileHeader.HeaderSize);
        content[..firstPageLength].CopyTo(header.AsSpan(OberonFileHeader.HeaderSize));
        _kernel.PutSector(pageAddresses[0], header);

        var copied = firstPageLength;
        for (var page = 1; page < pageAddresses.Length; page++)
        {
            var data = new byte[OberonFileSystem.SectorLength];
            var count = Math.Min(data.Length, content.Length - copied);
            content.Slice(copied, count).CopyTo(data);
            copied += count;
            _kernel.PutSector(pageAddresses[page], data);
        }
        _directory.Insert(file.Name, pageAddresses[0]);
        file.MarkRegistered(pageAddresses[0]);
    }

    public bool Delete(string name)
    {
        OberonFiles.ValidateName(name);
        if (_kernel is null || _directory is null) throw new InvalidOperationException("persistent file operations require a Kernel-backed Files instance");
        var headerAddress = _directory.Search(name);
        if (headerAddress == 0) return false;
        var header = _kernel.GetSector(headerAddress);
        var parsedHeader = OberonFileHeaderCodec.Read(header, headerAddress);
        var pages = OberonFileHeaderCodec.ReadPageAddresses(parsedHeader, _kernel.GetSector);
        var extensionCount = (parsedHeader.AllocatedPages + (256 - 64)) / 256;
        if (!_directory.Delete(name, out _)) return false;
        foreach (var page in pages) _kernel.FreeSector(page);
        for (var index = 0; index < extensionCount; index++) _kernel.FreeSector(parsedHeader.ExtensionAddresses[index]);
        _kernel.FreeSector(headerAddress);
        return true;
    }

    public bool Rename(string oldName, string newName)
    {
        if (_directory is null) throw new InvalidOperationException("persistent file operations require a Kernel-backed Files instance");
        OberonFiles.ValidateName(oldName);
        OberonFiles.ValidateName(newName);
        var address = _directory.Search(oldName);
        if (address == 0) return false;
        var header = _kernel!.GetSector(address);
        header.AsSpan(4, 32).Clear();
        Encoding.ASCII.GetBytes(newName).CopyTo(header, 4);
        _kernel.PutSector(address, header);
        return _directory.Rename(oldName, newName);
    }

    public void Purge(OberonFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.HeaderAddress != 0) Delete(file.Name);
    }

    public void Close(OberonFile file) => ArgumentNullException.ThrowIfNull(file);

    private OberonFile ReadPersisted(string name, int headerAddress)
    {
        var header = _kernel!.GetSector(headerAddress);
        var parsedHeader = OberonFileHeaderCodec.Read(header, headerAddress);
        var contentLength = parsedHeader.ContentLength;
        var pages = OberonFileHeaderCodec.ReadPageAddresses(parsedHeader, _kernel.GetSector);
        var content = new byte[contentLength];
        var copied = Math.Min(contentLength, OberonFileSystem.SectorLength - OberonFileHeader.HeaderSize);
        header.AsSpan(OberonFileHeader.HeaderSize, copied).CopyTo(content);
        var offset = copied;
        for (var page = 1; page < pages.Length && offset < content.Length; page++)
        {
            var data = _kernel.GetSector(pages[page]);
            var count = Math.Min(data.Length, content.Length - offset);
            data.AsSpan(0, count).CopyTo(content.AsSpan(offset));
            offset += count;
        }
        var file = new OberonFile(name, content, writable: false, parsedHeader.Date);
        file.MarkRegistered(headerAddress);
        return file;
    }

    public static void ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length >= 32 || !char.IsAsciiLetter(name[0]) ||
            name.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '.')))
            throw new ArgumentException($"invalid Oberon file name: {name}", nameof(name));
    }
}

public sealed class OberonFile
{
    private byte[] _content;
    private readonly bool _writable;

    public OberonFile(string name, ReadOnlySpan<byte> content, bool writable = true, int date = 0)
    {
        OberonFiles.ValidateName(name);
        Name = name;
        Date = date;
        _content = content.ToArray();
        _writable = writable;
    }

    public string Name { get; }
    public int Date { get; }
    public int Length => _content.Length;
    public bool Writable => _writable;

    public OberonRider Set(int position = 0)
    {
        var clamped = Math.Clamp(position, 0, Length);
        return new OberonRider(this, clamped);
    }

    internal byte ReadByte(int position) => _content[position];

    internal void WriteByte(int position, byte value)
    {
        if (!_writable) throw new InvalidOperationException("Oberon file is read-only");
        if (position < 0) throw new ArgumentOutOfRangeException(nameof(position));
        if (position >= _content.Length) Array.Resize(ref _content, position + 1);
        _content[position] = value;
    }

    internal ReadOnlySpan<byte> Content => _content;
    internal void MarkRegistered(int headerAddress) => HeaderAddress = headerAddress;
    internal int HeaderAddress { get; private set; }
}

public sealed class OberonRider
{
    private readonly OberonFile _file;

    internal OberonRider(OberonFile file, int position)
    {
        _file = file;
        Position = position;
    }

    public bool Eof { get; private set; }
    public int Result { get; private set; }
    public int Position { get; private set; }
    public OberonFile Base => _file;

    public bool ReadByte(out byte value)
    {
        if (Position >= _file.Length)
        {
            value = 0;
            Eof = true;
            return false;
        }

        value = _file.ReadByte(Position++);
        return true;
    }

    public int ReadBytes(Span<byte> destination)
    {
        var count = Math.Min(destination.Length, _file.Length - Position);
        for (var index = 0; index < count; index++) destination[index] = _file.ReadByte(Position++);
        if (count != destination.Length) Eof = true;
        return count;
    }

    public bool Read(out char character)
    {
        var ok = ReadByte(out var value);
        character = (char)value;
        return ok;
    }

    public bool ReadInt(out int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        if (ReadBytes(bytes) != bytes.Length)
        {
            value = 0;
            return false;
        }
        value = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        return true;
    }

    public string ReadString()
    {
        var chars = new List<char>();
        while (Read(out var character) && character != '\0') chars.Add(character);
        return new string(chars.ToArray());
    }

    public bool ReadNum(out int value)
    {
        var bits = 32;
        uint packed = 0;
        if (!ReadByte(out var current))
        {
            value = 0;
            return false;
        }
        while (current >= 0x80)
        {
            packed = BitOperations.RotateRight(packed + (uint)(current - 0x80), 7);
            bits -= 7;
            if (!ReadByte(out current))
            {
                value = 0;
                return false;
            }
        }

        value = bits <= 4
            ? (int)BitOperations.RotateRight(packed + (uint)(current & 0x0f), 4)
            : (int)BitOperations.RotateRight(packed + current, 7) >> (bits - 7);
        return true;
    }

    public void WriteByte(byte value)
    {
        _file.WriteByte(Position, value);
        Position++;
    }

    public void Write(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes) WriteByte(value);
    }

    public void Write(char character) => WriteByte((byte)character);

    public void WriteInt(int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        Write(bytes);
    }

    public void WriteString(string value)
    {
        foreach (var character in value) Write(character);
        Write('\0');
    }

    public void WriteNum(int value)
    {
        while (value < -0x40 || value >= 0x40)
        {
            WriteByte((byte)((value & 0x7f) | 0x80));
            value >>= 7;
        }
        WriteByte((byte)(value & 0x7f));
    }
}
