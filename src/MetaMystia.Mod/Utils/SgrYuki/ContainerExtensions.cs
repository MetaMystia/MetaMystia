using System;
using System.Linq;
using Il2CppInterop.Runtime;
using Il2CppSystem.Linq;

namespace SgrYuki.Utils;

public static class ContainerExtensions
{
    public static unsafe string Il2CppStringToManaged(this IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return null;

        int length = *(int*)(ptr + (IntPtr.Size * 2));
        char* chars = (char*)(ptr + (IntPtr.Size * 2) + sizeof(int));

        return new string(chars, 0, length);
    }

    extension<T>(System.Collections.Generic.IEnumerable<T> source)
    {
        public T GetRandomOne()
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            // 如果是 IReadOnlyList / List / Array，走 O(1)
            if (source is System.Collections.Generic.IReadOnlyList<T> list)
            {
                if (list.Count == 0)
                    throw new InvalidOperationException("Collection is empty.");

                return list[UnityEngine.Random.Range(0, list.Count)];
            }

            // 否则用水塘抽样
            T result = default;
            int count = 0;

            foreach (var item in source)
            {
                count++;
                if (UnityEngine.Random.Range(0, count) == 0)
                    result = item;
            }

            if (count == 0)
                throw new InvalidOperationException("Collection is empty.");

            return result;
        }

        public string DumpElements(string separator = ", ")
        {
            if (source == null) return "";

            if (source is System.Collections.Generic.IReadOnlyList<T> list)
            {
                if (list.Count == 0) return "";
            }

            var sb = new System.Text.StringBuilder(1024);
            sb.Append('{');
            foreach (var item in source)
            {
                try
                {
                    sb.Append(item.ToString());
                    sb.Append(separator);
                }
                catch (Exception ex)
                {
                    sb.Append($"<ToString threw {ex.GetType().Name}>");
                }
            }
            sb.Append(" }");
            return sb.ToString();
        }

    }

    extension<T>(Il2CppSystem.Collections.Generic.List<T> list)
    {
        public Il2CppSystem.Collections.Generic.IEnumerable<T> ToIEnumerable()
        {
            return new Il2CppSystem.Collections.Generic.IEnumerable<T>(list.Pointer);
        }
    }


    public static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T> ToIl2CppReferenceArray<T>
        (this Il2CppSystem.Collections.Generic.IEnumerable<T> enumerable)
        where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        return enumerable.ToArray().ToIl2CppReferenceArray();
    }

    public static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T> ToIl2CppReferenceArray<T>
        (this System.Collections.Generic.IEnumerable<T> source)
        where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        return new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T>(source.ToArray());
    }

    extension<T>(Il2CppSystem.Collections.Generic.IEnumerable<T> enumerable)
    {
        public Il2CppSystem.Collections.Generic.List<T> ToIl2CppList()
        {
            return new Il2CppSystem.Collections.Generic.List<T>(enumerable);
        }

        public T GetRandomOne()
        {
            return enumerable.ToIl2CppList().ToManagedList().GetRandomOne();
        }

    }

    extension<KeyT, ValueT>(Il2CppSystem.Collections.Generic.Dictionary<KeyT, ValueT> dict)
    {
        public System.Collections.Generic.List<KeyT> FilterKey(Predicate<ValueT> condition)
        {
            var result = new System.Collections.Generic.List<KeyT>();
            foreach (var kv in dict)
            {
                if (condition(kv.Value)) result.Add(kv.Key);
            }
            return result;
        }

        public System.Collections.Generic.List<KeyT> GetKeys()
        {
            var result = new System.Collections.Generic.List<KeyT>();
            foreach (var kv in dict)
            {
                result.Add(kv.Key);
            }
            return result;
        }

        public System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<KeyT, ValueT>> ToList()
        {
            var result = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<KeyT, ValueT>>();
            foreach (var kv in dict)
            {
                result.Add(new System.Collections.Generic.KeyValuePair<KeyT, ValueT>(kv.Key, kv.Value));
            }
            return result;
        }
    }

    extension<TKey, TValue>(System.Collections.Generic.Dictionary<TKey, TValue> dict)
    {
        public Il2CppSystem.Collections.Generic.Dictionary<TKey, TValue> ToIl2CppDictionary()
        {
            if (dict == null) return null;
            var result = new Il2CppSystem.Collections.Generic.Dictionary<TKey, TValue>();
            foreach (var kvp in dict)
            {
                result.Add(kvp.Key, kvp.Value);
            }
            return result;
        }
    }

    extension<T>(Il2CppSystem.Collections.Generic.List<T> il2cppList)
    {
        public System.Collections.Generic.List<T> ToManagedList()
        {
            if (il2cppList == null)
                return null;

            var result = new System.Collections.Generic.List<T>(il2cppList.Count);
            for (int i = 0; i < il2cppList.Count; i++)
            {
                result.Add(il2cppList[i]);
            }

            return result;
        }
    }



    extension<T>(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T> array) where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        public Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T> SortByToString()
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            int length = array.Length;
            if (length <= 1)
                return array; // 0/1 个元素无需排序

            // 拷贝到托管数组
            T[] managed = new T[length];
            for (int i = 0; i < length; i++)
            {
                managed[i] = array[i];
            }

            // 排序（注意 null 处理）
            Array.Sort(managed, (a, b) =>
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a is null) return -1;
                if (b is null) return 1;

                string sa = a.ToString();
                string sb = b.ToString();

                return string.Compare(
                    sa, sb,
                    StringComparison.Ordinal
                );
            });

            // 构造新的 Il2CppReferenceArray
            var result = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<T>(length);
            for (int i = 0; i < length; i++)
            {
                result[i] = managed[i];
            }

            return result;
        }

        /// <summary>
        /// Return -1 if item not found
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arr"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public int IndexAtByToString(T item)
        {
            var itemString = item.ToString();
            for (int i = 0; i < array.Length; i++)
            {
                if (itemString.Equals(array[i].ToString()))
                {
                    return i;
                }
            }
            return -1;
        }

        public string DumpElements(string separator = ", ")
        {
            if (array == null)
                return "<Il2CppReferenceArray: null>";

            int length = array.Length;
            if (length == 0)
                return "<Il2CppReferenceArray: empty>";

            var sb = new System.Text.StringBuilder(1024);

            sb.Append($"Il2CppReferenceArray<{typeof(T).Name}>[{length}] {{ ");

            for (int i = 0; i < length; i++)
            {
                if (i > 0)
                    sb.Append(separator);

                T element = array[i];

                if (element == null)
                {
                    sb.Append("null");
                    continue;
                }

                try
                {
                    sb.Append((element as UnityEngine.Object).ToString());
                }
                catch (Exception ex)
                {
                    sb.Append($"<ToString threw {ex.GetType().Name}>");
                }
            }

            sb.Append(" }");
            return sb.ToString();
        }
    }

    extension(Action action)
    {
        public Il2CppSystem.Action ToIl2cppAction()
        {
            return Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(action);
        }
    }

    extension<T>(Action<T> action)
    {
        public Il2CppSystem.Action<T> ToIl2cppAction()
        {
            return Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<T>>(action);
        }
    }

    public static System.Collections.Generic.List<T> Flatten<T>(this Il2CppSystem.Collections.Generic.List<Il2CppSystem.Collections.Generic.IEnumerable<T>> source)
    {
        var result = new System.Collections.Generic.List<T>();

        if (source == null) return result;

        foreach (var list in source)
        {
            foreach (var item in list.ToList())
            {
                result.Add(item);
            }
        }
        return result;
    }

    public static Il2CppSystem.Type ToIl2cppType(this System.Type T)
    {
        return Il2CppType.From(T);
    }
}

public static class Il2CppIEnumerableExtensions
{
    extension(Il2CppSystem.Collections.IEnumerable source)
    {
        /// <summary>
        /// 是否至少存在一个元素
        /// </summary>
        public bool Any()
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var enumerator = source.GetEnumerator();
            return enumerator.MoveNext();
        }

        /// <summary>
        /// 是否存在满足条件的元素
        /// </summary>
        public bool Any(
            Func<object, bool> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var enumerator = source.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (predicate(enumerator.Current))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 是否所有元素都满足条件
        /// （空集合返回 true，符合 LINQ 语义）
        /// </summary>
        public bool All(
            Func<object, bool> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var enumerator = source.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (!predicate(enumerator.Current))
                    return false;
            }

            return true;
        }
    }
}
