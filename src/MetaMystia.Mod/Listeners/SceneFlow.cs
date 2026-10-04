using Common.UI;

using Mystia.Scenes;

using MetaMystia.Multiplayer;
using MetaMystia.UI;
using SgrYuki;

namespace MetaMystia.Listeners;

/// <summary>
/// 场景进入阶段（原 <c>Patches/SceneManager</c> 各 <c>SceneManagerPatch</c> 的唤醒/开始逻辑）。
/// Main 与 Day 由 <see cref="ISceneListener.OnSceneAwake"/> 覆盖，其余场景由
/// <see cref="ISceneListener.OnSceneStart"/> 覆盖。场景内逐帧逻辑见 <see cref="DaySync"/>。
/// </summary>
[AutoLog]
public sealed partial class SceneFlow : ISceneListener
{
    private static bool s_enteredMain;

    public void OnSceneAwake(SceneId scene)
    {
        switch (scene)
        {
            case SceneId.Main:
                EnterMain();
                break;
            case SceneId.Day:
                EnterDay();
                break;
        }
    }

    public void OnSceneStart(SceneId scene)
    {
        switch (scene)
        {
            case SceneId.PrepNight:
                GameFlow.OnSceneTransit(Scene.IzakayaPrepScene);
                PrepSceneManager.Initialize();
                PrepSceneManager.FlushBufferedTables();
                break;
            case SceneId.Night:
                EnterNight();
                break;
            case SceneId.Staff:
                GameFlow.OnSceneTransit(Scene.StaffScene);
                break;
            case SceneId.Result:
                GameFlow.OnSceneTransit(Scene.ResultScene);
                break;
        }
    }

    /// <summary>原 <c>MainSceneManagerPatch.MainScene_Awake_Postfix</c>。</summary>
    private static void EnterMain()
    {
        GameFlow.OnSceneTransit(Scene.MainScene);
        L10n.PostInitializeTable();

        if (!s_enteredMain)
        {
            s_enteredMain = true;
            Log.Info("First time entering Main Scene.");
            Log.Info($"Game Version: {ModRuntime.GameVersion}");
            if (ModRuntime.GameVersion != ModRuntime.TargetGameVersion)
            {
                Log.Warning($"Game version does not match target version! Expected: {ModRuntime.TargetGameVersion}");
                InGameConsole.LogToConsole($"<color=#FF6666>{TextId.GameVersionMismatchNotify.Get(ModRuntime.TargetGameVersion, ModRuntime.GameVersion)}</color>");
            }
            MetricsReporter.OnEnterMainScene();
            Log.Info(MultiplayerStatus.DebugText);
            InGameConsole.ShowPassive(TextId.ConsoleTestServerWelcome.Get());
        }

        InGameConsole.FlushDeferred();
    }

    /// <summary>原 <c>DaySceneManagerPatch.Awake_Prefix</c> 与 <c>Awake_Postfix</c>。</summary>
    private static void EnterDay()
    {
        ScheduleSync.ResetFirstTrialGuest();
        GameFlow.OnSceneTransit(Scene.DayScene);
        PlayerManager.Local.ResetState();

        ResourceExManager.OnDaySceneAwake();
        PrepSceneManager.ClearPrepTable();

        if (GameSession.IsOnline)
        {
            PlayerProfile.SendProfile();
        }

        if (!ModRuntime.Ready)
        {
            var warningMessage = TextId.ModInitFailure.Get();
            InGameConsole.LogError(warningMessage);
        }
    }

    /// <summary>原 <c>NightSceneManagerPatch.NightScene_Start_Postfix</c>。</summary>
    private static void EnterNight()
    {
        GameFlow.OnSceneTransit(Scene.WorkScene);
        CheatManager.TryApplyFever();
        PlayerManager.Local.ResetState();
        if (GameSession.HasRoomPeers)
        {
            if (!PrepSceneManager.IsYuyukoPrepActive) PrepSceneManager.ClearPrepTable();
            PlayerManager.ResetState();
        }
        GameFlow.OnCharactersReady(Scene.WorkScene);
    }
}
