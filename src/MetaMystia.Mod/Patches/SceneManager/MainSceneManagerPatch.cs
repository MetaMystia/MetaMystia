using HarmonyLib;

using Common.UI;
using MainScene;

using MetaMystia.Patch;
using MetaMystia.UI;
using SgrYuki;

namespace MetaMystia;


[HarmonyPatch(typeof(MainScene.SceneManager))]
[AutoLog]
public partial class MainSceneManagerPatch
{
    public static bool FirstEnterMain { get; private set; } = true;

    [HarmonyPatch(nameof(SceneManager.Awake))]
    [HarmonyPostfix]
    public static void MainScene_Awake_Postfix()
    {
        GameFlow.OnSceneTransit(Scene.MainScene);
        L10n.PostInitializeTable();
        if (FirstEnterMain)
        {
            Log.Info("First time entering Main Scene.");
            Log.Info($"Game Version: {Plugin.GameVersion}");
            if (Plugin.GameVersion != Plugin.TargetGameVersion)
            {
                Log.Warning($"Game version does not match target version! Expected: {Plugin.TargetGameVersion}");
                InGameConsole.LogToConsole($"<color=#FF6666>{TextId.GameVersionMismatchNotify.Get(Plugin.TargetGameVersion, Plugin.GameVersion)}</color>");
            }
            Il2CppInteropPatcher.NotifyIfPatched();
            MetricsReporter.OnEnterMainScene();
            Log.Info(MultiplayerStatus.DebugText);
        }
        FirstEnterMain = false;

        InGameConsole.FlushDeferred();
    }
}
