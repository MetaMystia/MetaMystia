using System.Linq;

using Common.UI;
using GameData.RunTime.Common;

using MetaMystia.Multiplayer;
using MetaMystia.Network;
using MetaMystia.UI;

namespace MetaMystia;

/// <summary>游戏场景与剧情状态，不拥有连接和玩家身份。</summary>
public static partial class GameFlow
{
    public static Scene LocalScene { get; private set; } = Scene.EmptyScene;
    public static bool IsMultiplayerAvailable { get; private set; }
    public static bool InStory { get; private set; }
    public static bool CharactersReady { get; private set; }
    public static DayDestination Destination { get; private set; }
    public static bool IsCooperative => Destination != DayDestination.None;
    public static bool IsFinalTrial => Destination is DayDestination.FinalTrial or DayDestination.FinalTrialAgain;
    public static GameStage Stage => LocalScene switch
    {
        Scene.MainScene => GameStage.MainMenu,
        Scene.DayScene when IsCooperative => GameStage.DayEnd,
        Scene.DayScene when CharactersReady && !InStory && !PlayerManager.LocalIsDayOver
            && RunTimeScheduler.CurrentGamePhase == RunTimeScheduler.GamePhase.Day => GameStage.Day,
        Scene.IzakayaPrepScene => GameStage.Preparation,
        Scene.WorkScene when CharactersReady => GameStage.Work,
        Scene.ResultScene => GameStage.Result,
        Scene.LoadScene => GameStage.Loading,
        _ => GameStage.Unavailable,
    };
    public static bool CanJoin => Stage is GameStage.MainMenu or GameStage.Day;
    public static bool CanOpenRoom => CanJoin && !DayDestinationManager.HasLocalIntent
        && GameSession.Room?.Members.All(p => p.Uid == GameSession.Client.Uid
            || p.Stage is GameStage.MainMenu or GameStage.Day) == true;
    public static bool IsGameplaySyncActive => GameSession.HasRoomPeers && !InStory;
    public static bool ShouldSkipAction => !IsGameplaySyncActive;
    public static bool IsPureDay => LocalScene == Scene.DayScene && CharactersReady && !InStory
        && !DayDestinationManager.IsEntering && !DayDestinationManager.HasLocalIntent
        && !PlayerManager.LocalIsDayOver && PlayerManager.CharacterSpawnedAndInitialized
        && RunTimeScheduler.CurrentGamePhase == RunTimeScheduler.GamePhase.Day;
#if DEBUG
    public static int WorkTimeSecondOverride = 30;
#else
    public static int WorkTimeSecondOverride = 9 * 60;
#endif

    public static void RefreshInStoryCache()
    {
        var director = Common.SceneDirector.Instance?.playableDirector;
        InStory = director != null && director.state is UnityEngine.Playables.PlayState.Playing or UnityEngine.Playables.PlayState.Delayed;
    }

    public static void OnSceneTransit(Scene scene)
    {
        CharactersReady = false;
        PlayerManager.OnSceneUnloading();
        // 在新白天触发剧情入口之前结束上一段玩法；加载就绪另由 Stage 发布。
        if (scene == Scene.DayScene && IsCooperative) ResetGameplay(resetEntry: false);
        if (IsCooperative && LocalScene == Scene.DayScene && scene != Scene.DayScene) DayDestinationManager.Reset();
        LocalScene = scene;
        if (scene == Scene.MainScene)
        {
            IsMultiplayerAvailable = true;
            PlayerManager.Local.ResetState();
        }
        PlayerProfile.SendProfile();
        if (GameSession.IsRoomHost && scene != Scene.DayScene) GameSession.SetJoinable(false);
    }

    public static void OnCharactersReady(Scene scene)
    {
        if (LocalScene != scene) return;
        CharactersReady = true;
        PlayerManager.InitLocalSkin();
        PlayerManager.RefreshCharacters();
        PlayerProfile.SendProfile();
        PlayerProfile.SendMotion();
    }

    public static void BeginCooperative(DayDestination destination)
    {
        Destination = destination;
        PlayerProfile.SendProfile();
    }

    public static void ResetGameplay(bool resetEntry = true)
    {
        Destination = DayDestination.None;
        // 离开白天已清理旧入口，正常返回时保留较快成员提交的下一次意向。
        if (resetEntry) DayDestinationManager.Reset();
        PrepSceneManager.ClearPrepTable();
        PrepSceneManager.ResetYuyukoPrep();
        BusinessStart.Reset();
        PlayerManager.Local.ResetState();
        foreach (var peer in PlayerManager.Peers.Values) peer.ResetState();
    }

    // 存档入口在 LoadScene 之前重置游戏状态，必须在入口处停止联机业务。
    public static void BeforeStateReset()
    {
        if (IsCooperative) DisconnectForTransition();
        else DayDestinationManager.WithdrawLocal();
        PrepSceneManager.ClearPrepTable();
        PrepSceneManager.ResetYuyukoPrep();
        PlayerManager.Local.ResetState();
        CharactersReady = false;
        PlayerManager.OnSceneUnloading();
        if (LocalScene != Scene.MainScene) LocalScene = Scene.LoadScene;
        PlayerProfile.SendProfile();
    }

    public static void DisconnectForTransition()
    {
        if (GameSession.IsConnectingOrOnline)
        {
            GameSession.Stop(resumeGameplay: false);
            InGameConsole.ShowPassive(TextId.NetworkFlowInterrupted.Get());
        }
        ResetGameplay();
    }

    public static void BeforeSceneLoad(Scene target)
    {
        bool supported = CanKeepConnection(LocalScene, target, Destination,
            DayDestinationManager.ReplayingChallenge, Patch.NightSceneDirectorPatch.ReturningFromTrial);
        if (GameSession.IsConnectingOrOnline && !supported) DisconnectForTransition();
        if (!IsCooperative) DayDestinationManager.WithdrawLocal();
        OnSceneTransit(Scene.LoadScene);
    }
}
