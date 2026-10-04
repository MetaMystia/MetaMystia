using System;

using Mystia;

using MetaMystia.Multiplayer.Messages;
using MetaMystia.ResourceEx;
using MetaMystia.UI;

namespace MetaMystia;

/// <summary>
/// 模组入口：框架在模组注册完成后调用一次（对应原先的 Plugin.Load）。
/// </summary>
[AutoLog]
public sealed partial class ModEntry : IInitialization
{
    public void Initialize(IMod mod)
    {
        ModRuntime.Bind(mod);

        ConfigManager.InitConfigs();
        L10n.Initialize();

        if (ConfigManager.Debug.Value)
        {
            Log.Warning("MetaMystia Debug mode is enabled.");
            InGameConsole.LogToConsole("<color=#FFAA44>MetaMystia 调试模式已启用</color>");
        }

        if (ConfigManager.IgnoreDlcDependencyCheck.Value)
        {
            Log.Warning("DLC and resource pack dependency checks are DISABLED by config. Unknown issues may occur.");
            InGameConsole.LogDeferred(() => TextId.IgnoreDlcDependencyCheckWarning.Get());
        }

        Log.Info($"Plugin {mod.Id} is loaded!");
        Log.Info(MultiplayerStatus.DebugText);

        MultiplayerMessage.RegisterAllFormatter();

        try
        {
            ResourceExManager.Initialize();
        }
        catch (Exception ex)
        {
            Log.Error($"FAILED to Initialize ResourceEx! {ex.Message}");
            ModRuntime.Failure = ex;
        }
    }
}
