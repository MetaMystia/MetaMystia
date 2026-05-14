using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Runtime;

namespace MetaMystia.Utils.Il2CppInterop;

/// <summary>
/// 用于构造带 <c>out</c>/<c>ref</c> 参数的 il2cpp 委托。
/// <para>
/// Il2CppInterop 的 <c>DelegateSupport.ConvertDelegate</c> 内部对每个参数类型
/// 调用 <c>MakeGenericType</c>，遇到 <c>String&amp;</c> / <c>Boolean&amp;</c>
/// 等 byref 类型会直接抛
/// <c>The type 'System.String&amp;' may not be used as a type argument.</c>
/// </para>
/// <para>
/// 调用方需自行编写一个匹配 il2cpp ABI 的
/// <c>[UnmanagedFunctionPointer(CallingConvention.Cdecl)]</c> 托管委托
/// （out 参数用 <c>IntPtr*</c> / <c>byte*</c> 等指针），并保证它有静态强引用
/// 不被 GC，再把函数指针交给本类即可得到一个可注册到游戏侧的 il2cpp 委托。
/// </para>
/// </summary>
public static class Il2CppOutDelegateBuilder
{
    /// <summary>
    /// 用一个原生函数指针构造 il2cpp 委托 <typeparamref name="TIl2CppDelegate"/>。
    /// </summary>
    /// <typeparam name="TIl2CppDelegate">il2cpp 委托类型（必须继承自
    /// <see cref="Il2CppSystem.Delegate"/>）。</typeparam>
    /// <param name="nativeFunctionPointer">通过
    /// <see cref="Marshal.GetFunctionPointerForDelegate(Delegate)"/> 取得的、
    /// 与 il2cpp ABI 匹配的原生函数指针。**调用方必须用静态字段持有原始托管
    /// 委托，避免 GC 后函数指针失效。**</param>
    /// <param name="parameterCount">il2cpp 委托 Invoke 方法的参数数量
    /// （不含末尾的 <c>Il2CppMethodInfo*</c>）。</param>
    public static TIl2CppDelegate Build<TIl2CppDelegate>(
        IntPtr nativeFunctionPointer,
        int parameterCount)
        where TIl2CppDelegate : Il2CppObjectBase
    {
        if (nativeFunctionPointer == IntPtr.Zero)
            throw new ArgumentNullException(nameof(nativeFunctionPointer));
        if (parameterCount < 0 || parameterCount > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(parameterCount));

        var classTypePtr = Il2CppClassPointerStore<TIl2CppDelegate>.NativeClassPtr;
        if (classTypePtr == IntPtr.Zero)
            throw new InvalidOperationException(
                $"{typeof(TIl2CppDelegate)} 的 il2cpp 类指针未初始化");

        var methodInfo = UnityVersionHandler.NewMethod();
        methodInfo.MethodPointer = nativeFunctionPointer;
        methodInfo.ParametersCount = (byte)parameterCount;
        methodInfo.Slot = ushort.MaxValue;
        methodInfo.IsMarshalledFromNative = true;

        // il2cpp 委托构造器对实例方法会校验 this 非空，必须传一个非 null 的
        // Il2CppSystem.Object 占位 target，否则抛
        // "Delegate to an instance method cannot have null 'this'."
        var dummyTarget = new Il2CppSystem.Object();

        Il2CppSystem.Delegate converted;
        if (UnityVersionHandler.MustUseDelegateConstructor)
        {
            converted = ((TIl2CppDelegate)Activator.CreateInstance(
                    typeof(TIl2CppDelegate), dummyTarget, methodInfo.Pointer))
                .Cast<Il2CppSystem.Delegate>();
        }
        else
        {
            var nativeDelegatePtr = IL2CPP.il2cpp_object_new(classTypePtr);
            converted = new Il2CppSystem.Delegate(nativeDelegatePtr);
        }

        converted.method_ptr = methodInfo.MethodPointer;
        converted.method = methodInfo.Pointer;
        converted.m_target = dummyTarget;
        if (UnityVersionHandler.MustUseDelegateConstructor)
        {
            converted.invoke_impl = converted.method_ptr;
            converted.method_code = dummyTarget.Pointer;
        }

        return converted.Cast<TIl2CppDelegate>();
    }

    /// <summary>
    /// 便捷重载：直接传入托管 trampoline 委托。
    /// **调用方仍需用静态字段持有该 <paramref name="nativeTrampoline"/>**，
    /// 否则函数指针在 GC 后将失效。
    /// </summary>
    public static TIl2CppDelegate Build<TIl2CppDelegate>(
        Delegate nativeTrampoline,
        int parameterCount)
        where TIl2CppDelegate : Il2CppObjectBase
    {
        if (nativeTrampoline == null)
            throw new ArgumentNullException(nameof(nativeTrampoline));
        return Build<TIl2CppDelegate>(
            Marshal.GetFunctionPointerForDelegate(nativeTrampoline),
            parameterCount);
    }
}
