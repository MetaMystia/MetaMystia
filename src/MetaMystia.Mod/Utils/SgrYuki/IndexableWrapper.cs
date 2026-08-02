using System;

namespace SgrYuki.Utils;

// Support "foreach" any container that implements [] operator and Count member method
public readonly struct IndexableWrapper
{
    private readonly object _inner;

    public IndexableWrapper(object inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /* =========================
     * foreach 支持
     * ========================= */

    public Enumerator GetEnumerator()
        => new Enumerator(_inner);

    public struct Enumerator
    {
        private readonly object _container;
        private int _index;

        public Enumerator(object container)
        {
            _container = container;
            _index = -1;
        }

        public readonly object Current => ((dynamic)_container)[_index];

        public bool MoveNext()
        {
            return ++_index < ((dynamic)_container).Count();
        }
    }

    /* =========================
     * Any / All（内建）
     * ========================= */

    /// <summary>
    /// 是否至少存在一个元素
    /// </summary>
    public bool Any()
    {
        var e = GetEnumerator();
        return e.MoveNext();
    }

    /// <summary>
    /// 是否存在满足条件的元素
    /// </summary>
    public bool Any(Func<object, bool> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        var e = GetEnumerator();
        while (e.MoveNext())
        {
            if (predicate(e.Current))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 是否所有元素都满足条件
    /// （空集合返回 true，符合 LINQ 语义）
    /// </summary>
    public bool All(Func<object, bool> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        var e = GetEnumerator();
        while (e.MoveNext())
        {
            if (!predicate(e.Current))
                return false;
        }
        return true;
    }
}
