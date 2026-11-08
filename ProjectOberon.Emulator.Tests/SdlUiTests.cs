internal static partial class SdlUiTests
{
    public static void Run()
    {
        ParsesUiOptions();
        RejectsInvalidUiOptions();
        EncodesKeyboardEvents();
        ScalesMouseCoordinates();
        RendersFramebufferPixels();
        FormatsWindowTitle();
        EditsAndSavesHostText();
        ScrollsLongHostText();
        RoutesEditorEventsThroughOberonMessages();
    }
}
