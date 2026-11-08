namespace ProjectOberon.Emulator;

public sealed class OberonText
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

    public void Insert(int position, OberonTextBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ValidatePosition(position, allowEnd: true);
        if (buffer.Length == 0)
        {
            return;
        }

        var index = SplitAt(position);
        _pieces.InsertRange(index, ClonePieces(buffer.Pieces));
        Length += buffer.Length;
        MergeAdjacentPieces();
        MarkChanged(OberonTextOperation.Insert, position, position + buffer.Length);
    }

    public void Append(OberonTextBuffer buffer) => Insert(Length, buffer);

    public OberonTextBuffer Delete(int begin, int end)
    {
        ValidateRange(begin, end);
        var buffer = Copy(begin, end);
        if (begin == end)
        {
            return buffer;
        }

        var start = SplitAt(begin);
        var finish = SplitAt(end);
        _pieces.RemoveRange(start, finish - start);
        Length -= end - begin;
        MergeAdjacentPieces();
        MarkChanged(OberonTextOperation.Delete, begin, end);
        return buffer;
    }

    public void ChangeLooks(int begin, int end, OberonTextStyle style)
    {
        ValidateRange(begin, end);
        if (begin == end)
        {
            return;
        }

        var start = SplitAt(begin);
        var finish = SplitAt(end);
        for (var index = start; index < finish; index++)
        {
            _pieces[index].Style = style;
        }

        MergeAdjacentPieces();
        MarkChanged(OberonTextOperation.Replace, begin, end);
    }

    public void ClearChanged()
    {
        Changed = false;
        Notify?.Invoke(this, OberonTextOperation.Unmark, 0, 0);
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

    private void AppendInitial(string text, OberonTextStyle style)
    {
        if (text.Length == 0)
        {
            return;
        }

        _pieces.Add(new OberonTextPiece(text, style));
        Length = text.Length;
    }

    private void CopyRange(int begin, int end, OberonTextBuffer buffer)
    {
        var position = 0;
        foreach (var piece in _pieces)
        {
            var pieceEnd = position + piece.Text.Length;
            var overlapBegin = Math.Max(begin, position);
            var overlapEnd = Math.Min(end, pieceEnd);
            if (overlapBegin < overlapEnd)
            {
                buffer.Append(piece.Text.Substring(overlapBegin - position, overlapEnd - overlapBegin), piece.Style);
            }
            position = pieceEnd;
            if (position >= end)
            {
                break;
            }
        }
    }

    private int SplitAt(int position)
    {
        if (position == Length)
        {
            return _pieces.Count;
        }

        var offset = 0;
        for (var index = 0; index < _pieces.Count; index++)
        {
            var piece = _pieces[index];
            var next = offset + piece.Text.Length;
            if (position == offset)
            {
                return index;
            }
            if (position < next)
            {
                var local = position - offset;
                _pieces[index] = new OberonTextPiece(piece.Text[..local], piece.Style);
                _pieces.Insert(index + 1, new OberonTextPiece(piece.Text[local..], piece.Style));
                return index + 1;
            }
            offset = next;
        }

        return _pieces.Count;
    }

    private OberonTextPiece FindPiece(int position)
    {
        var offset = 0;
        foreach (var piece in _pieces)
        {
            if (position < offset + piece.Text.Length)
            {
                return piece;
            }
            offset += piece.Text.Length;
        }

        throw new ArgumentOutOfRangeException(nameof(position));
    }

    private int PieceStart(int position)
    {
        var offset = 0;
        foreach (var piece in _pieces)
        {
            if (position < offset + piece.Text.Length)
            {
                return offset;
            }
            offset += piece.Text.Length;
        }

        throw new ArgumentOutOfRangeException(nameof(position));
    }

    private void MergeAdjacentPieces()
    {
        for (var index = _pieces.Count - 1; index > 0; index--)
        {
            if (_pieces[index - 1].Style != _pieces[index].Style)
            {
                continue;
            }

            _pieces[index - 1].Text += _pieces[index].Text;
            _pieces.RemoveAt(index);
        }
    }

    private void MarkChanged(OberonTextOperation operation, int begin, int end)
    {
        Changed = true;
        Notify?.Invoke(this, operation, begin, end);
    }

    private void ValidateRange(int begin, int end)
    {
        ValidatePosition(begin, allowEnd: true);
        if (end < begin || end > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(end));
        }
    }

    private void ValidatePosition(int position, bool allowEnd)
    {
        var max = allowEnd ? Length : Length - 1;
        if (position < 0 || position > max)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }
    }

    private static IEnumerable<OberonTextPiece> ClonePieces(IReadOnlyList<OberonTextPiece> pieces)
    {
        return pieces.Select(piece => new OberonTextPiece(piece.Text, piece.Style));
    }
}
