using System.Collections;
using System.Linq;

using Common.UI;
using DayScene;
using DayScene.UI;
using DEYU.AdpUISystem.Managers;
using GameData.RunTime.Common;
using NightScene;
using NightScene.EventUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Network;
using MetaMystia.UI;
using SgrYuki.Utils;

using static MetaMystia.UI.DaySceneSelectionMenu;
using MainLoop = GameData.Profile.DLC3_MausoleumCuisineCompetitionBossData._MainChallengeLoop_d__59;

namespace MetaMystia;

/// <summary>
/// 料理对决：房主发起邀请，成员选队后经入口协调进入神灵庙料理大赛的难度模式。
/// 难度模式跳过剧情对话与结果记录，结束后直接回白天；本局去掉 Boss。
/// </summary>
public static class VersusManager
{
    private sealed class State
    {
        public bool Ended;
    }

    private static readonly DaySceneSelectionItem CollabMenuItem = new();
    private static readonly VersusTempo[] Tempos = [VersusTempo.Slow, VersusTempo.Normal, VersusTempo.Fast];
    private static readonly System.Random random = new();
    private const string SustainedPanel = "DaySceneSustainedPannel(Clone)";
    private const string ChatSelectionPanel = "DaySceneChatSelectionPannel(Clone)";
    private static int handledInvite;
    private static bool menuPending;
    private static bool enterQueued;

    public static bool IsActive => GameFlow.Destination == DayDestination.Versus;
    /// <summary>仅在调用原版挑战入口期间为真，供切场景时保留房间。</summary>
    public static bool Entering { get; private set; }

    private static NightSceneDirector.ChallengeType ChallengeType => VersusRoster.Tempo switch
    {
        VersusTempo.Slow => NightSceneDirector.ChallengeType.Story_Seiga_TempleCuisineCompetition,
        VersusTempo.Fast => NightSceneDirector.ChallengeType.Story_Futo_TempleCuisineCompetition,
        _ => NightSceneDirector.ChallengeType.Story_Tochiko_TempleCuisineCompetition,
    };

    public static string TeamName(VersusTeam team) => team switch
    {
        VersusTeam.Red => TextId.VersusTeamRed.Get(),
        VersusTeam.Blue => TextId.VersusTeamBlue.Get(),
        VersusTeam.Watch => TextId.VersusTeamWatch.Get(),
        _ => TextId.VersusTeamNone.Get(),
    };

    /// <summary>邀请或对局期间玩家列表里名字后的队伍标记。</summary>
    public static string TeamTag(int uid)
    {
        if (!VersusRoster.Open && !IsActive) return "";
        var team = VersusRoster.TeamOf(uid);
        var color = team switch { VersusTeam.Red => "#FF8C8C", VersusTeam.Blue => "#8CC8FF", _ => "#8C8698" };
        return $"  <color={color}>{TeamName(team)}</color>";
    }

    /// <summary>对局中头顶名字按队着色，旁观与未分队不变。</summary>
    public static UnityEngine.Color? LabelColor(int uid) => !IsActive ? null : VersusRoster.TeamOf(uid) switch
    {
        VersusTeam.Red => new UnityEngine.Color(1f, 0.55f, 0.55f, 0.9f),
        VersusTeam.Blue => new UnityEngine.Color(0.55f, 0.78f, 1f, 0.9f),
        _ => null,
    };

    public static string TempoName(VersusTempo tempo) => tempo switch
    {
        VersusTempo.Slow => TextId.VersusTempoSlow.Get(),
        VersusTempo.Fast => TextId.VersusTempoFast.Get(),
        _ => TextId.VersusTempoNormal.Get(),
    };

    public static void Reset()
    {
        VersusRoster.Reset();
        handledInvite = 0;
        menuPending = false;
        enterQueued = false;
        Entering = false;
    }

    public static void OpenInvite(VersusTempo tempo)
    {
        VersusRoster.OpenInvite(tempo);
        foreach (var member in GameSession.Room.Members) VersusRoster.Assign(member.Uid);
        Publish();
    }

    public static void CloseInvite()
    {
        VersusRoster.Close();
        Publish();
    }

    public static void OnMemberJoined(int uid)
    {
        if (!GameSession.IsRoomHost) return;
        // 按入房顺序补齐，房主先于新成员分队。
        foreach (var member in GameSession.Room.Members) VersusRoster.Assign(member.Uid);
        Publish();
    }

    public static void OnPeerLeft(int uid)
    {
        if (!GameSession.IsRoomHost || !VersusRoster.Teams.ContainsKey(uid)) return;
        VersusRoster.Remove(uid);
        Publish();
    }

    public static void ReceiveChoice(int uid, int invite, VersusTeam team, bool hasDlc3)
    {
        if (!GameSession.IsRoomHost || !VersusRoster.Open || invite != VersusRoster.Invite || team > VersusTeam.Watch) return;
        if (uid != PlayerManager.Local.Uid && !PlayerManager.Peers.ContainsKey(uid)) return;
        VersusRoster.Choose(uid, team, hasDlc3);
        Publish();
    }

    public static void ApplyState(VersusStateMessage state)
    {
        VersusRoster.Apply(state.Open, state.Invite, state.Tempo, state.Teams, state.Confirmed, state.MissingDlc3);
        OnStateChanged();
    }

    private static void Publish()
    {
        VersusStateMessage.Send();
        OnStateChanged();
    }

    private static void OnStateChanged()
    {
        if (VersusRoster.Open && VersusRoster.Invite != handledInvite && !IsActive)
        {
            handledInvite = VersusRoster.Invite;
            menuPending = true;
            var host = GameSession.Room?.Host ?? 0;
            InGameConsole.ShowPassive(TextId.VersusInvited.Get(LiveModeManager.GetDisplayName(host), TempoName(VersusRoster.Tempo)));
        }
        if (VersusRoster.Open) return;
        menuPending = false;
        if (DayDestinationManager.LocalIntent == DayDestination.Versus) DayDestinationManager.WithdrawLocal();
    }

    public static void Tick()
    {
        // 入口确认后关闭本次邀请，返回白天的成员不再按旧邀请选队。
        if (GameSession.IsRoomHost && VersusRoster.Open && IsActive) CloseInvite();
        VersusMatch.Flush();
        VersusMatch.ApplyHostScore();
        if (!menuPending || GameFlow.Stage != GameStage.Day
            || AdpUIPanelManager.CurrentInputMode != AdpUIPanelManager.RequestedInputMode.Player
            || !AdpUIPanelManager.IsPanelFadeOpsClear()) return;
        menuPending = false;
        if (!VersusRoster.Open || IsActive) return;
        // 人少的一队排在前面。
        VersusTeam[] choices = VersusRoster.Count(VersusTeam.Blue) < VersusRoster.Count(VersusTeam.Red)
            ? [VersusTeam.Blue, VersusTeam.Red, VersusTeam.Watch, VersusTeam.None]
            : [VersusTeam.Red, VersusTeam.Blue, VersusTeam.Watch, VersusTeam.None];
        OpenSelectionMenu(BuildSelectionItems(choices, ChoiceTitle, _ => VersusRoster.Open && !IsActive, Choose), CloseEndButton);
    }

    // 房主在没有邀请时选节奏发起，有邀请时各端选队。
    internal static DaySceneChatSelectionPannel.GetSelectionConfigurationCallback CollabMenuSelection => CollabMenuItem.Set(
        () => TextId.VersusTitle.Get(),
        () => GameSession.HasRoomPeers && !IsActive && (VersusRoster.Open || GameSession.IsRoomHost),
        () =>
        {
            if (VersusRoster.Open) OpenMenu();
            else OpenSelectionMenu(BuildSelectionItems(Tempos, tempo => TextId.VersusTempoItem.Get(TempoName(tempo)),
                _ => GameSession.IsRoomHost && !VersusRoster.Open, OpenInvite), CloseEndButton);
        });

    /// <summary>选队菜单等本机空闲（无面板、非剧情）时再打开，不叠在其他面板上。</summary>
    public static void OpenMenu()
    {
        if (VersusRoster.Open && !IsActive) menuPending = true;
    }

    public static void SetTeam(int uid, VersusTeam team)
    {
        VersusRoster.SetTeam(uid, team);
        Publish();
    }

    public static void Shuffle()
    {
        VersusRoster.Shuffle(GameSession.Room.Members.Select(member => member.Uid), random);
        Publish();
    }

    private static string ChoiceTitle(VersusTeam team)
    {
        if (team == VersusTeam.None) return TextId.VersusDecline.Get();
        var title = TextId.VersusJoinTeam.Get(TeamName(team), VersusRoster.Count(team));
        bool chosen = VersusRoster.Confirmed.Contains(PlayerManager.Local.Uid) && VersusRoster.TeamOf(PlayerManager.Local.Uid) == team;
        return chosen ? TextId.VersusChosen.Get(title) : title;
    }

    public static void Choose(VersusTeam team)
    {
        menuPending = false;
        bool hasDlc3 = RunTimePlayerData.HasDLC3;
        if (GameSession.IsRoomHost) ReceiveChoice(PlayerManager.Local.Uid, VersusRoster.Invite, team, hasDlc3);
        else VersusChoiceMessage.Send(VersusRoster.Invite, team, hasDlc3);
        if (team == VersusTeam.None)
        {
            if (DayDestinationManager.LocalIntent == DayDestination.Versus) DayDestinationManager.WithdrawLocal();
            return;
        }
        if (!hasDlc3)
        {
            InGameConsole.LogError(TextId.VersusDlc3Required.Get());
            return;
        }
        DayDestinationManager.Submit(DayDestination.Versus, Enter);
    }

    private static void Enter()
    {
        if (enterQueued) return;
        enterQueued = true;
        PluginHost.Instance.StartManagedCoroutine(EnterWhenPanelsClosed());
    }

    // LoadScene 会关闭白天常驻面板，其上还有面板时抛异常并停在加载；先关掉选队菜单、对话等再进入。
    private static IEnumerator EnterWhenPanelsClosed()
    {
        int retry = 0;
        while (IsActive && GameSession.IsInRoom && GameFlow.LocalScene == Scene.DayScene)
        {
            bool settled = AdpUIPanelManager.IsPanelFadeOpsClear();
            if (settled && !GameFlow.InStory && Panel.TopPanelName is null or SustainedPanel)
            {
                enterQueued = false;
                Entering = true;
                DaySceneDirector.Instance.StartChallengeSession(
                    ChallengeType, false, NightSceneDirector.Difficulty.Normal, new Il2CppSystem.Nullable<int>());
                Entering = false;
                yield break;
            }
            if (settled && retry-- <= 0)
            {
                retry = 30;
                if (Panel.TopPanelName == ChatSelectionPanel && Panel.TopPanel.ControlledPanel?.IsPanelOpened is true)
                    Panel.TopPanel.ControlledPanel.ClosePanel();
                else Panel.CloseActivePanelsBeforeSceneTransit();
            }
            yield return null;
        }
        enterQueued = false;
    }

    /// <summary>
    /// 主循环状态见 docs/research/versus-competition.md。
    /// 返回 false 时本次不执行原版 MoveNext：状态 3 为选完伙伴后等待全员就绪。
    /// </summary>
    public static bool BeforeLoopStep(MainLoop loop)
    {
        if (!IsActive || !GameSession.IsInRoom) return true;
        if (loop.__1__state == 3) VersusMatch.Prepare();
        if (loop.__1__state == 3 && BusinessStart.IsWaitingForStart)
        {
            BusinessStart.Wait(static () => { });
            return false;
        }
        // 客机本地倒计时停在 1 秒，房主结束后归零，由原版走结束分支。
        if (loop.__1__state == 7 && GameSession.IsRoomClient && Scopes.Scene.Get<State>().Ended)
            EventManager.Instance.TotalCountDown = 0;
        // 状态 9 结算前等房主最终比分。
        if (loop.__1__state == 9 && !VersusMatch.BeforeResult())
        {
            loop.__2__current = null;
            return false;
        }
        return true;
    }

    public static void AfterLoopStep(MainLoop loop, int previousState)
    {
        if (IsActive && GameSession.IsRoomHost && previousState == 7 && loop.__1__state == 8)
            VersusEndMessage.Send();
    }

    public static void ReceiveEnd()
    {
        if (IsActive) Scopes.Scene.Get<State>().Ended = true;
    }
}
