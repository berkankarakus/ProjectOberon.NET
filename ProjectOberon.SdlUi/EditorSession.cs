using System.Text;
using ProjectOberon.Emulator;

namespace ProjectOberon.SdlUi;

internal sealed partial class EditorSession
{
    public const int OriginX = 32;
    public const int OriginY = 32;
    public const int SurfaceWidth = Display.Width - OriginX * 2;
    public const int ViewportHeight = Display.Height - OriginY * 2;

    private readonly Memory _surfaceMemory = new();
    private readonly OberonDisplay _surfaceDisplay;
    private readonly OberonTextViewportController _viewport;
    private readonly OberonViewerSystem _inputSystem;
    private readonly OberonEventLoop _eventLoop;
    private readonly OberonViewer _inputViewer;
    private readonly ITextClipboard _clipboard;
    private bool _dragging;
    private bool _flipY;

    private EditorSession(string path, OberonTextEditor editor, ITextClipboard clipboard)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        Path = path;
        Editor = editor;
        _clipboard = clipboard;
        _viewport = new OberonTextViewportController(Editor);
        _surfaceDisplay = new OberonDisplay(_surfaceMemory);
        _inputSystem = new OberonViewerSystem(Display.Width, Display.Height);
        _inputSystem.AddTrack(0, Display.Width);
        _eventLoop = new OberonEventLoop(_inputSystem);
        _inputViewer = new OberonViewer("host-editor", HandleInputMessage);
        _inputSystem.Open(_inputViewer, 0, Display.Height);
        _inputSystem.PassFocus(_inputViewer);
    }

    public string Path { get; }
    public OberonTextEditor Editor { get; }
    public bool IsDirty => Editor.Text.Changed;
    public int ScrollLine => _viewport.ScrollLine;

    public static EditorSession Load(string path, string fontPath)
    {
        return Load(path, fontPath, new SdlTextClipboard());
    }

    internal static EditorSession Load(string path, string fontPath, ITextClipboard clipboard)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(fontPath);
        ArgumentNullException.ThrowIfNull(clipboard);

        var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
        var font = OberonFont.Load(fontPath);
        var frame = new OberonTextFrame(new OberonText(text), font);
        return new EditorSession(path, new OberonTextEditor(frame), clipboard);
    }

}
