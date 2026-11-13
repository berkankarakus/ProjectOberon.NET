namespace ProjectOberon.Compiler;

internal sealed class OberonCompilerNamedCollection<T>
    where T : class
{
    private readonly Func<T, string> _nameSelector;
    private readonly List<T> _items = [];
    private readonly Dictionary<string, T> _itemsByName = new(StringComparer.Ordinal);

    public OberonCompilerNamedCollection(Func<T, string> nameSelector)
    {
        ArgumentNullException.ThrowIfNull(nameSelector);
        _nameSelector = nameSelector;
    }

    public IReadOnlyList<T> Items => _items;

    public bool TryAdd(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!_itemsByName.TryAdd(_nameSelector(item), item))
        {
            return false;
        }

        _items.Add(item);
        return true;
    }

    public T? Find(string name)
    {
        return _itemsByName.TryGetValue(name, out var item) ? item : null;
    }
}
