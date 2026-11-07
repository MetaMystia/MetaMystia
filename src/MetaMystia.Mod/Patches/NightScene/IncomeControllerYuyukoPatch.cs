using HarmonyLib;

using NightScene.UI.HUDUtility;


using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.UI.HUDUtility.IncomeControllerYuyuko))]
[AutoLog]
public partial class IncomeControllerYuyukoPatch
{
    private static int? hostLife;
    private static int? lastSentLife;

    internal static void ResetProgress()
    {
        hostLife = null;
        lastSentLife = null;
    }

    [HarmonyPatch(nameof(IncomeControllerYuyuko.SetContext))]
    [HarmonyPostfix]
    public static void SetContext_Postfix(IncomeControllerYuyuko __instance)
    {
        if (!IsPhase3Panel(__instance)) return;
        if (GameSession.IsRoomHost) SendProgress();
        else ApplyProgress();
    }

    // 原版各条扣血路径先修改 yuyukoTotalLife，再用 SetTargetProgress 刷新显示。
    // 这里只将 UI 更新作为状态探测点，发送的是挑战生命值，而非动画中的 currentProgress。
    [HarmonyPatch(nameof(IncomeControllerYuyuko.SetTargetProgress))]
    [HarmonyPrefix]
    public static void SetTargetProgress_Prefix(IncomeControllerYuyuko __instance, ref int targetValue)
    {
        if (!IsPhase3Panel(__instance)) return;
        if (GameSession.IsRoomHost)
        {
            SendProgress();
        }
        else if (hostLife.HasValue)
        {
            YuyukoBossDataPatch.CurrentContext.yuyukoTotalLife = hostLife.Value;
            targetValue = hostLife.Value;
        }
    }

    private static bool IsPhase3Panel(IncomeControllerYuyuko panel) =>
        GameSession.HasRoomPeers && PrepSceneManager.IsYuyukoChallenge && panel != null && panel.invert
        && YuyukoBossDataPatch.CurrentContext?.statusDisplayer?.Pointer == panel.Pointer;

    private static void SendProgress()
    {
        int life = YuyukoBossDataPatch.CurrentContext.yuyukoTotalLife;
        if (lastSentLife == life) return;
        lastSentLife = life;
        YuyukoLifeMessage.Send(life);
    }

    internal static void ReceiveProgress(int life)
    {
        var context = YuyukoBossDataPatch.CurrentContext;
        if (!PrepSceneManager.IsYuyukoChallenge || context == null
            || life < 0 || life > context.__4__this.phase3YuyukoTotalLife) return;

        // 剧情和阶段 UI 的打开时刻可能不同，保留最新绝对值，进入三阶段再应用。
        hostLife = life;
        ApplyProgress();
    }

    private static void ApplyProgress()
    {
        var context = YuyukoBossDataPatch.CurrentContext;
        if (!hostLife.HasValue || !IsPhase3Panel(context?.statusDisplayer)) return;
        context.yuyukoTotalLife = hostLife.Value;
        context.statusDisplayer.SetTargetProgress(hostLife.Value);
    }
}
