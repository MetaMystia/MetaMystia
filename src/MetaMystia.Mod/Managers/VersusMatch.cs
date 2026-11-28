using System.Collections.Generic;

using GameData.Core.Collections.CharacterUtility;
using GameData.Profile;
using GameData.RunTime.Common;
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

        public void OnScopeEnd(bool resume)
        {
            if (BossInfo != null) UnityEngine.Object.Destroy(BossInfo);
        }
    }

    private static State Current => Scopes.Scene.Get<State>();
    private static readonly int[] BossIds = [3004, 3003, 3005];
    private static bool applying;

    /// <summary>本局 Boss 方占桌登记的伙伴 ID；付款按它查小费倍率。</summary>
    private static int BossId => Current.BossInfo?.PartnerId ?? -1;

    public static bool IsActive => VersusManager.IsActive && GameSession.IsInRoom
        && GameFlow.LocalScene == Common.UI.Scene.WorkScene;

    /// <summary>该成员视角下的玩家方：蓝队为蓝，其余为红。</summary>
    private static VersusTeam Side(int uid) => VersusRoster.TeamOf(uid) == VersusTeam.Blue ? VersusTeam.Blue : VersusTeam.Red;

    public static VersusTeam DeskTeam(int deskCode) => Current.Desks.GetValueOrDefault(deskCode);

    /// <summary>
    /// 开场前（状态 3）注册 Boss 方伙伴资料：克隆本局 Boss 资料并设为本机等级，
    /// 使 <c>GuestPay</c> 中 Boss 桌小费倍率与本机相同（房主计分，等同两队按房主等级计算）。
    /// </summary>
    public static void Prepare()
    {
        var state = Current;
        if (state.BossInfo != null) return;
        // 托管侧读不到 bossMapping 内容；Boss 方只借用资料的键与等级倍率，取任一大赛 Boss 即可。
        PartnerBossInfo source = null;
        foreach (int candidate in BossIds)
            if ((source = DataBaseCharacter.RefPartnerInfo(candidate)?.TryCast<PartnerBossInfo>()) != null) break;
        if (source == null) return;
        int id = source.PartnerId;
        var manager = PartnerManager.Instance;
        if (manager == null || manager.BossPartnerMatching.ContainsKey(id)) return;
        var clone = UnityEngine.Object.Instantiate(source);
        clone.level = RunTimePlayerData.Level;
        manager.AddBossPartner(clone);
        state.BossInfo = clone;
        Log.Info($"versus: boss side registered as {id}, level {clone.level}");
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
        applying = true;
        manager.ReleasePlayerOccupiedDeskCode(deskCode);
        manager.ReleaseBossOccupiedDeskCode(deskCode);
        if (team != VersusTeam.None)
        {
            if (team == Side(PlayerManager.Local.Uid)) manager.TryAddPlayerOccupiedDeskCode(deskCode);
            else if (BossId >= 0) manager.TryAddBossOccupiedDeskCode(deskCode, BossId);
        }
        applying = false;
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
        if (applying || !IsActive) return true;
        if (release && GameSession.IsRoomHost && DeskTeam(deskCode) != VersusTeam.None) Release(deskCode);
        return false;
    }

    /// <summary>房主每帧：红蓝比分或连击变化时下发。</summary>
    public static void Flush()
    {
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
