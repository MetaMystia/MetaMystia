using GameData.RunTime.DaySceneUtility;

using Mystia.Listeners;
using Mystia.Scenes;

using MetaMystia.Multiplayer;

namespace MetaMystia.Listeners;

/// <summary>
/// 白天与营业界面通知。承接原 <c>DaySceneShopPannelPatch</c>、<c>DaySceneSustainedPannelPatch</c>、
/// <c>UIManagerPatch</c>，以及原 <c>Patches/Compat/WorkSceneSustainedPannelPatch</c> 的营业内快进拦截。
/// </summary>
[AutoLog]
public sealed partial class UiSync : IDayUiListener, IWorkUiListener
{
    /// <summary>原 <c>DaySceneShopPannelPatch.OnPanelOpen_Postfix</c>：货架为空时清掉面板留下的自定义间距。</summary>
    public void OnShopPannelOpened(ShopPannelView view)
    {
        if (view.HasProducts) return;

        Log.Info("Empty shelf detected after OnPanelOpen, removing orphaned custom spacing");
        view.ClearCustomSpacing();
    }

    /// <summary>
    /// 原 <c>DaySceneSustainedPannelPatch.OnFastForwardSubmit_Prefix</c>：联机下先提交营业意向并等待，
    /// 意向确认后才继续原版的快进提交（原版会先耗尽行动点，等待期间才能改选挑战）。
    /// </summary>
    void IDayUiListener.OnPreFastForward(ref bool cancelInvocation)
    {
        if (!GameSession.HasRoomPeers || DayDestinationManager.ReplayingBusiness) return;

        cancelInvocation = true;
        // 面板由本次提交的实例自身持有；意向确认后在下一轮 <c>ContinueEntry</c> 里回到原版提交流程。
        var panel = DayScene.UI.UIManager.Instance?.SustainedPannel;
        DayDestinationManager.Submit(DayDestination.Business, () =>
        {
            if (RunTimeDayScene.RemainActions == 0) DaySync.RequestDayOver();
            else panel?.OnFastForwardSubmit();
        });
    }

    /// <summary>
    /// 原 <c>WorkSceneSustainedPannelPatch.OnFastForwardSubmit_Prefix</c>：跳过整晚只允许主机发起，
    /// 客机拦下原版提交。与白天的快进是不同入口，因此按接口分开实现。
    /// </summary>
    void IWorkUiListener.OnPreFastForward(ref bool cancelInvocation)
    {
        if (!GameSession.IsRoomClient) return;

        Log.Message("Client attempted to fast forward, blocked");
        cancelInvocation = true;
    }

    /// <summary>原 <c>UIManagerPatch.Initialize_Postfix</c>：营业 HUD 打开后刷新本地立绘。</summary>
    public void OnHudOpened() => PlayerManager.RefreshPortrait(true);
}
