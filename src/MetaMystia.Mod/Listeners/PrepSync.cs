using System.Globalization;

using Common.UI;
using Common.UI.GlobalMap;
using GameData.Core.Collections;
using GameData.RunTime.NightSceneUtility;
using UnityEngine;

using Mystia.Listeners;
using Mystia.Scenes;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.UI;
using SgrYuki.Utils;

namespace MetaMystia.Listeners;

/// <summary>
/// 备菜阶段同步，取代原 <c>Patches/Common/IzakayaSelectorPanelPatch</c>、
/// <c>Patches/PrepScene/IzakayaConfigPannelPatch</c> 与 <c>Patches/PrepScene/IzakayaConfigurePatch</c>：
/// 选店共识、备菜面板确认、菜单逐项同步与作弊客流写回。多人同步的发送点与条件保持不变。
/// </summary>
[AutoLog]
public sealed partial class PrepSync : IPrepListener, IPrepNightSceneGameLoop, IDaySceneGameLoop
{
    #region 状态

    /// <summary>
    /// 备菜场景的地图确认服务。选店面板属白天场景 UI，其开关与 <c>Confirm</c> 只挂在备菜场景服务上
    /// （框架形状如此），白天场景循环的 <see cref="Update(IDaySceneServices, float)"/> 需要这个引用。
    /// </summary>
    private static IPrepNightMapServices s_mapServices;

    /// <summary>本模组是否已把原版地图确认关掉（关掉后由本类接管推进）。</summary>
    private static bool s_mapConfirmGated;

    /// <summary>本机提交的实际地图对象与标签，取代原 <c>IzakayaSelectorPanelPatch.cachedSpots</c>。</summary>
    private static IGuideMapSpot s_submittedSpot;
    private static MapLabel s_submittedLabel = MapLabel.Unknown;

    /// <summary>本次选店是否已广播确认，取代原 <c>IzakayaSelectorPanelPatch.confirmed</c>。</summary>
    private static bool s_confirmed;

    /// <summary>待执行的本机地图确认（地图对象 + 等级）。</summary>
    private static IGuideMapSpot s_pendingMapSpot;
    private static IzakayaLevel s_pendingMapLevel;
    private static bool s_mapConfirmPending;

    /// <summary>本模组触发的原版确认会再次回调本监听，用标志跳过这次回声。</summary>
    private static bool s_applyingConfirmed;

    /// <summary>当前备菜面板视图，取代原 <c>IzakayaConfigPannelPatch.instanceRef</c>；面板打开时由监听参数刷新。</summary>
    private static PrepConfigView s_configView;

    /// <summary>远端存放食物期间跳过回广播，取代原 <c>IzakayaConfigurePatch._skipPatchStoreFood</c>。</summary>
    private static bool s_applyingRemoteStore;

    /// <summary>待执行的「开始营业」推进；由 <see cref="PrepOver"/> 置起，在备菜场景循环里调用服务。</summary>
    private static bool s_completePending;

    #endregion

    #region 备菜场景循环

    public void Setup(IPrepNightSceneServices services)
    {
        s_mapServices = services.Map;
        s_configView = null;
        s_submittedSpot = null;
        s_submittedLabel = MapLabel.Unknown;
        s_mapConfirmPending = false;
        s_completePending = false;

        // 联机时拦住原版地图确认，改由本模组的选店共识推进；没进房保持原版行为。
        s_mapConfirmGated = GameSession.HasRoomPeers;
        services.Map.SetConfirmEnabled(!s_mapConfirmGated);

        ApplyCompleteGate(services);

        // 面板打开期间不接受同步编辑：原补丁在 OnPanelOpen 前后夹住该窗口，
        // 框架没有 OnPanelOpen 回调，改为从场景 Setup 到面板第一次 GoToSpecific。
        PrepSceneManager.IsOpeningPanel = true;
    }

    /// <summary>
    /// 每帧重申「开始营业」开关（该开关随场景重置），并执行排队的营业推进。
    /// 地图确认的推进在白天场景循环里执行。
    /// </summary>
    public void Update(IPrepNightSceneServices services, float delta)
    {
        ApplyCompleteGate(services);

        if (!s_completePending) return;
        s_completePending = false;
        // 原回调已被开关拦住，由服务放行同一个原版回调（预计算本夜菜单 + 关面板进营业）。
        services.Session.Confirm();
    }

    /// <summary>
    /// 「开始营业」开关。原前缀只在联机、且幽幽子轮次已开始备菜时才拦下按钮回调；其余情况
    /// （没进房、试炼轮次尚未开始）一律放行原版，因此这里逐帧按同一条件重申。
    /// </summary>
    private static void ApplyCompleteGate(IPrepNightSceneServices services)
    {
        var gated = GameSession.HasRoomPeers
            && (!PrepSceneManager.IsYuyukoChallenge || PrepSceneManager.IsYuyukoPrepActive);
        services.Session.SetCompleteEnabled(!gated);
    }

    public void Shutdown(IPrepNightSceneServices services)
    {
        PrepSceneManager.IsOpeningPanel = false;
        s_mapConfirmPending = false;
    }

    #endregion

    #region 白天场景循环

    public void Setup(IDaySceneServices services) => s_confirmed = false;

    /// <summary>
    /// 本机确认由网络消息或本机点击发起，都在服务作用域之外，所以排到这里执行
    /// （场景服务只在场景循环的 Setup/Update/Shutdown 内可用）。
    /// </summary>
    public void Update(IDaySceneServices services, float delta)
    {
        if (!s_mapConfirmPending) return;
        s_mapConfirmPending = false;
        if (s_mapServices == null) return;

        s_applyingConfirmed = true;
        try
        {
            s_mapServices.Confirm(s_pendingMapSpot, s_pendingMapLevel);
        }
        finally
        {
            s_applyingConfirmed = false;
        }
    }

    public void Shutdown(IDaySceneServices services) => s_mapConfirmPending = false;

    #endregion

    #region 选店

    /// <summary>
    /// 选店面板点「前往营业」之后，取代原补丁在 <c>_OnGuideMapInitialize_b__21_0</c> 上的前缀：
    /// 联机时原版确认已被 <see cref="IPrepNightMapServices.SetConfirmEnabled"/> 拦住，这里广播选择并推进共识。
    /// </summary>
    public void OnGuideMapConfirmed(GuideMapView view)
    {
        if (s_applyingConfirmed) return;
        if (!s_mapConfirmGated) return;

        var spot = view.SelectedSpot;
        var level = (IzakayaLevel)view.SelectedLevel;
        if (spot == null || level == IzakayaLevel.Null)
        {
            Log.Error("Guide map confirmed without a selected spot, cannot proceed.");
            return;
        }

        var label = MapLabelExtensions.FromMapKey(spot.PrimaryName);
        Log.Message($"Selected Spot: {label.ToMapKey()}, Level: {(int)level}");

        // 记录自己的选择
        PlayerManager.Local.IzakayaMapLabel = label;
        PlayerManager.Local.IzakayaLevel = (int)level;
        s_submittedSpot = spot;
        s_submittedLabel = label;

        if (!GameSession.HasRoomPeers)
        {
            // 中途退房：原版确认已被拦住，按本机选择直接推进（等价原补丁放行原方法）。
            QueueConfirmedSelection(label, level);
            return;
        }

        // 广播自己的选择
        SelectIzakayaMessage.Send(label, (int)level);

        if (GameSession.IsRoomClient)
        {
            // 客机：发送 SELECT 后等待主机 CONFIRM，同时展示当前状态
            InGameConsole.ShowPassive(TextId.WaitingForHostConfirm.Get(label.FormatIzakayaSelection((int)level)));
            ShowSelectionStatus();
            return;
        }
        // 主机：检查所有 peer 是否已选择且一致
        TryConfirmSelection();
    }

    /// <summary>
    /// 主机侧：检查全员选店是否一致，若一致则广播 CONFIRM_SELECT 并排入本机推进。
    /// 由于来源是本地触发或同步时间触发，因此主机自身也需要覆写选择。
    /// </summary>
    public static void TryConfirmSelection()
    {
        if (s_confirmed || !GameSession.IsRoomHost || GameFlow.Destination != DayDestination.Business) return;
        var mapLabel = PlayerManager.Local.IzakayaMapLabel;
        var level = PlayerManager.Local.IzakayaLevel;

        // 主机自己还没选择
        if (!mapLabel.IsSelected() || level == 0) return;
        if (PlayerManager.Peers.Count > 0 && !PlayerManager.AllPeersSelectedSameIzakaya(mapLabel, level)) return;

        // 全员一致 → 广播 CONFIRM_SELECT → 本地执行切换
        var mySelect = mapLabel.FormatIzakayaSelection(level);
        Log.LogMessage($"All peers match selection: {mySelect}, broadcasting CONFIRM and proceeding");
        s_confirmed = true;
        ConfirmIzakayaMessage.Send(mapLabel, level);
        InGameConsole.ShowPassive(TextId.SelectedIzakaya.Get(mySelect));

        QueueConfirmedSelection(mapLabel, (IzakayaLevel)level);
    }

    /// <summary>
    /// 客机侧：收到其他玩家的 SELECT 后，显示当前全员选店状态摘要。
    /// </summary>
    public static void ShowSelectionStatus()
    {
        var myMapLabel = PlayerManager.Local.IzakayaMapLabel;
        var myLevel = PlayerManager.Local.IzakayaLevel;

        // 自己还没选，不显示摘要
        if (!myMapLabel.IsSelected() || myLevel == 0) return;

        var mySelect = myMapLabel.FormatIzakayaSelection(myLevel);

        if (!PlayerManager.AllPeersSelectedSameIzakaya(myMapLabel, myLevel))
        {
            var mismatch = PlayerManager.GetFirstMismatchSelection(myMapLabel, myLevel);
            InGameConsole.ShowPassive(TextId.SelectedIzakayaMismatch.Get(mySelect, mismatch ?? "???"));
        }
    }

    /// <summary>
    /// 客机侧：收到主机 CONFIRM（原 <c>ConfirmIzakayaMessage</c> 的调用点）。原方法里的反向补丁不再需要，
    /// 改用服务触发原版确认。
    /// </summary>
    public static void TryProceedWithConfirmedSelection(MapLabel mapLabel, IzakayaLevel mapLevel)
    {
        if (!s_mapConfirmGated) return;
        QueueConfirmedSelection(mapLabel, mapLevel);
    }

    /// <summary>
    /// 用提交时记下的地图对象恢复面板字段并触发原版确认（原 <c>TryProceedWithConfirmedSelection</c>）：
    /// 直接恢复原入口读取的字段，避免选点回调重置等级。
    /// </summary>
    private static void QueueConfirmedSelection(MapLabel mapLabel, IzakayaLevel mapLevel)
    {
        Panel.CloseActivePanelsBeforeSceneTransit();
        if (s_submittedSpot == null || s_submittedLabel != mapLabel)
        {
            Log.Error($"No submitted spot for the confirmed selection {mapLabel.ToMapKey()}, cannot proceed.");
            return;
        }

        s_pendingMapSpot = s_submittedSpot;
        s_pendingMapLevel = mapLevel;
        s_mapConfirmPending = true;
    }

    #endregion

    #region 菜单逐项同步

    /// <summary>取代原 <c>RegisterToDailyRecipes</c> 前缀：对端缺 DLC 时跳过原版注册，否则广播本次新增。</summary>
    public void OnPreRecipeAdded(int id, ref bool cancelInvocation)
    {
        if (GameSession.HasRoomPeers && !PlayerManager.RecipeAvailable(id))
        {
            Log.LogWarning($"Peer does not have recipe {id}, skipping...");
            InGameConsole.ShowPassiveFromAnyThread(TextId.DLCPeerRecipeNotAvailable.Get(id));
            cancelInvocation = true;
            return;
        }

        new UpdatePrepMessage { AddedRecipes = [id] }.Submit();
    }

    /// <summary>取代原 <c>RegisterToDailyBeverages</c> 前缀。</summary>
    public void OnPreBeverageAdded(int id, ref bool cancelInvocation)
    {
        if (GameSession.HasRoomPeers && !PlayerManager.BeverageAvailable(id))
        {
            Log.LogWarning($"Peer does not have beverage {id}, skipping...");
            InGameConsole.ShowPassiveFromAnyThread(TextId.DLCPeerBeverageNotAvailable.Get(id));
            cancelInvocation = true;
            return;
        }

        new UpdatePrepMessage { AddedBeverages = [id] }.Submit();
    }

    /// <summary>取代原 <c>RegisterToCookers</c> 前缀；厨具注销也会经过该方法。</summary>
    public void OnPreCookerAssigned(int id, int index, ref bool cancelInvocation)
    {
        if (index < 0 || index >= IzakayaConfigure.Instance.CookerConfigure.Length)
        {
            Log.LogWarning($"RegisterToCookers out of range: id={id}, index={index}");
            cancelInvocation = true;
            return;
        }

        if (id != -1 && GameSession.HasRoomPeers && !PlayerManager.CookerAvailable(id))
        {
            Log.LogWarning($"Peer does not have cooker {id}, skipping...");
            InGameConsole.ShowPassiveFromAnyThread(TextId.DLCPeerCookerNotAvailable.Get(id));
            cancelInvocation = true;
            return;
        }

        new UpdatePrepMessage { ChangedCookers = new() { [index] = id } }.Submit();
    }

    public void OnRecipeRemoved(int id) => new UpdatePrepMessage { RemovedRecipes = [id] }.Submit();

    public void OnBeverageRemoved(int id) => new UpdatePrepMessage { RemovedBeverages = [id] }.Submit();

    /// <summary>
    /// 存放食物（原 <c>StoreFood_Original</c>）：远端同步的重放入口。进入时置重入标志，
    /// 使这次调用触发的 <see cref="OnFoodStored"/> 不再回广播（取代原静态布尔闩）。
    /// </summary>
    public static void StoreFood(Sellable sellable, int messageSender = -1)
    {
        s_applyingRemoteStore = true;
        try
        {
            IzakayaConfigure.Instance.StoreFood(sellable, messageSender);
        }
        finally
        {
            s_applyingRemoteStore = false;
        }
    }

    /// <summary>取代原 <c>StoreFood</c> 前缀。</summary>
    public void OnFoodStored(Sellable sellable)
    {
        Log.LogInfo($"StoreFood: {sellable.Text.Name}");
        if (s_applyingRemoteStore) return;
        if (!GameSession.HasRoomPeers) return;

        var food = SellableFood.FromSellable(sellable);
        StoreFoodMessage.Send(food);
    }

    #endregion

    #region 配置写回

    /// <summary>取代原 <c>IzakayaConfigure.Initialize</c>/<c>UpdateValue</c> 后缀，写回作弊客流倍率。</summary>
    public void OnConfigureUpdated(IzakayaConfigure configure) => ApplyConfiguredFlowRate(configure);

    private static void ApplyConfiguredFlowRate(IzakayaConfigure configure)
    {
        if (GameSession.IsRoomClient) return;

        float rate = ConfigManager.CheatFlowRate.Value;
        if (rate == 0f || rate == 1f || float.IsNaN(rate) || rate < 0f || rate >= 16f) return;
        if (configure == null) return;

        var normalInterval = configure.NormalGuestInterval;
        configure.NormalGuestInterval = new Vector2(normalInterval.x / rate, normalInterval.y / rate);
        configure.SpecialGuestGachaInterval /= rate;
        string rateText = rate.ToString("0.###", CultureInfo.InvariantCulture);
        Log.LogInfo($"Guest flow rate {rate}x applied to izakaya configuration.");
        InGameConsole.ShowPassiveFromAnyThread(TextId.CheatFlowRateActive.Get(rateText));
    }

    #endregion

    #region 备菜面板

    /// <summary>当前备菜面板视图；<c>PrepSceneManager.UpdateUI</c> 与幽幽子收尾按原 <c>instanceRef</c> 的语义使用。</summary>
    internal static PrepConfigView ConfigPanel => s_configView;

    /// <summary>刷新面板的完成度与三个分组，取代原 <c>PrepSceneManager.UpdateUI</c> 里的面板写回。</summary>
    internal static void RefreshConfigUI() => s_configView?.UpdateUi();

    /// <summary>
    /// 页签切换，取代原 <c>IzakayaConfigPannel.GoToSpecific</c> 后缀。面板打开时原版会在 OnPanelOpen 末尾
    /// 调用一次 <c>GoToSpecific</c>，那一次的 <see cref="PrepSceneManager.IsOpeningPanel"/> 仍为 true：
    /// 结束开启窗口并开始本轮备菜，与原本挂在 OnPanelOpen 上的行为一致。
    /// </summary>
    public void OnConfigTabSelected(PrepConfigView view)
    {
        // 视图按打开的面板缓存：PC 上按 Buffered 生命周期复用时前后两次是同一个视图，因此视图实例是否变化
        // 等价于原面板实例是否变化。开启窗口由 PrepSceneManager.IsOpeningPanel 表达（场景 Setup 或上一轮
        // PrepOver 置起），视图变化只作为平台按 Temp 生命周期重建面板时的兜底。
        var opening = PrepSceneManager.IsOpeningPanel || view != s_configView;
        s_configView = view;

        if (!opening)
        {
            if (!PrepSceneManager.CanSyncEdits) return;
            // 切页会按本机库存清理厨具，随后恢复联机配置；不产生新的修改请求。
            if (GameSession.IsRoomHost) PrepSceneManager.UpdateGroups();
            else PrepSceneManager.UpdateCookers();
            PrepSceneManager.UpdateUI();
            return;
        }

        PrepSceneManager.IsOpeningPanel = false;
        PrepSceneManager.TryBeginYuyukoPrep();
        if (!PrepSceneManager.IsYuyukoChallenge) PrepSceneManager.BeginPrep();
    }

    /// <summary>
    /// 面板点「开始营业」，取代原 <c>_SolveDailyCompletion_b__64_7</c> 前缀：记录本机就绪、广播、
    /// 主机侧检查全员后收尾。原按钮回调已被 <see cref="ApplyCompleteGate"/> 拦住，
    /// 真正进入营业由 <see cref="PrepOver"/> 排队到场景循环里放行原版回调完成。
    /// </summary>
    public void OnPrepConfirmed(PrepConfigView view)
    {
        s_configView = view;

        if (!GameSession.HasRoomPeers) return;
        if (PrepSceneManager.IsYuyukoChallenge)
        {
            if (!PrepSceneManager.IsYuyukoPrepActive) return;
            if (PlayerManager.LocalIsPrepOver) return;
        }

        PlayerManager.LocalIsPrepOver = true;
        InGameConsole.ShowPassive(TextId.MystiaReadyForWork.Get());
        PrepReadyMessage.Send();
        if (GameSession.IsRoomHost) PrepSceneManager.TryCompletePrep();
    }

    /// <summary>
    /// 备菜收尾，取代原 <c>IzakayaConfigPannelPatch.PrepOver</c>：状态复位、关掉压栈的面板，
    /// 再排队放行原版「开始营业」回调（预计算本夜菜单并进入营业）。
    /// 每台机器都由本方法触发原回调：本机点按的原版回调已被开关拦住，未点按的玩家（主机强制收尾）同样如此。
    /// </summary>
    public static void PrepOver()
    {
        Log.Info("PrepOver called");
        if (PrepSceneManager.IsYuyukoChallenge)
        {
            if (!PrepSceneManager.IsYuyukoPrepActive) return;
            PrepSceneManager.EndYuyukoPrep();
            // 试炼每一轮都会在同一场景里重新打开配置面板，这里重新置起开启窗口，下一次 GoToSpecific 结束它。
            PrepSceneManager.IsOpeningPanel = true;
        }
        else
        {
            PlayerManager.ResetState();
        }
        string[] exceptPanels = ["WorkSceneTrayPannel(Clone)", "WorkSceneSustainedPannel(Clone)"];  // 白玉楼测验
        Panel.ClosePanelUntil("IzakayaConfigPannelNew(Clone)", exceptPanels);
        s_completePending = true;
    }

    #endregion
}
