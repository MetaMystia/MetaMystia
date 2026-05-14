using System;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using Il2CppSystem.Collections.Generic;
using DayScene.UI;
using MetaMystia.Utils.Il2CppInterop;
using SgrYuki.Utils;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(DayScene.UI.DaySceneChatSelectionPannel))]
[AutoLog]
public unsafe partial class DaySceneChatSelectionPannelPatch
{
    // 详见 Utils/Il2CppOutDelegateBuilder.cs：
    // ConvertDelegate 不支持 byref 参数，必须手动构造 il2cpp 委托。

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate void NativeSpecialNPCSelectionInvoker(
        IntPtr thisPtr,
        IntPtr dataPtr,
        IntPtr* titleOut,
        byte* availabilityOut,
        IntPtr* onInteractOut,
        Il2CppMethodInfo* methodInfo);

    // 必须强引用，避免 GC 后函数指针失效
    private static readonly NativeSpecialNPCSelectionInvoker s_BeverageNativeInvoker = NativeBeverageInvoke;

    private static readonly DaySceneChatSelectionPannel.GetSpecialNPCSelectionConfigurationCallback s_BeverageDelegate =
        Il2CppOutDelegateBuilder.Build<DaySceneChatSelectionPannel.GetSpecialNPCSelectionConfigurationCallback>(
            s_BeverageNativeInvoker, parameterCount: 4);

    [HarmonyPatch(nameof(DaySceneChatSelectionPannel.GetConfigurationSet))]
    [HarmonyPostfix]
    public static void GetConfigurationSet_Postfix(
        ref IEnumerable<DaySceneChatSelectionPannel.GetSpecialNPCSelectionConfigurationCallback> __result,
        DaySceneChatSelectionPannel __instance)
    {
        Log.Warning("正在为酒水货架添加对话选项");

        var list = __result.ToIl2CppList();
        list.Add(s_BeverageDelegate);
        __result = list.ToIEnumerable();
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
