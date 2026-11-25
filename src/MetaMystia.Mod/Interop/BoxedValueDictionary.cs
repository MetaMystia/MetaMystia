using System;

using Il2CppInterop.Runtime;

namespace MetaMystia;

/// <summary>
/// 向 Il2Cpp 字典写入装箱值类型。直接使用 Add 或索引器会把对象头当作字段数据写入，
/// 见 docs/reference/il2cppinterop-defects.md。
/// </summary>
internal static unsafe class BoxedValueDictionary
{
    internal static void SetBoxedValue<TValue>(
        this Il2CppSystem.Collections.Generic.Dictionary<string, TValue> dictionary, string key, TValue value)
        where TValue : Il2CppSystem.Object
    {
        // 字符串传对象指针，int 传数据指针。
        var nativeKey = new Il2CppSystem.Object(IL2CPP.ManagedStringToIl2Cpp(key));
        SetItem(dictionary, (void*)nativeKey.Pointer, value);
        GC.KeepAlive(nativeKey);
    }

    internal static void SetBoxedValue<TValue>(
        this Il2CppSystem.Collections.Generic.Dictionary<int, TValue> dictionary, int key, TValue value)
        where TValue : Il2CppSystem.Object
        => SetItem(dictionary, &key, value);

    private static void SetItem<TValue>(Il2CppSystem.Object dictionary, void* key, TValue value)
        where TValue : Il2CppSystem.Object
    {
        // 按字典声明的类型判断：声明为引用类型时，装箱值也不能拆箱。
        if (!IL2CPP.il2cpp_class_is_valuetype(Il2CppClassPointerStore<TValue>.NativeClassPtr))
            throw new ArgumentException($"{typeof(TValue)} is not an IL2CPP value type", nameof(value));

        var method = IL2CPP.il2cpp_class_get_method_from_name(
            IL2CPP.il2cpp_object_get_class(dictionary.Pointer), "set_Item", 2);
        if (method == IntPtr.Zero) throw new InvalidOperationException("Dictionary set_Item unavailable");

        var args = stackalloc void*[2];
        args[0] = key;
        args[1] = (void*)IL2CPP.il2cpp_object_unbox(value.Pointer);
        var exception = IntPtr.Zero;
        IL2CPP.il2cpp_runtime_invoke(method, dictionary.Pointer, args, ref exception);
        GC.KeepAlive(value);
        GC.KeepAlive(dictionary);
        Il2CppException.RaiseExceptionIfNecessary(exception);
    }
}
