using System;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using Il2CppInterop.Runtime;

namespace MetaMiku
{
    public static class Utils
    {
        private static ManualLogSource Log => MetaMystia.Plugin.Instance.Log;
        private const string LOG_TAG = "[MetaMiku.Utils]";

        /// <summary>
        /// 强制将 Il2CppSystem.ValueTuple 等装箱类型的值插入到 Il2CppSystem.Collections.Generic.Dictionary 中。
        /// <para>
        /// <b>问题背景 (Context):</b><br/>
        /// 在 Il2CppInterop 中，`Il2CppSystem.ValueTuple` 被映射为一个类 (Class)，它在托管堆上持有一个指向 IL2CPP 对象的指针。
        /// 这个 IL2CPP 对象是已装箱 (Boxed) 的，包含对象头 (Header, 16字节) 和实际数据。<br/>
        /// 然而，底层的 C++ `Dictionary` (泛型参数为结构体时) 存储的是未装箱 (Unboxed) 的纯数据。<br/>
        /// 当直接调用 `dictionary.Add(key, value)` 时，Interop 层错误地将装箱对象的指针 (指向 Header) 传递给了底层方法，
        /// 导致底层方法将 Header 当作数据拷贝进字典，造成数据偏移 (Item3 变成 Item1) 和内存破坏 (读取 Item1 时访问非法指针)。
        /// </para>
        /// <para>
        /// <b>解决方案 (Solution):</b><br/>
        /// 此方法通过 `IL2CPP.il2cpp_object_unbox` 获取跳过 Header 后的纯数据指针，
        /// 并通过反射手动调用底层的 `set_Item` 方法，确保传入正确的数据指针。
        /// </para>
        /// </summary>
        /// <typeparam name="TKey">键类型 (必须是值类型，如 int, long, float 等)</typeparam>
        /// <typeparam name="TValue">值类型 (必须是 Il2CppSystem.Object 的子类，通常是 ValueTuple)</typeparam>
        /// <param name="dictionary">目标字典</param>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public static unsafe void ForceAddOrUpdateValueTuple<TKey, TValue>(
            this Il2CppSystem.Collections.Generic.Dictionary<TKey, TValue> dictionary,
            TKey key,
            TValue value)
            where TKey : struct
            where TValue : Il2CppSystem.Object
        {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));
            if (value == null) throw new ArgumentNullException(nameof(value));

            // 1. 获取原始数据指针 (Get pointer to raw struct data, skipping the object header)
            System.IntPtr rawDataPtr = IL2CPP.il2cpp_object_unbox(value.Pointer);

            // 2. 查找 set_Item 方法 (Find set_Item method)
            // 我们需要缓存这个 MethodInfo 吗？为了性能可以考虑，但为了稳定性先实时查找。
            System.IntPtr methodPtr = System.IntPtr.Zero;
            System.IntPtr iter = System.IntPtr.Zero;
            System.IntPtr curMethod = System.IntPtr.Zero;
            System.IntPtr classPtr = IL2CPP.il2cpp_object_get_class(dictionary.Pointer);

            while ((curMethod = IL2CPP.il2cpp_class_get_methods(classPtr, ref iter)) != System.IntPtr.Zero)
            {
                string name = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_method_get_name(curMethod));
                // 目标方法签名: set_Item(TKey key, TValue value)
                // 参数数量应为 2
                if (name == "set_Item" && IL2CPP.il2cpp_method_get_param_count(curMethod) == 2)
                {
                    methodPtr = curMethod;
                    break;
                }
            }

            if (methodPtr == System.IntPtr.Zero)
            {
                Log.LogError($"{LOG_TAG} Could not find set_Item method on dictionary!");
                return;
            }

            // 3. 调用方法 (Invoke)
            // 构造参数数组。注意：对于泛型字典，Key 和 Value 通常都是通过指针传递的。
            // TKey 是 struct，我们将其装箱并固定，获取其数据指针。
            // TValue 是 struct (在 C++ 侧)，我们传 unboxed data pointer。
            
            GCHandle keyHandle = GCHandle.Alloc(key, GCHandleType.Pinned);
            try
            {
                System.IntPtr* args = stackalloc System.IntPtr[2];
                args[0] = keyHandle.AddrOfPinnedObject(); 
                args[1] = rawDataPtr;            

                System.IntPtr exc = System.IntPtr.Zero;
                IL2CPP.il2cpp_runtime_invoke(methodPtr, dictionary.Pointer, (void**)args, ref exc);

                if (exc != System.IntPtr.Zero)
                {
                    // 尝试获取异常信息
                    // var excObj = new Il2CppSystem.Exception(exc);
                    // Log.LogError($"{LOG_TAG} Exception during set_Item invoke: {excObj.Message}");
                    Log.LogError($"{LOG_TAG} Exception during set_Item invoke! (Pointer: {exc})");
                }
                else
                {
                    // Log.LogDebug($"{LOG_TAG} Successfully invoked set_Item with unboxed value.");
                }
            }
            finally
            {
                keyHandle.Free();
            }
        }
    }
}

/*
MetaMiku 注:

这是一个巨抽象的问题，可能因电脑不同而异

问题来源:
试图对 GameData.CoreLanguage.Collections.DataBaseLanguage.SpecialGuest 插入新数据时，内存发生偏移，插入错误，导致读取数据时访问非法内存。
研究了很久，最后是让 Gemini3 完成了一个替代方案，下面是 Gemini3 的解释原文:

Gemini 3:

这个问题的原因非常隐蔽，但你的观察（Item3 变成了 Item1，Item4 变成了 Item2）提供了关键线索。

问题分析
这确实是 Il2CppInterop 或底层机制的一个 Bug，具体表现为装箱（Boxing）偏移处理不当。

内存布局差异：

Il2CppSystem.ValueTuple 在 C# 端被 Il2CppInterop 包装为一个类（Class），它持有一个指向 IL2CPP 对象的指针。这个对象在内存中是**已装箱（Boxed）**的，包含一个 16 字节的头部（Header，包含类指针和同步块）+ 实际数据。
Dictionary<int, ValueTuple> 在底层 C++ 代码中，存储的是**未装箱（Unboxed）**的结构体数据，没有头部。
错误的拷贝：

当你调用 specialGuest.Add(key, val) 时，生成的互操作代码直接将 val.Pointer（指向装箱对象的头部）传递给了底层的 Add 方法。
底层的 Add 方法期望接收一个指向结构体数据的指针，然后将其内容拷贝到字典的数组中。
结果是：它把**装箱对象的头部（16字节）**当作了结构体的前两个字段（Item1, Item2）拷贝了进去，而真正的 Item1 和 Item2 被当作了 Item3 和 Item4。
这就是为什么你读取 Item1/Item2 时会崩溃（读到了垃圾内存作为指针），而读取 Item3/Item4 时却读到了原本的 Item1/Item2。
解决方案
我们需要绕过 Il2CppInterop 生成的 Add / set_Item 方法，手动调用底层的 set_Item 方法，并显式传递解箱后（Unboxed）的数据指针。

我已将 F2 的逻辑修改为使用 unsafe 代码块进行底层调用：

使用 IL2CPP.il2cpp_object_unbox(val.Pointer) 获取跳过头部后的纯数据指针。
通过反射（在 IL2CPP 层面）找到字典的 set_Item 方法。
使用 il2cpp_runtime_invoke 直接调用该方法，传入正确的 Key 指针和 Unboxed Value 指针。
请再次尝试运行并按 F2，这次应该能正确插入数据，并且 F3 读取时也不会崩溃了。

Gemini 3 Pro (Preview) • 1x



Gemini 3:

我已经为你创建了 MetaMikuUtils.cs 文件，并封装了 ForceAddOrUpdateValueTuple 扩展方法。

工具类说明
这个工具类 MetaMiku.Utils 包含了一个扩展方法 ForceAddOrUpdateValueTuple，专门用于解决 Il2CppSystem.ValueTuple 在 Dictionary 中插入时发生的内存偏移问题。

问题的前因后果：

原因：Il2CppInterop 将 ValueTuple 映射为引用类型（Class），包含对象头（Header）。而底层的 C++ 字典期望的是值类型（Struct），不包含 Header。直接插入时，Interop 层错误地将 Header 当作数据拷贝，导致数据错位（Item3 变 Item1）和内存破坏。
后果：读取字典时，前两个字段指向无效内存地址，导致崩溃；后两个字段读到的是前两个字段的值。
解决方案：该工具方法通过 IL2CPP.il2cpp_object_unbox 获取跳过 Header 后的纯数据指针，并使用底层 il2cpp_runtime_invoke 直接调用字典的 set_Item 方法，绕过有问题的 Interop 自动转换。
使用方法
在你的代码中（例如 PluginManager.cs），你可以像下面这样使用它：

引入命名空间：

调用扩展方法：

现在你可以放心地在项目中使用这个工具来处理类似的字典插入问题了。

Gemini 3 Pro (Preview) • 1x
*/
