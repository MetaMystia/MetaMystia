using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DayScene.UI;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;

namespace MetaMystia;

/// <summary>
/// Il2CppInterop 的 ConvertDelegate 不支持 out/ref 参数，需手动构造 il2cpp 委托。
/// </summary>
[AutoLog]
public static unsafe partial class Il2CppOutDelegate
{
    public delegate void GetSelectionConfigurationHandler(
        DaySceneChatSelectionPannel.BaseInteractData baseInteractData,
        out string title,
        out bool availability,
        out Il2CppSystem.Action onInteract);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NativeGetSelectionConfigurationInvoker(
        IntPtr thisPtr,
        IntPtr dataPtr,
        IntPtr* titleOut,
        byte* availabilityOut,
        IntPtr* onInteractOut,
        Il2CppMethodInfo* methodInfo);

    private static readonly NativeGetSelectionConfigurationInvoker s_NativeInvoker = NativeInvoke;
    private static readonly IntPtr s_NativeInvokerPtr =
        Marshal.GetFunctionPointerForDelegate(s_NativeInvoker);

    private static readonly Dictionary<IntPtr, GetSelectionConfigurationHandler> s_Handlers = new();
    private static readonly List<DaySceneChatSelectionPannel.GetSelectionConfigurationCallback> s_KeepAlive = new();

    public static DaySceneChatSelectionPannel.GetSelectionConfigurationCallback CreateGetSelectionConfigurationCallback(
        GetSelectionConfigurationHandler handler)
    {
        var classTypePtr = Il2CppClassPointerStore<
            DaySceneChatSelectionPannel.GetSelectionConfigurationCallback>.NativeClassPtr;
        if (classTypePtr == IntPtr.Zero)
            throw new InvalidOperationException("GetSelectionConfigurationCallback 的 il2cpp 类指针未初始化");

        var methodInfo = UnityVersionHandler.NewMethod();
        methodInfo.MethodPointer = s_NativeInvokerPtr;
        methodInfo.ParametersCount = 4;
        methodInfo.Slot = ushort.MaxValue;
        methodInfo.IsMarshalledFromNative = true;
        s_Handlers[methodInfo.Pointer] = handler;

        Il2CppSystem.Delegate converted;
        var dummyTarget = new Il2CppSystem.Object();
        if (UnityVersionHandler.MustUseDelegateConstructor)
        {
            converted = ((DaySceneChatSelectionPannel.GetSelectionConfigurationCallback)
                Activator.CreateInstance(
                    typeof(DaySceneChatSelectionPannel.GetSelectionConfigurationCallback),
                    dummyTarget, methodInfo.Pointer)).Cast<Il2CppSystem.Delegate>();
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

        var result = converted.Cast<DaySceneChatSelectionPannel.GetSelectionConfigurationCallback>();
        s_KeepAlive.Add(result);
        return result;
    }

    private static void NativeInvoke(
        IntPtr thisPtr,
        IntPtr dataPtr,
        IntPtr* titleOut,
        byte* availabilityOut,
        IntPtr* onInteractOut,
        Il2CppMethodInfo* methodInfo)
    {
        try
        {
            if (!s_Handlers.TryGetValue((IntPtr)methodInfo, out var handler))
                return;

            var data = dataPtr != IntPtr.Zero
                ? new DaySceneChatSelectionPannel.BaseInteractData(dataPtr)
                : null;

            handler(data, out var title, out var availability, out var onInteract);

            if (titleOut != null)
                *titleOut = title != null ? IL2CPP.ManagedStringToIl2Cpp(title) : IntPtr.Zero;
            if (availabilityOut != null)
                *availabilityOut = (byte)(availability ? 1 : 0);
            if (onInteractOut != null)
                *onInteractOut = onInteract != null ? onInteract.Pointer : IntPtr.Zero;
        }
        catch (Exception ex)
        {
            Log.Error($"NativeInvoke: {ex}");
            if (titleOut != null) *titleOut = IntPtr.Zero;
            if (availabilityOut != null) *availabilityOut = 0;
            if (onInteractOut != null) *onInteractOut = IntPtr.Zero;
        }
    }
}
