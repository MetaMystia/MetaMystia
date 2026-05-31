using HarmonyLib;

using Common.UI;
using MainScene;

using MetaMystia.UI;

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
        MpManager.OnSceneTransit(Scene.MainScene);
        L10n.PostInitializeTable();
        if (FirstEnterMain)
        {
            Log.Info("First time entering Main Scene.");
            Plugin.OnFirstEnterMainScene();
        }
        FirstEnterMain = false;

        InGameConsole.FlushDeferred();
    }
}
