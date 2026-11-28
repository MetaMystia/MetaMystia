using System.Collections.Generic;
using System.Linq;

using GameData.Core.Collections.CharacterUtility;
using GameData.Profile;
using GameData.RunTime.Common;
using NightScene.CookingUtility;
using NightScene.EventUtility;
using NightScene.PartnerUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Guests;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Patch;
using MetaMystia.UI;


namespace MetaMystia;

/// <summary>
/// 料理对决局内：占桌与比分。各端把本队映射为原版玩家方、对方队映射为 Boss 方（旁观以红队为玩家方），
/// 原版按占桌表分流上菜面板、连击、付款与小费。占桌、释放与比分都以房主为准。
/// </summary>
public static class VersusMatch
{
    private sealed class State : IScopeEnd
    {
        public readonly Dictionary<int, VersusTeam> Desks = new();
        /// <summary>房主上次下发的红、蓝比分与连击。</summary>
        public readonly int[] Sent = [-1, -1, -1, -1];
        /// <summary>客机收到的红、蓝比分与连击。</summary>
        public int[] Host;
        public bool Final;
        public bool Announced;
        public PartnerBossInfo BossInfo;
        /// <summary>厨具当前成品或存放物所属的队，按格子编号。</summary>
        public readonly Dictionary<int, VersusTeam> Cookers = new();
        public bool Prepared;
        public bool StockSent;
        public VersusStockMessage Stock;
        public bool StockApplied;
        /// <summary>正在写入原版占桌表（本类写入放行）。</summary>
        public bool Applying;
        /// <summary>房主：当前原版付款、断连击的归属队；None 为未占的桌，null 为按原版记在本端。</summary>
        public VersusTeam? Credit;

        public void OnScopeEnd(bool resume)
        {
            if (BossInfo != null) UnityEngine.Object.Destroy(BossInfo);
            if (PartnerSkin == null) return;
            PartnerSkin = null;
            PlayerProfile.SendProfile();
        }
    }

    private static State Current => Scopes.Scene.Get<State>();
    private static readonly int[] BossIds = [3004, 3003, 3005];

    /// <summary>本局所选伙伴：原版只换本端外观，对局中网络资料以它的外观发给其他端；null 为未换。</summary>
    public static int? PartnerSkin { get; private set; }

    /// <summary>本局 Boss 方占桌登记的伙伴 ID；付款按它查小费倍率。</summary>
    private static int BossId => Current.BossInfo?.PartnerId ?? -1;

    public static bool IsActive => VersusManager.IsActive && GameSession.IsInRoom
        && GameFlow.LocalScene == Common.UI.Scene.WorkScene;

    /// <summary>该成员视角下的玩家方：蓝队为蓝，其余为红。</summary>
    private static VersusTeam Side(int uid) => VersusRoster.TeamOf(uid) == VersusTeam.Blue ? VersusTeam.Blue : VersusTeam.Red;

    public static VersusTeam DeskTeam(int deskCode) => Current.Desks.GetValueOrDefault(deskCode);

    /// <summary>
    /// 开场前（状态 3，原版已按图鉴发库存、选完伙伴）每帧调用：
    /// 注册 Boss 方伙伴资料，关闭 QTE 增益与伙伴的温泉移速，把伙伴外观发给其他端，客机套用房主下发的统一库存。
    /// </summary>
    public static void Prepare(int partnerId)
    {
        var state = Current;
        if (!state.Prepared)
        {
            state.Prepared = true;
            RegisterBossSide(state);
            if (partnerId >= 0)
            {
                PartnerSkin = partnerId;
                PlayerProfile.SendProfile();
            }
            // 伙伴只换外观：QTE 奖励与温泉移速都去掉，所有人的 QTE 都不触发增益。
            var rewards = QTERewardManager.Instance;
            if (rewards != null)
            {
                rewards.ResetBuffReward();
                rewards.disableReward = true;
            }
            EventManager.Instance?.RemoveAllRegisteredTimedBuff(EventManager.BuffType.SlowDown, false);
        }
        ApplyStock(state);
    }

    /// <summary>
    /// 克隆一份大赛 Boss 资料并设为本机等级注册为 Boss 方，
    /// 使 <c>GuestPay</c> 中 Boss 桌小费倍率与本机相同（房主计分，等同两队按房主等级计算）。
    /// </summary>
    private static void RegisterBossSide(State state)
    {
        // 托管侧读不到 bossMapping 内容；Boss 方只借用资料的键与等级倍率，取任一大赛 Boss 即可。
        PartnerBossInfo source = null;
        foreach (int candidate in BossIds)
            if ((source = DataBaseCharacter.RefPartnerInfo(candidate)?.TryCast<PartnerBossInfo>()) != null) break;
        var manager = PartnerManager.Instance;
        if (source == null || manager == null || manager.BossPartnerMatching.ContainsKey(source.PartnerId)) return;
        var clone = UnityEngine.Object.Instantiate(source);
        clone.level = RunTimePlayerData.Level;
        manager.AddBossPartner(clone);
        state.BossInfo = clone;
        Log.Info($"versus: boss side registered as {clone.PartnerId}, level {clone.level}");
    }

    /// <summary>
    /// 房主每帧：全员进入夜间后下发一次统一库存。原版按各自图鉴发放，取房主的并去掉有人不能用的。
    /// 先于开场放行发出，客机在开场前收到。
    /// </summary>
    private static void SendStock()
    {
        var state = Current;
        if (!GameSession.IsRoomHost || state.StockSent || !state.Prepared
            || GameSession.Room.Members.Any(member => member.Stage != Network.GameStage.Work)) return;
        state.StockSent = true;
        var stock = new VersusStockMessage();
        foreach (var pair in RunTimeStorage.Ingredients)
            if (pair.Value < 0 || PlayerManager.IngredientAvailable(pair.Key)) stock.Ingredients[pair.Key] = pair.Value;
        foreach (var pair in RunTimeStorage.Beverages)
            if (pair.Value < 0 || PlayerManager.BeverageAvailable(pair.Key)) stock.Beverages[pair.Key] = pair.Value;
        stock.Send();
        state.Stock = stock;
        ApplyStock(state);
    }

    public static void ReceiveStock(VersusStockMessage stock)
    {
        if (!IsActive) return;
        var state = Current;
        state.Stock = stock;
        state.StockApplied = false;
        ApplyStock(state);
    }

    // 原版状态 1 发完库存后才能覆盖，否则会被原版写回。
    private static void ApplyStock(State state)
    {
        if (state.StockApplied || state.Stock is not { } stock || !state.Prepared) return;
        state.StockApplied = true;
        var ingredients = RunTimeStorage.Ingredients;
        ingredients.Clear();
        foreach (var pair in stock.Ingredients) ingredients[pair.Key] = pair.Value;
        var beverages = RunTimeStorage.Beverages;
        beverages.Clear();
        foreach (var pair in stock.Beverages) beverages[pair.Key] = pair.Value;
        Log.Info($"versus: stock applied, {stock.Ingredients.Count} ingredients, {stock.Beverages.Count} beverages");
    }

    /// <summary>该成员与本端是否同一方；对局外总为真。保温箱等按队隔离的消息据此取舍。</summary>
    public static bool SameSide(int uid) => !IsActive || Side(uid) == Side(PlayerManager.Local.Uid);

    /// <summary>本端是否旁观（对局中不能交互）。</summary>
    public static bool IsSpectating => IsActive && VersusRoster.TeamOf(PlayerManager.Local.Uid) is not (VersusTeam.Red or VersusTeam.Blue);

    /// <summary>厨具开火或存放：成品归操作者的队。</summary>
    public static void SetCookerOwner(int gridIndex, int uid)
    {
        if (IsActive) Current.Cookers[gridIndex] = VersusRoster.TeamOf(uid);
    }

    public static void ClearCookerOwner(int gridIndex)
    {
        if (IsActive) Current.Cookers.Remove(gridIndex);
    }

    /// <summary>本端能否取出该厨具的成品：他队的不能取，旁观都不能取。在厨具交互入口检查。</summary>
    public static bool CanExtract(int gridIndex)
    {
        if (!IsActive) return true;
        if (IsSpectating) return false;
        return !Current.Cookers.TryGetValue(gridIndex, out var owner) || owner == VersusRoster.TeamOf(PlayerManager.Local.Uid);
    }

    /// <summary>房主：裁定上菜前检查。该桌已属他队或操作者是旁观时拒绝。</summary>
    public static bool CanServe(int uid, int deskCode)
    {
        if (!IsActive) return true;
        var team = VersusRoster.TeamOf(uid);
        if (team is not (VersusTeam.Red or VersusTeam.Blue)) return false;
        var owner = DeskTeam(deskCode);
        return owner == VersusTeam.None || owner == team;
    }

    /// <summary>房主：上菜被接受后，未占的桌归操作者的队。</summary>
    public static void Claim(int uid, int deskCode)
    {
        if (!IsActive || !GameSession.IsRoomHost || deskCode < 0 || DeskTeam(deskCode) != VersusTeam.None) return;
        var team = VersusRoster.TeamOf(uid);
        if (team is not (VersusTeam.Red or VersusTeam.Blue)) return;
        VersusDeskMessage.Send(deskCode, team);
        ApplyDesk(deskCode, team);
    }

    /// <summary>房主：顾客离桌时释放。</summary>
    public static void Release(int deskCode)
    {
        if (!GameSession.IsRoomHost || deskCode < 0) return;
        VersusDeskMessage.Send(deskCode, VersusTeam.None);
        ApplyDesk(deskCode, VersusTeam.None);
    }

    /// <summary>按本端视角写入原版占桌表；他队占了本端正在上菜的桌时关闭面板并退回物品。</summary>
    public static void ApplyDesk(int deskCode, VersusTeam team)
    {
        var manager = PartnerManager.Instance;
        if (!IsActive || manager == null) return;
        var desks = Current.Desks;
        if (team == VersusTeam.None) desks.Remove(deskCode);
        else desks[deskCode] = team;
        Current.Applying = true;
        manager.ReleasePlayerOccupiedDeskCode(deskCode);
        manager.ReleaseBossOccupiedDeskCode(deskCode);
        if (team != VersusTeam.None)
        {
            if (team == Side(PlayerManager.Local.Uid)) manager.TryAddPlayerOccupiedDeskCode(deskCode);
            else if (BossId >= 0) manager.TryAddBossOccupiedDeskCode(deskCode, BossId);
        }
        Current.Applying = false;
        if (team == VersusTeam.None || team == VersusRoster.TeamOf(PlayerManager.Local.Uid)) return;
        if (WorkSceneServePannelPatch.PanelDeskCode == deskCode)
            ServeSlots.ClosePanel(deskCode, GuestRegistry.Of(WorkSceneServePannelPatch.instanceRef.currentGuestController), returnLocal: true);
    }

    /// <summary>
    /// 原版占桌、释放入口：对决中只接受本类写入。房主的顾客离桌回调转为广播释放；
    /// 上菜送达的原版占桌由上菜裁定代替。
    /// </summary>
    public static bool AllowOriginalDeskWrite(int deskCode, bool release)
    {
        if (!IsActive || Current.Applying) return true;
        if (release && GameSession.IsRoomHost && DeskTeam(deskCode) != VersusTeam.None) Release(deskCode);
        return false;
    }

    /// <summary>
    /// 房主：原版的差评离桌、赶客与耐心耗尽不查 Boss 占桌表，付款与断连击总记在本端。
    /// 这些调用期间设定归属队：他队的付款记到 Boss 方、断连击改断 Boss 方；未占的桌耐心耗尽时两队都断连击。
    /// </summary>
    public readonly struct CreditScope : System.IDisposable
    {
        private readonly bool owns;

        public CreditScope(VersusTeam team)
        {
            owns = IsActive && GameSession.IsRoomHost && Current.Credit == null;
            if (owns) Current.Credit = team;
        }

        public void Dispose()
        {
            if (owns) Current.Credit = null;
        }
    }

    public static CreditScope Credit(VersusTeam team) => new(team);

    public static CreditScope CreditDesk(int deskCode) => new(IsActive ? DeskTeam(deskCode) : VersusTeam.None);

    /// <summary>房主 <c>FundEdit</c> 前：归属他队时记到 Boss 方并跳过原版。</summary>
    public static bool RedirectFund(float value)
    {
        if (!IsActive || Current.Credit is not (VersusTeam.Red or VersusTeam.Blue) || Current.Credit == Side(PlayerManager.Local.Uid)) return false;
        PartnerManager.Instance.BossGetScore += UnityEngine.Mathf.RoundToInt(value);
        return true;
    }

    /// <summary>房主 <c>LoseAllCombo</c> 前：归属他队或未占的桌时断 Boss 方连击；返回 true 跳过原版（本端不断）。</summary>
    public static bool RedirectLoseCombo()
    {
        if (!IsActive || Current.Credit is not { } team || team is not (VersusTeam.Red or VersusTeam.Blue or VersusTeam.None)
            || team == Side(PlayerManager.Local.Uid)) return false;
        PartnerManager.Instance.SetBossCombo(0);
        return team != VersusTeam.None;
    }

    /// <summary>房主每帧：统一库存，红蓝比分或连击变化时下发。</summary>
    public static void Flush()
    {
        if (IsActive) SendStock();
        if (!IsActive || !GameSession.IsRoomHost || BusinessStart.IsWaitingForStart || Current.Final) return;
        var score = HostScore();
        var sent = Current.Sent;
        if (score[0] == sent[0] && score[1] == sent[1] && score[2] == sent[2] && score[3] == sent[3]) return;
        score.CopyTo(sent, 0);
        VersusScoreMessage.Send(score, final: false);
    }

    /// <summary>房主本端原版数值换算为红、蓝：玩家方为房主视角的一方。</summary>
    private static int[] HostScore()
    {
        var events = EventManager.Instance;
        var partners = PartnerManager.Instance;
        int own = events.EarnedFund, other = partners.BossGetScore;
        int ownCombo = events.CurrentCombo, otherCombo = partners.BossGetCombo;
        return Side(PlayerManager.Local.Uid) == VersusTeam.Red
            ? [own, other, ownCombo, otherCombo]
            : [other, own, otherCombo, ownCombo];
    }

    public static void ReceiveScore(int[] score, bool final)
    {
        if (!IsActive || score is not { Length: 4 }) return;
        var state = Current;
        state.Host = score;
        if (final) state.Final = true;
        ApplyHostScore();
    }

    /// <summary>客机每帧：本地重放评价、付款对比分和连击的改动以房主数值覆盖。</summary>
    public static void ApplyHostScore()
    {
        if (!IsActive || GameSession.IsRoomHost || Current.Host is not { } score) return;
        var events = EventManager.Instance;
        var partners = PartnerManager.Instance;
        if (events == null || partners == null) return;
        bool red = Side(PlayerManager.Local.Uid) == VersusTeam.Red;
        int own = red ? score[0] : score[1], other = red ? score[1] : score[0];
        int ownCombo = red ? score[2] : score[3], otherCombo = red ? score[3] : score[2];
        int gained = own - events.EarnedFund;
        if (gained != 0)
        {
            NightSceneEventManagerPatch.FundEdit_ReversePatch(events, own, EventManager.MathOperation.Set);
            if (gained > 0) NightScene.UI.UIManager.Instance?.tipAndMoneyDisplayerController?.NotifyOrderMoneyMessage(gained);
        }
        if (partners.BossGetScore != other) partners.BossGetScore = other;
        if (partners.BossGetCombo != otherCombo) partners.BossGetCombo = otherCombo;
        if (events.CurrentCombo != ownCombo)
        {
            events.CurrentCombo = ownCombo;
            events.CallExternOnComboUpdate(ownCombo);
        }
    }

    /// <summary>
    /// 主循环状态 9（结算）前：房主下发最终比分；客机等最终比分写入后再运行原版结算。
    /// 返回 false 表示本帧继续等待。
    /// </summary>
    public static bool BeforeResult()
    {
        if (!IsActive) return true;
        var state = Current;
        if (GameSession.IsRoomHost)
        {
            if (!state.Final)
            {
                state.Final = true;
                var score = HostScore();
                VersusScoreMessage.Send(score, final: true);
                Announce(state, score);
            }
            return true;
        }
        if (!state.Final) return false;
        ApplyHostScore();
        Announce(state, state.Host);
        return true;
    }

    private static void Announce(State state, int[] score)
    {
        if (state.Announced) return;
        state.Announced = true;
        var text = score[0] == score[1] ? TextId.VersusDraw.Get(score[0])
            : TextId.VersusWinner.Get(VersusManager.TeamName(score[0] > score[1] ? VersusTeam.Red : VersusTeam.Blue), score[0], score[1]);
        InGameConsole.ShowPassive(text);
    }
}
