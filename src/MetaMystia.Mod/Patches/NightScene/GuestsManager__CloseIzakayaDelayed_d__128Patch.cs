#if !TMI_RELEASE_4_4_0E
#error 请核对本文件依赖的游戏协程、状态机及编译器生成成员，完成版本适配后再更新此标记。
#endif

using HarmonyLib;

using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;

namespace MetaMystia.Patch;

// 4.4.0e：GuestsManager.CloseIzakayaDelayed 的状态机。普通营业的调用在闭包中被内联，只能 Hook MoveNext。
[HarmonyPatch(typeof(NightScene.GuestManagementUtility.GuestsManager._CloseIzakayaDelayed_d__128))]
public static class GuestsManager__CloseIzakayaDelayed_d__128Patch
{
    /// <summary>主机等完全部顾客与符卡延长营业，开始关店。</summary>
    [HarmonyPatch(nameof(GuestsManager._CloseIzakayaDelayed_d__128.MoveNext))]
    [HarmonyPrefix]
    public static void MoveNext_Prefix(GuestsManager._CloseIzakayaDelayed_d__128 __instance)
    {
        if (GameSession.IsRoomHost && GameSession.HasRoomPeers && __instance.__1__state == 0
            && __instance.izakayaCloseType == GuestsManager.IzakayaCloseType.General)
            IzakayaCloseMessage.Send(true);
    }
}
