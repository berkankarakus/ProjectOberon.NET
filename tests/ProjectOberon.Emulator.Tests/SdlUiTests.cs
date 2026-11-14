using ProjectOberon.SdlUi;

internal static partial class SdlUiTests
{
    public static void Run()
    {
        ParsesUiOptions();
        HandlesUiHelp();
        RejectsInvalidUiOptions();
        EncodesKeyboardEvents();
        ScalesMouseCoordinates();
        RendersFramebufferPixels();
        FormatsWindowTitle();
        RendersModernEditorCaret();
        EditsAndSavesHostText();
        ScrollsLongHostText();
        RoutesEditorEventsThroughOberonMessages();
        LogsRuntimeErrors();
    }

    private static void LogsRuntimeErrors()
    {
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-ui-log-{Guid.NewGuid():N}.log");
        try
        {
            using (var log = UiLog.Open(path))
            {
                log.WriteError(new InvalidOperationException("render failed"));
            }

            var content = File.ReadAllText(path);
            TestAssertions.True(content.Contains("error type=System.InvalidOperationException", StringComparison.Ordinal));
            TestAssertions.True(content.Contains("message=render failed", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
