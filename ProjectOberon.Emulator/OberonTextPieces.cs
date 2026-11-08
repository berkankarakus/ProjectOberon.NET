namespace ProjectOberon.Emulator;

public sealed partial class OberonText
{
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
