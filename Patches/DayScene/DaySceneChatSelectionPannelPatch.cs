using System;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using Il2CppSystem.Collections.Generic;
using DayScene.UI;
using SgrYuki.Utils;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(DayScene.UI.DaySceneChatSelectionPannel))]
[AutoLog]
public unsafe partial class DaySceneChatSelectionPannelPatch
{
    // ──────────────────────────────────────────────────────────────────────────
    // Il2CppInterop 的 DelegateSupport.ConvertDelegate 不支持 byref（out/ref）
    // 参数：内部会执行 MakeGenericType(String&) 而抛出
    // "The type 'System.String&' may not be used as a type argument."
    //
    // GetSpecialNPCSelectionConfigurationCallback 含 out string / out bool /
    // out Action 三个 byref，所以必须手动构造 il2cpp 委托：
    //   1. 用 [UnmanagedFunctionPointer] 托管委托匹配 il2cpp ABI；
    //   2. Marshal.GetFunctionPointerForDelegate 拿函数指针；
    //   3. UnityVersionHandler.NewMethod() 构造 Il2CppMethodInfo*；
    //   4. 通过 il2cpp 委托类的 (object,IntPtr) 构造器或 il2cpp_object_new
    //      包出 Il2CppSystem.Delegate，并填充 method_ptr/method/invoke_impl。
    // ──────────────────────────────────────────────────────────────────────────

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate void NativeSpecialNPCSelectionInvoker(
        IntPtr thisPtr,
        IntPtr dataPtr,
        IntPtr* titleOut,
        byte* availabilityOut,
        IntPtr* onInteractOut,
        Il2CppMethodInfo* methodInfo);

    // 强引用，避免 GC 后函数指针失效
    private static readonly NativeSpecialNPCSelectionInvoker s_BeverageNativeInvoker = NativeBeverageInvoke;

    private static readonly IntPtr s_BeverageNativeInvokerPtr =
        Marshal.GetFunctionPointerForDelegate(s_BeverageNativeInvoker);

    private static DaySceneChatSelectionPannel.GetSpecialNPCSelectionConfigurationCallback s_BeverageDelegate;

    [HarmonyPatch(nameof(DaySceneChatSelectionPannel.GetConfigurationSet))]
    [HarmonyPostfix]
    public static void GetConfigurationSet_Postfix(
        ref IEnumerable<DaySceneChatSelectionPannel.GetSpecialNPCSelectionConfigurationCallback> __result,
        DaySceneChatSelectionPannel __instance)
    {
        Log.Warning("正在为酒水货架添加对话选项");

        var del = s_BeverageDelegate ??= CreateBeverageDelegate();

        var list = __result.ToIl2CppList();
        list.Add(del);
        __result = list.ToIEnumerable();
    }

    private static DaySceneChatSelectionPannel.GetSpecialNPCSelectionConfigurationCallback CreateBeverageDelegate()
    {
        var classTypePtr = Il2CppClassPointerStore<
            DaySceneChatSelectionPannel.GetSpecialNPCSelectionConfigurationCallback>.NativeClassPtr;
        if (classTypePtr == IntPtr.Zero)
            throw new InvalidOperationException(
                "GetSpecialNPCSelectionConfigurationCallback 的 il2cpp 类指针未初始化");

        var methodInfo = UnityVersionHandler.NewMethod();
        methodInfo.MethodPointer = s_BeverageNativeInvokerPtr;
        methodInfo.ParametersCount = 4; // data + 3 个 out
        methodInfo.Slot = ushort.MaxValue;
        methodInfo.IsMarshalledFromNative = true;

        Il2CppSystem.Delegate converted;
        // il2cpp 委托构造器对实例方法会校验 this 非空，必须传一个非 null 的
        // Il2CppSystem.Object 作为占位 target。
        var dummyTarget = new Il2CppSystem.Object();
        if (UnityVersionHandler.MustUseDelegateConstructor)
        {
            converted = ((DaySceneChatSelectionPannel.GetSpecialNPCSelectionConfigurationCallback)
                Activator.CreateInstance(
                    typeof(DaySceneChatSelectionPannel.GetSpecialNPCSelectionConfigurationCallback),
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

        return converted.Cast<DaySceneChatSelectionPannel.GetSpecialNPCSelectionConfigurationCallback>();
    }

    private static unsafe void NativeBeverageInvoke(
        IntPtr thisPtr,
        IntPtr dataPtr,
        IntPtr* titleOut,
        byte* availabilityOut,
        IntPtr* onInteractOut,
        Il2CppMethodInfo* methodInfo)
    {
        try
        {
            var data = dataPtr != IntPtr.Zero
                ? new DaySceneChatSelectionPannel.SpecialNPCInteractData(dataPtr)
                : null;

            BeverageShelfSelection(data, out var title, out var availability, out var onInteract);

            if (titleOut != null)
                *titleOut = title != null ? IL2CPP.ManagedStringToIl2Cpp(title) : IntPtr.Zero;
            if (availabilityOut != null)
                *availabilityOut = (byte)(availability ? 1 : 0);
            if (onInteractOut != null)
                *onInteractOut = onInteract != null ? onInteract.Pointer : IntPtr.Zero;
        }
        catch (Exception ex)
        {
            Log.Error($"NativeBeverageInvoke 抛出异常: {ex}");
            if (titleOut != null) *titleOut = IntPtr.Zero;
            if (availabilityOut != null) *availabilityOut = 0;
            if (onInteractOut != null) *onInteractOut = IntPtr.Zero;
        }
    }

    private static void BeverageShelfSelection(
        DaySceneChatSelectionPannel.SpecialNPCInteractData data,
        out string title,
        out bool availability,
        out Il2CppSystem.Action onInteract)
    {
        var merchantData = data?.merchantData;
        availability = merchantData != null;
        title = "酒水货架";

        System.Action managedOnInteract = () =>
        {
            data?.closeChatSelectionPannelCallback?.Invoke();
            DayScene.UI.UIManager.Instance.OpenShopPannel(merchantData, null);
        };

        onInteract = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(managedOnInteract);
    }
}
