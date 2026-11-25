using System;

using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine.UI;

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
    public static string TargetGameVersion => TmiBuildInfo.TargetGameVersion;
    public readonly static string ModVersion = MyPluginInfo.PLUGIN_VERSION;
    public static string ProtocolVersion => ModVersion;

#if DEBUG
    public static TraceLog tl = new TraceLog();
#endif

    public static bool AllPatched => PatchRegistry.AllPatched;

    public Plugin()
    {
        Instance = this;
    }

    public override void Load()
    {
        ConfigManager.InitConfigs();
        L10n.Initialize();
        MystiaLib.Bootstrap.Initialize();
        Hotkeys.Register();

        if (ConfigManager.Debug.Value)
        {
            MetaMystia.Log.Warning("MetaMystia Debug mode is enabled.");
            InGameConsole.LogToConsole("<color=#FFAA44>MetaMystia 调试模式已启用</color>");
        }

        if (ConfigManager.IgnoreDlcDependencyCheck.Value)
        {
            MetaMystia.Log.Warning("DLC and resource pack dependency checks are DISABLED by config. Unknown issues may occur.");
            InGameConsole.LogDeferred(() => TextId.IgnoreDlcDependencyCheckWarning.Get());
        }

        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch { }
        MetaMystia.Log.Info($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<PluginHost>();
            MetaMystia.Log.Info("Registered C# types in Il2Cpp");
        }
        catch (Exception ex)
        {
            MetaMystia.Log.Error($"Failed to register Il2Cpp type: {ex}");
        }

        MetaMystia.Log.Info(MetaMystia.UI.MultiplayerStatus.DebugText);

        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll(typeof(BootstrapPatch));

        PatchRegistry.ApplyAll(harmony);

        Multiplayer.Messages.MultiplayerMessage.RegisterAllFormatter();

        try
        {
            RuntimeAddressables.Initialize();
        }
        catch (Exception ex)
        {
            MetaMystia.Log.Warning($"Early RuntimeAddressables init failed (will retry later): {ex}");
        }

        try
        {
            ResourceExManager.Initialize();
        }
        catch (Exception ex)
        {
            MetaMystia.Log.Fatal($"Failed to initialize ResourceEx: {ex}");
            PatchRegistry.PatchedException = ex;
        }
    }

    [HarmonyPatch(typeof(UnityEngine.UI.CanvasScaler))]
    class BootstrapPatch
    {
        [HarmonyPatch(nameof(CanvasScaler.Handle))]
        [HarmonyPostfix]
        static void Handle()
        {
            if (PluginHost.Instance == null)
            {
                MetaMystia.Log.Info("Creating PluginHost");
                try
                {
                    PluginHost.Create("MetaMystiaHost");
                    if (PluginHost.Instance != null)
                    {
                        MetaMystia.Log.Message("PluginHost created");
                    }
                }
                catch (Exception e)
                {
                    MetaMystia.Log.Error($"Failed to create PluginHost: {e}");
                }
            }
        }
    }
}
