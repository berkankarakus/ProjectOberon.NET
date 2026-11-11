internal static partial class RscModuleTests
{
    public static void Run()
    {
        ParsesCompleteModule();
        RejectsUnsupportedVersion();
        RejectsTruncatedModule();
        RejectsNegativeVariableSpace();
        RejectsNegativeStringSpace();
        RejectsNegativeCodeWordCount();
        RejectsNegativeEntryCount();
        RejectsInvalidTrailer();
        RejectsInvalidCodeLayout();
        RejectsNegativeSize();
        RejectsDuplicateNames();
        RejectsInvalidEntriesAndFixups();
        WritesRoundTripModule();
        RejectsMismatchedCodeCount();
        RejectsMismatchedPayloadCount();
        RejectsInvalidCodeOffsets();
    }
}
