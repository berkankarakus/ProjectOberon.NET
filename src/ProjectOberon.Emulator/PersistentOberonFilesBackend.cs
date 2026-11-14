using System.Buffers.Binary;
using System.Text;

namespace ProjectOberon.Emulator;

internal sealed class PersistentOberonFilesBackend : IOberonFilesBackend
{
    private readonly OberonKernel _kernel;
    private readonly OberonFileDirectory _directory;

    public PersistentOberonFilesBackend(OberonKernel kernel, bool initializeDirectory)
    {
        _kernel = kernel;
        _directory = new OberonFileDirectory(kernel);
        if (initializeDirectory)
        {
            _directory.Initialize();
        }
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
        var pageAddresses = Array.Empty<int>();
        var extensionAddresses = Array.Empty<int>();
        try
        {
            pageAddresses = AllocatePages(fullPages + 1);
            extensionAddresses = AllocateExtensions(fullPages, pageAddresses);
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
        catch
        {
            FreeAllocated(extensionAddresses);
            FreeAllocated(pageAddresses);
            throw;
        }
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
        if (oldName == newName)
        {
            return true;
        }

        if (_directory.Search(newName) != 0)
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
        if (file.HeaderAddress != 0)
        {
            Delete(file.Name);
        }
    }

    public void Close(OberonFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
    }

    private int[] AllocatePages(int count)
    {
        var pages = new int[count];
        var hint = 0;
        try
        {
            for (var index = 0; index < pages.Length; index++)
            {
                pages[index] = _kernel.AllocSector(hint);
                hint = pages[index];
            }
        }
        catch
        {
            FreeAllocated(pages);
            throw;
        }
        return pages;
    }

    private int[] AllocateExtensions(int fullPages, int[] pages)
    {
        var count = (fullPages + (256 - 64)) / 256;
        var extensions = new int[Math.Max(0, count)];
        var hint = pages[^1];
        try
        {
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
        }
        catch
        {
            FreeAllocated(extensions);
            throw;
        }
        return extensions;
    }

    private void FreeAllocated(IEnumerable<int> addresses)
    {
        foreach (var address in addresses)
        {
            if (address != 0)
            {
                _kernel.FreeSector(address);
            }
        }
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
