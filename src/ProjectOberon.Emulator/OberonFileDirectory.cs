using Page = ProjectOberon.Emulator.OberonDirectoryPage;

namespace ProjectOberon.Emulator;

public sealed partial class OberonFileDirectory
{
    public const int RootAddress = OberonDiskLayout.DirectoryRootAddress;
    public const int PageSize = OberonDirectoryPageCodec.PageSize;
    public const int MaxEntries = OberonDirectoryPageCodec.MaxEntries;
    private const int MinEntries = MaxEntries / 2;
    private const int DirectoryMark = OberonDirectoryPageCodec.DirectoryMark;

    private readonly OberonKernel _kernel;

    public OberonFileDirectory(OberonKernel kernel) => _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));

    public void Initialize()
    {
        _kernel.InitSectorMap();
        var root = new Page();
        WritePage(RootAddress, root);
    }

    public void SyncSectorMap()
    {
        _kernel.InitSectorMap();
        var files = new List<int>();
        TraverseAndReserve(RootAddress, files, new HashSet<int>());
        foreach (var headerAddress in files)
        {
            ReserveFile(headerAddress);
        }
    }
}
