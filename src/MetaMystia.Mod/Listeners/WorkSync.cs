using System;
using System.Collections.Generic;

using Il2CppSystem.Linq;

using Common.TimelineExtestion;
using GameData.Core.Collections;
using GameData.RunTime.NightSceneUtility;
using NightScene.CookingUtility;
using NightScene.GuestManagementUtility;

using Mystia.Listeners;
using Mystia.Scenes;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.UI;

namespace MetaMystia.Listeners;

/// <summary>
/// 营业场景的烹饪、上菜、保温箱与时制同步，取代原 <c>CookControllerPatch</c>、<c>CookSystemManagerPatch</c>、
/// <c>GameTimeManagerPatch</c>、<c>WorkSceneServePannelPatch</c> 与 <c>WorkSceneStoragePannelPatch</c>。
/// </summary>
[AutoLog]
public sealed partial class WorkSync : ICookListener, IWorkListener, IWorkSceneGameLoop
{
    /// <summary>本模组主动关闭上菜面板时跳过一次关闭前同步；取代原 <c>PatchSkipPermit</c>。</summary>
    private static int s_skipServePanelClose;

    /// <summary>正在执行服务驱动的远端动作：此期间本类不再广播，等价于原反向补丁的旁路效果。</summary>
    private static bool s_applyingRemote;

    /// <summary>
    /// 远端动作由网络消息发起，处于服务作用域之外（场景服务只在场景循环的 Setup/Update/Shutdown 内可用），
    /// 因此排队到营业场景循环的 Update 内执行。
    /// </summary>
    private static readonly List<Action<IWorkSceneCook>> s_pendingCooks = [];

    private static bool s_cookCallEnabled = true;

    /// <summary>
    /// 当前打开的上菜面板视图；未打开时为 <c>null</c>。由 <see cref="OnServePanelOpened"/> 建立、
    /// <see cref="OnPreServePanelClosed"/> 释放，取代表板直取。
    /// </summary>
    internal static ServePannelView? ServePanel { get; private set; }

    /// <summary>本模组主动关闭上菜面板前调用：本次关闭不再触发确认上菜同步。</summary>
    internal static void SkipNextServePanelClose() => s_skipServePanelClose++;

    /// <summary>保温箱面板列表刷新；面板未打开时保持原静态缓存为 null 的语义。</summary>
    internal static void RefreshStoragePanel()
    {
        var panel = NightScene.UI.UIManager.Instance?.SustainedPannel?.WorkSceneStoragePannel;
        if (panel == null || !panel.IsPanelOpened) return;
        panel.UpdateFoodField();
        panel.m_FoodsGroup?.UpdateElements();
    }

    /// <summary>排队一个需要调用游戏烹饪方法的远端动作。</summary>
    internal static void EnqueueCook(Action<IWorkSceneCook> apply) => s_pendingCooks.Add(apply);

    #region 营业场景循环

    public void Setup(IWorkSceneServices services)
    {
        // 展示服务（标签、角色像素集）只在场景循环的服务窗口内可用，窗口由本类开合。
        ScenePresentation.Begin(services.Presentation);
        // 等待全员就绪期间阻止原版开厨（原 CookSystemManagerPatch 的前缀跳过）。
        s_cookCallEnabled = !BusinessStart.IsWaitingForStart;
        services.Cook.SetCallEnabled(s_cookCallEnabled);
        if (!s_cookCallEnabled) InGameConsole.ShowPassive(TextId.BusinessStartWaiting.Get());

        // 原 NightSceneEventManagerPatch.Initialize_Postfix：联机下夜间时长改用模组覆盖值。
        if (GameSession.HasRoomPeers) services.Time.WholeNightSeconds = GameFlow.WorkTimeSecondOverride;

        // 原 StartGuestSpawningAndTiming_Prefix：普通营业在开场事件、装饰与伙伴初始化完成后才启动刷客与计时。
        if (!BusinessStart.IsWaitingForStart) return;

        services.Time.SetTimingEnabled(false);
        BusinessStart.Wait(() => GuestSync.EnqueueReplay("begin timing", scoped => scoped.Time.BeginTiming()));
    }

    public void Update(IWorkSceneServices services, float delta)
    {
        ScenePresentation.Pump();

        var callEnabled = !BusinessStart.IsWaitingForStart;
        if (callEnabled != s_cookCallEnabled)
        {
            s_cookCallEnabled = callEnabled;
            services.Cook.SetCallEnabled(callEnabled);
        }

        if (s_pendingCooks.Count == 0) return;

        s_applyingRemote = true;
        try
        {
            foreach (var apply in s_pendingCooks) apply(services.Cook);
        }
        finally
        {
            s_applyingRemote = false;
            s_pendingCooks.Clear();
        }
    }

    public void Shutdown(IWorkSceneServices services)
    {
        ScenePresentation.End();
        ServePanel = null;
        s_skipServePanelClose = 0;
        s_applyingRemote = false;
        s_pendingCooks.Clear();
        s_yuyukoOpenOrders.Clear();
    }

    #endregion

    #region 烹饪

    public void OnPreCookStarted(CookController controller, ref Sellable result, ref Recipe recipe, ref bool cancelInvocation)
    {
        if (YuyukoGuestSync.IsSwallowedCooker(controller.GridIndex))
        {
            cancelInvocation = true;
            return;
        }
        if (GameSession.HasRoomPeers && (!PlayerManager.RecipeAvailable(recipe.Id) || !PlayerManager.FoodAvailable(result.id)))
        {
            Log.LogWarning($"Peer does not have recipe {recipe.Id}, skipping SetCook.");
            InGameConsole.ShowPassive(TextId.DLCPeerRecipeNotAvailable.Get(recipe.Id));
            cancelInvocation = true;
        }
    }

    public void OnPreCookCountdownStarted(CookController controller, ref float qteScore, ref bool cancelInvocation)
    {
        // 联机下 QTE 期间时间继续推进，厨具可能已被吞食，需阻止 QTE 结束后再启动烹饪倒计时。
        if (YuyukoGuestSync.IsSwallowedCooker(controller.GridIndex)) cancelInvocation = true;
    }

    public void OnCookStarted(CookController controller, Sellable result, Recipe recipe, bool couldReturnIngredients)
    {
        if (YuyukoGuestSync.IsSwallowedCooker(controller.GridIndex)) return;
        if (GameFlow.ShouldSkipAction) return;
        if (s_applyingRemote) return;

        SellableFood food = SellableFood.FromSellable(result);
        NightCookMessage.Send(controller.GridIndex, food, recipe.Id);
    }

    public void OnCookCountdownStarted(CookController controller, float qteScore)
    {
        // 原实现在前缀内、吞食判定之后发送，这里重复同一判定以保持发送条件一致。
        if (YuyukoGuestSync.IsSwallowedCooker(controller.GridIndex)) return;
        if (s_applyingRemote) return;

        QTEMessage.Send(controller.GridIndex, qteScore);
    }

    public void OnCookExtracted(CookController controller)
    {
        // 吞食消息已让两端各执行一次原版中断，不能再把其内部 Extract 当作玩家取菜广播。
        // 客机的重放在调用挑战服务时置起中断标记；主机的原版吞食则已先登记为已锁定的厨具。
        if (YuyukoGuestSync.IsInterruptingCooker) return;
        if (YuyukoGuestSync.IsSwallowedCooker(controller.GridIndex)) return;
        if (GameFlow.ShouldSkipAction) return;
        if (s_applyingRemote) return;

        ExtractFromCookerMessage.Send(controller.GridIndex);
    }

    public void OnCookStored(CookController controller, Sellable value)
    {
        if (GameFlow.ShouldSkipAction) return;
        if (s_applyingRemote) return;

        StoreSellableMessage.Send(controller.GridIndex, value);
    }

    #endregion

    #region 上菜面板

    public void OnServePanelOpened(ServePannelView view)
    {
        ServePanel = view;

        if (GameFlow.ShouldSkipAction || !GameSession.HasRoomPeers) return;

        var deskCode = view.DeskCode;
        if (deskCode == -1 || !GuestsManager.Instance.AllGuestInDeskCode.Contains(deskCode)) return;

        var fsm = GuestsMap.GetGuestFsm(GuestsManager.Instance.GetInDeskGuest(deskCode));
        view.PendingFood = fsm.WillServeFood;
        view.PendingBeverage = fsm.WillServeBeverage;
        view.RefreshPendingVisual();
    }

    public void OnPreServePanelClosed(ServePannelView view, ref bool cancelInvocation)
    {
        ServePanel = null;
        if (s_skipServePanelClose > 0)
        {
            s_skipServePanelClose--;
            return;
        }

        if (GameFlow.ShouldSkipAction || !GameSession.HasRoomPeers) return;
        if (GameSession.IsRoomHost)
        {
            GuestFSM.OnConfirmServe(view.Guest, view.PendingFood, view.PendingBeverage);
            return;
        }
        if (GameSession.IsRoomClient)
        {
            GuestFSM.OnConfirmServe(view.Guest, view.PendingFood, view.PendingBeverage);
            return;
        }
        throw new InvalidOperationException("Unexpected network state in OnPreServePanelClosed");
    }

    public void OnPreDishServed(ServePannelView view, ref Sellable dish, ref bool cancelInvocation)
    {
        if (GameFlow.ShouldSkipAction || !GameSession.HasRoomPeers) return;

        if ((dish.Type == Sellable.SellableType.Food && view.Order.ServFood != null) ||
            (dish.Type == Sellable.SellableType.Beverage && view.Order.ServBeverage != null))
        {
            // 已有 料理/酒水，跳过本次上菜
            Log.Info($"Already have {(dish.Type == Sellable.SellableType.Food ? "food" : "beverage")} in order, skipping Patch & Send");
            cancelInvocation = true;
            return;
        }

        if (GameSession.IsRoomHost || GameSession.IsRoomClient)
        {
            Log.Warning($"Send {dish?.Text?.BriefName}");
            GuestFSM.OnServe(view.Guest, dish, dish.Type);
            return;
        }
        throw new InvalidOperationException("Unexpected network state in OnPreDishServed");
    }

    public void OnPreDishCancelled(ServePannelView view, ref Sellable dish, ref bool cancelInvocation)
    {
        if (GameFlow.ShouldSkipAction || !GameSession.HasRoomPeers) return;

        if ((dish.Type == Sellable.SellableType.Food && view.PendingFood == null) ||
            (dish.Type == Sellable.SellableType.Beverage && view.PendingBeverage == null) ||
            (IzakayaTray.Instance?.IsTrayFull ?? true))
        {
            // 没有待上菜 料理/酒水，跳过本次撤回
            Log.Info($"No {(dish.Type == Sellable.SellableType.Food ? "food" : "beverage")} to cancel in order, skipping Patch & Cancel");
            cancelInvocation = true;
            return;
        }

        if (GameSession.IsRoomHost || GameSession.IsRoomClient)
        {
            Log.Warning($"Cancel {dish?.Text?.BriefName}");
            GuestFSM.OnServe(view.Guest, null, dish.Type);
            return;
        }
        throw new InvalidOperationException("Unexpected network state in OnPreDishCancelled");
    }

    #endregion

    #region 本体投掷上菜的延迟回调

    /// <summary>
    /// 本体订单打开上菜面板时记下它的订单序号，取代原 <c>WorkSceneSustainedPannelPatch</c> 前缀在
    /// 8 参回调外面套的一层订单身份判断。一条订单一个序号，因此同一面板的多次开启互不覆盖。
    /// </summary>
    private static readonly Dictionary<nint, int> s_yuyukoOpenOrders = new();

    public void OnServeCallbacksRegistered(ServeCallbackView callbacks)
    {
        var guest = callbacks.Guest;
        if (!YuyukoGuestSync.IsBody(guest)) return;

        var fsm = GuestsMap.GetGuestFsm(guest);
        if (fsm == null) return;
        s_yuyukoOpenOrders[callbacks.Order.Pointer] = fsm.OrderSeq;
    }

    /// <summary>
    /// 本体手动订单的投掷动画可能晚于主机评价和续单完成。旧投掷的延迟回调不能写入新桌面或评价下一单，
    /// 因此这里按「订单身份 + 序号 + 仍在等待上菜」拒绝过期回调。耐心恢复回调在原实现里不被包装，保持放行。
    /// </summary>
    public void OnPreServeCallback(ServeCallbackView callbacks, ServeCallbackKind kind, ref bool cancelInvocation)
    {
        if (kind == ServeCallbackKind.PatientRecover) return;
        if (!s_yuyukoOpenOrders.TryGetValue(callbacks.Order.Pointer, out int seq)) return;

        var guest = callbacks.Guest;
        var fsm = guest == null ? null : GuestsMap.GetGuestFsm(guest);
        bool current = guest != null && fsm != null
            && guest.AllOrdersCount > 0
            && fsm.CurrentOrder?.Pointer == callbacks.Order.Pointer
            && (!GameSession.HasRoomPeers
                || (YuyukoGuestSync.IsBody(guest) && fsm.OrderSeq == seq && fsm.CurrentState == GuestFSM.State.WaitingServe));
        if (current) return;

        cancelInvocation = true;
    }

    #endregion

    #region 保温箱与时制

    public void OnPreStorageExtracted(ref Sellable sellable, ref bool cancelInvocation)
    {
        Log.InfoCaller($"{sellable?.id}, {sellable?.Text?.Name}");
        if (sellable.type == Sellable.SellableType.Beverage)
        {
            if (GameSession.HasRoomPeers && !PlayerManager.BeverageAvailable(sellable.id))
            {
                Log.LogWarning($"Peer does not have beverage {sellable.id}, cannot extract.");
                InGameConsole.ShowPassive(TextId.DLCPeerBeverageNotAvailable.Get(sellable.id));
                cancelInvocation = true;
            }
        }
        else if (sellable.type == Sellable.SellableType.Food)
        {
            if (GameSession.HasRoomPeers && !PlayerManager.FoodAvailable(sellable.id))
            {
                Log.LogWarning($"Peer does not have recipe {sellable.id}, cannot extract.");
                InGameConsole.ShowPassive(TextId.DLCPeerFoodNotAvailable.Get(sellable.id));
                cancelInvocation = true;
                return;
            }
            SellableFood food = SellableFood.FromSellable(sellable);
            ExtractFoodMessage.Send(food);
        }
    }

    public void OnPreTimeModeSet(GameTimeManager manager, ref GameTimeManager.TimeMode mode, ref bool cancelInvocation)
    {
        if (GameFlow.LocalScene == Common.UI.Scene.WorkScene && !GameFlow.ShouldSkipAction)
        {
            mode = GameTimeManager.TimeMode.Resume;
        }
        Log.DebugCaller($"time mode changed to {mode}");
    }

    #endregion
}
