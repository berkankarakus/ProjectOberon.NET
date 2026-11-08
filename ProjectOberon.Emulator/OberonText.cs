namespace ProjectOberon.Emulator;

public sealed partial class OberonText
{
    private readonly List<OberonTextPiece> _pieces = [];

    public OberonText(string text = "", OberonTextStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        AppendInitial(text, style ?? OberonTextStyle.Default);
    }

    public int Length { get; private set; }

    public bool Changed { get; private set; }

    public Action<OberonText, OberonTextOperation, int, int>? Notify { get; set; }

    internal IReadOnlyList<OberonTextPiece> Pieces => _pieces;

    internal void MarkClean() => Changed = false;

    public string GetText()
    {
        return string.Concat(_pieces.Select(piece => piece.Text));
    }

    public OberonTextReader OpenReader(int position = 0)
    {
        ValidatePosition(position, allowEnd: true);
        return new OberonTextReader(this, position);
    }

    public OberonTextScanner OpenScanner(int position = 0)
    {
        ValidatePosition(position, allowEnd: true);
        return new OberonTextScanner(GetText(), position);
    }

    public OberonTextStyle Attributes(int position)
    {
        ValidatePosition(position, allowEnd: false);
        return FindPiece(position).Style;
    }

    public OberonTextBuffer Copy(int begin, int end)
    {
        ValidateRange(begin, end);
        var buffer = new OberonTextBuffer();
        CopyRange(begin, end, buffer);
        return buffer;
    }

    internal bool TryRead(int position, out char character, out OberonTextStyle style)
    {
        if (position >= Length)
        {
            character = '\0';
            style = OberonTextStyle.Default;
            return false;
        }

        var piece = FindPiece(position);
        var offset = position - PieceStart(position);
        character = piece.Text[offset];
        style = piece.Style;
        return true;
    }

}
