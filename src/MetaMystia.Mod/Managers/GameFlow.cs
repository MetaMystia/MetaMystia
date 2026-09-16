using Common.UI;
using GameData.RunTime.Common;

using MetaMystia.Multiplayer;

namespace MetaMystia;

/// <summary>游戏场景与剧情状态，不拥有连接和玩家身份。</summary>
public static class GameFlow
{
    public static Scene LocalScene { get; private set; } = Scene.EmptyScene;
    public static bool IsMultiplayerAvailable { get; private set; }
    public static bool InStory { get; private set; }
    public static bool CharactersReady { get; private set; }
    public static bool IsGameplaySyncActive => GameSession.HasPeers && !InStory;
    public static bool ShouldSkipAction => !IsGameplaySyncActive;
    public static bool IsPureDay => LocalScene == Scene.DayScene && CharactersReady && !InStory
        && !Patch.UniversalGameManagerPatch.WaitingForAdmission
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
        if (scene != Scene.DayScene) DayDestinationManager.Reset();
        if (scene != Scene.WorkScene) PrepSceneManager.ResetYuyukoPrep();
        LocalScene = scene;
        if (scene == Scene.MainScene)
        {
            IsMultiplayerAvailable = true;
            GameSession.Stop();
            return;
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
}
