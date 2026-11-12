internal static partial class OberonDiskFormatTests
{
    public static void Run()
    {
        MapsLogicalSectorsToPhysicalSectors();
        RejectsInvalidLogicalAddresses();
        RoundTripsFileHeaderAndExtensions();
        RoundTripsEmptyFileHeader();
        RejectsInvalidFileHeader();
        RejectsMissingFilePages();
        RoundTripsDirectoryPage();
        RejectsInvalidDirectoryPageShape();
    }
}
