namespace ProjectOberon.Emulator;

public sealed partial class OberonText
{
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

    public void Append(OberonTextBuffer buffer)
    {
        Insert(Length, buffer);
    }

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

    private void MarkChanged(OberonTextOperation operation, int begin, int end)
    {
        Changed = true;
        Notify?.Invoke(this, operation, begin, end);
    }
}
