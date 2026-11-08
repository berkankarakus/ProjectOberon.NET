using System.Runtime.InteropServices;

namespace ProjectOberon.SdlUi;

internal interface ITextClipboard
{
    string GetText();

    void SetText(string text);
}

internal sealed class SdlTextClipboard : ITextClipboard
{
    public string GetText()
    {
        return SdlNative.GetClipboardText();
    }

    public void SetText(string text)
    {
        SdlNative.SetClipboardText(text);
    }
}
