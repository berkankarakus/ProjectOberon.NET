using System.Text;

namespace ProjectOberon.SdlUi;

internal sealed partial class EditorSession
{
    public void Save()
    {
        File.WriteAllBytes(Path, Encoding.Latin1.GetBytes(Editor.Text.GetText()));
        Editor.Text.ClearChanged();
    }
}
