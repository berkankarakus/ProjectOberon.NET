internal static partial class OberonTextTests
{
    public static void Run()
    {
        EditsAndReadsText();
        PreservesDeletedTextInBuffer();
        AppliesStylesAndNotifies();
        ScansOberonTokens();
        WritesFormattedText();
        WritesNumericBoundaries();
    }
}
