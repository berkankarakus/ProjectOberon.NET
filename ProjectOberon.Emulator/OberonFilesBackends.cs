using System.Buffers.Binary;
using System.Text;

namespace ProjectOberon.Emulator;

internal interface IOberonFilesBackend
{
    OberonFile? Old(string name);
    OberonFile New(string name);
    void Register(OberonFile file);
    bool Delete(string name);
    bool Rename(string oldName, string newName);
    void Purge(OberonFile file);
    void Close(OberonFile file);
}

internal sealed class ReadOnlyOberonFilesBackend : IOberonFilesBackend
{
    private readonly OberonFileSystem _fileSystem;

    public ReadOnlyOberonFilesBackend(OberonFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public OberonFile? Old(string name)
    {
        var entry = _fileSystem.Enumerate().FirstOrDefault(item => item.Name == name);
        if (entry is null)
        {
            return null;
        }

        var date = _fileSystem.ReadHeader(entry.HeaderAddress).Date;
        return new OberonFile(entry.Name, _fileSystem.ReadFile(entry), writable: false, date);
    }

    public OberonFile New(string name)
    {
        return new OberonFile(name, ReadOnlySpan<byte>.Empty, writable: true);
    }

    public void Register(OberonFile file)
    {
        throw PersistentOnly();
    }

    public bool Delete(string name)
    {
        throw PersistentOnly();
    }

    public bool Rename(string oldName, string newName)
    {
        throw PersistentOnly();
    }

    public void Purge(OberonFile file)
    {
        if (file.HeaderAddress != 0)
        {
            throw PersistentOnly();
        }
    }

    public void Close(OberonFile file)
    {
    }

    private static InvalidOperationException PersistentOnly()
    {
        return new InvalidOperationException("persistent file operations require a Kernel-backed Files instance");
    }
}

internal sealed class PersistentOberonFilesBackend : IOberonFilesBackend
{
    private readonly OberonKernel _kernel;
    private readonly OberonFileDirectory _directory;

    public PersistentOberonFilesBackend(OberonKernel kernel, bool initializeDirectory)
    {
        _kernel = kernel;
        _directory = new OberonFileDirectory(kernel);
        if (initializeDirectory) _directory.Initialize();
        else _directory.SyncSectorMap();
    }

    public OberonFile? Old(string name)
    {
        var address = _directory.Search(name);
        return address == 0 ? null : ReadPersisted(name, address);
    }

    public OberonFile New(string name)
    {
        return new OberonFile(name, ReadOnlySpan<byte>.Empty, writable: true, _kernel.Clock);
    }

    public void Register(OberonFile file)
    {
        var totalLength = checked(OberonFileHeader.HeaderSize + file.Length);
        var fullPages = (totalLength - 1) / OberonDiskLayout.SectorLength;
        var lastPageLength = totalLength - fullPages * OberonDiskLayout.SectorLength;
        var pageAddresses = AllocatePages(fullPages + 1);
        var extensionAddresses = AllocateExtensions(fullPages, pageAddresses);
        var header = new byte[OberonDiskLayout.SectorLength];
        OberonFileHeaderCodec.Write(header, file.Name, fullPages, lastPageLength, file.Date, extensionAddresses, pageAddresses);
        var content = file.Content;
        var firstPageLength = Math.Min(content.Length, OberonDiskLayout.SectorLength - OberonFileHeader.HeaderSize);
        content[..firstPageLength].CopyTo(header.AsSpan(OberonFileHeader.HeaderSize));
        _kernel.PutSector(pageAddresses[0], header);

        var copied = firstPageLength;
        for (var page = 1; page < pageAddresses.Length; page++)
        {
            var data = new byte[OberonDiskLayout.SectorLength];
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
        var headerAddress = _directory.Search(name);
        if (headerAddress == 0)
        {
            return false;
        }
        var header = _kernel.GetSector(headerAddress);
        var parsedHeader = OberonFileHeaderCodec.Read(header, headerAddress);
        var pages = OberonFileHeaderCodec.ReadPageAddresses(parsedHeader, _kernel.GetSector);
        var extensionCount = (parsedHeader.AllocatedPages + (256 - 64)) / 256;
        if (!_directory.Delete(name, out _))
        {
            return false;
        }

        foreach (var page in pages)
        {
            _kernel.FreeSector(page);
        }

        for (var index = 0; index < extensionCount; index++)
        {
            _kernel.FreeSector(parsedHeader.ExtensionAddresses[index]);
        }
        _kernel.FreeSector(headerAddress);
        return true;
    }

    public bool Rename(string oldName, string newName)
    {
        var address = _directory.Search(oldName);
        if (address == 0)
        {
            return false;
        }
        var header = _kernel.GetSector(address);
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

    public void Close(OberonFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
    }

    private int[] AllocatePages(int count)
    {
        var pages = new int[count];
        var hint = 0;
        for (var index = 0; index < pages.Length; index++)
        {
            pages[index] = _kernel.AllocSector(hint);
            hint = pages[index];
        }
        return pages;
    }

    private int[] AllocateExtensions(int fullPages, int[] pages)
    {
        var count = (fullPages + (256 - 64)) / 256;
        var extensions = new int[Math.Max(0, count)];
        var hint = pages[^1];
        for (var index = 0; index < extensions.Length; index++)
        {
            extensions[index] = _kernel.AllocSector(hint);
            hint = extensions[index];
            var data = new byte[OberonDiskLayout.SectorLength];
            for (var entry = 0; entry < 256; entry++)
            {
                var pageIndex = 64 + index * 256 + entry;
                if (pageIndex >= pages.Length)
                {
                    break;
                }
                BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(entry * 4), pages[pageIndex]);
            }
            _kernel.PutSector(extensions[index], data);
        }
        return extensions;
    }

    private OberonFile ReadPersisted(string name, int headerAddress)
    {
        var header = _kernel.GetSector(headerAddress);
        var parsedHeader = OberonFileHeaderCodec.Read(header, headerAddress);
        var pages = OberonFileHeaderCodec.ReadPageAddresses(parsedHeader, _kernel.GetSector);
        var content = new byte[parsedHeader.ContentLength];
        var copied = Math.Min(content.Length, OberonDiskLayout.SectorLength - OberonFileHeader.HeaderSize);
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
}
