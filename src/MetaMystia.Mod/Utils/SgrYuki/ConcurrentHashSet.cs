namespace SgrYuki.Utils;

public class ConcurrentHashSet<T>
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<T, byte> _dict = new();

    public bool Add(T item)
        => _dict.TryAdd(item, 0);

    public bool Remove(T item)
        => _dict.TryRemove(item, out _);

    public bool Contains(T item)
        => _dict.ContainsKey(item);

    public int Count
        => _dict.Count;

    public void Clear()
        => _dict.Clear();

    public System.Collections.Generic.IEnumerable<T> Items
        => _dict.Keys;
}
