internal static partial class OberonDiskFormatTests
{
    public static void Run()
    {
        MapsLogicalSectorsToPhysicalSectors();
        RejectsInvalidLogicalAddresses();
        RoundTripsFileHeaderAndExtensions();
        RejectsInvalidFileHeader();
        RejectsMissingFilePages();
        RoundTripsDirectoryPage();
        RejectsInvalidDirectoryPageShape();
    }
}
