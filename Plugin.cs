using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine.UI;
using System;

using MetaMystia.Patch;
using MetaMystia.ResourceEx.Addressables;
using MetaMystia.UI;
using SgrYuki;

namespace MetaMystia;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    public static Plugin Instance;
    public static string GameVersion => Common.LoadingSceneManager.VersionData;
    public static string TargetGameVersion => "RELEASE 4.4.0e";
    public readonly static string ModVersion = MyPluginInfo.PLUGIN_VERSION;

    public static bool AllPatched => PatchRegistry.AllPatched;

    public Plugin()
    {
        Instance = this;
    }

    public override void Load()
    {
        ConfigManager.InitConfigs();
        L10n.Initialize();
        Il2CppInteropPatcher.TryPatch();

        if (ConfigManager.Debug.Value)
        {
            Log.LogWarning("MetaMystia Debug mode is enabled.");
            InGameConsole.LogToConsole("<color=#FFAA44>MetaMystia 调试模式已启用</color>");
        }

        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch { }
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<PluginManager>();
            Log.LogInfo("Registered C# Types in Il2Cpp");
        }
        catch (Exception ex)
        {
            Log.LogError($"FAILED to Register Il2Cpp Type! {ex.Message}");
        }

        Log.LogInfo(MpManager.DebugText);

        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        var originalHandle = AccessTools.Method(typeof(CanvasScaler), "Handle");
        var postHandle = AccessTools.Method(typeof(BootstrapPatch), "Handle");
        harmony.Patch(originalHandle, postfix: new HarmonyMethod(postHandle));

        PatchRegistry.ApplyAll(harmony);

        Network.Action.RegisterAllFormatter();

        try
        {
            RuntimeAddressables.Initialize();
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Early RuntimeAddressables init failed (will retry later): {ex.Message}");
        }

        try
        {
            ResourceExManager.Initialize();
        }
        catch (Exception ex)
        {
            Log.LogFatal($"FAILED to Initialize ResourceEx! {ex.Message}");
            PatchRegistry.PatchedException = ex;
        }
    }

    public static void OnFirstEnterMainScene()
    {
        Instance?.Log.LogInfo($"Game Version: {GameVersion}");
        if (GameVersion != TargetGameVersion)
        {
            Instance?.Log.LogWarning($"Game version does not match target version! Expected: {TargetGameVersion}");
            InGameConsole.LogToConsole($"<color=#FF6666>{UI.TextId.GameVersionMismatchNotify.Get(TargetGameVersion, GameVersion)}</color>");
        }
        Il2CppInteropPatcher.NotifyIfPatched();
        MetricsReporter.OnEnterMainScene();
        Instance?.Log.LogInfo(MpManager.DebugText);
    }

    class BootstrapPatch
    {
        [HarmonyPostfix]
        static void Handle()
        {
            if (PluginManager.Instance == null)
            {
                Instance.Log.LogMessage("Bootstrapping Trainer...");
                try
                {
                    PluginManager.Create("PluginManager");
                    if (PluginManager.Instance != null)
                    {
                        Instance.Log.LogMessage("Trainer Bootstrapped!");
                    }
                }
                catch (Exception e)
                {
                    Instance.Log.LogMessage($"ERROR Bootstrapping Trainer: {e.Message}");
                }
            }
        }
    }
}
