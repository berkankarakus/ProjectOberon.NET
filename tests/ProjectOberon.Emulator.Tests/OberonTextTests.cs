internal static partial class OberonTextTests
{
    public static void Run()
    {
        EditsAndReadsText();
        ValidatesTextPositions();
        PreservesDeletedTextInBuffer();
        AppliesStylesAndNotifies();
        ScansOberonTokens();
        WritesFormattedText();
        WritesNumericBoundaries();
    }
}
