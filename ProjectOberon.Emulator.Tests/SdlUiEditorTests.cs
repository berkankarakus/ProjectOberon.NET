using System.Text;
using ProjectOberon.Emulator;
using ProjectOberon.SdlUi;

internal static partial class SdlUiTests
{
    private static void EditsAndSavesHostText()
    {
        var textPath = Path.GetTempFileName();
        var fontPath = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(textPath, Encoding.Latin1.GetBytes("AB"));
            File.WriteAllBytes(fontPath, BuildFont());
            var session = EditorSession.Load(textPath, fontPath);

            session.InsertText("C");
            TestAssertions.Equal("CAB", session.Editor.Text.GetText());
            session.HandleKey(8, 0);
            TestAssertions.Equal("AB", session.Editor.Text.GetText());
            session.HandleKey(0x4000004f, 0);
            session.HandleKey('s', SdlNative.KeymodCtrl);
            TestAssertions.False(session.IsDirty);
            TestAssertions.Equal("AB", Encoding.Latin1.GetString(File.ReadAllBytes(textPath)));

            var pixels = new uint[Display.Width * Display.Height];
            TestAssertions.True(session.Render(pixels, flipY: false) > 0);
        }
        finally
        {
            File.Delete(textPath);
            File.Delete(fontPath);
        }
    }

    private static void ScrollsLongHostText()
    {
        var textPath = Path.GetTempFileName();
        var fontPath = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(textPath, Encoding.Latin1.GetBytes(string.Join('\r', Enumerable.Repeat("A", 100))));
            File.WriteAllBytes(fontPath, BuildFont());
            var session = EditorSession.Load(textPath, fontPath);

            TestAssertions.Equal(0, session.ScrollLine);
            TestAssertions.True(session.HandleKey(0x4000004e, 0));
            TestAssertions.Equal(1, session.ScrollLine);
            TestAssertions.True(session.HandleKey(0x4000004b, 0));
            TestAssertions.Equal(0, session.ScrollLine);
        }
        finally
        {
            File.Delete(textPath);
            File.Delete(fontPath);
        }
    }

    private static void RoutesEditorEventsThroughOberonMessages()
    {
        var textPath = Path.GetTempFileName();
        var fontPath = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(textPath, Encoding.Latin1.GetBytes("AB"));
            File.WriteAllBytes(fontPath, BuildFont());
            var session = EditorSession.Load(textPath, fontPath);

            session.InsertText("C");
            TestAssertions.Equal("CAB", session.Editor.Text.GetText());
            TestAssertions.True(session.HandleKey('z', SdlNative.KeymodCtrl));
            TestAssertions.Equal("AB", session.Editor.Text.GetText());
            TestAssertions.True(session.HandleKey('y', SdlNative.KeymodCtrl));
            TestAssertions.Equal("CAB", session.Editor.Text.GetText());
            TestAssertions.True(session.HandleKey(0x4000004f, 0));
            TestAssertions.True(session.HandleMouseButton(true, 1, 0, 0, 100, 100, flipY: false));
            TestAssertions.False(session.HandleKey('?', 0));
        }
        finally
        {
            File.Delete(textPath);
            File.Delete(fontPath);
        }
    }

}
