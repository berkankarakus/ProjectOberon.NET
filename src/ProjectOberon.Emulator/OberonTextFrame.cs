namespace ProjectOberon.Emulator;

public sealed partial class OberonTextFrame
{
    private bool _wrapLines = true;
    private string? _layoutSource;
    private int _layoutWidth;
    private IReadOnlyList<OberonTextLine>? _layoutCache;

    public OberonTextFrame(OberonText text, OberonFont font)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Font = font ?? throw new ArgumentNullException(nameof(font));
    }

    public OberonText Text { get; }

    public OberonFont Font { get; }

    public bool WrapLines
    {
        get => _wrapLines;
        set
        {
            if (_wrapLines == value)
            {
                return;
            }

            _wrapLines = value;
            _layoutCache = null;
        }
    }
}
