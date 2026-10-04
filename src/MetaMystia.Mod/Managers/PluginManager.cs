using System;

using Mystia;
using Mystia.Imgui;
using Mystia.Scenes;

using GameData.Profile;

using MetaMystia.Listeners;
using MetaMystia.Multiplayer;
using MetaMystia.UI;

using Rect = Mystia.Numerics.Rect;

namespace MetaMystia;

/// <summary>
/// 模组静态业务入口（与其他 Manager 一致）。帧循环驱动见 <see cref="ModLoop"/>。
/// </summary>
[AutoLog]
public static partial class PluginManager
{
    public static string Label
    {
        get
        {
            int packCount = ResourceExManager.LoadedPackages.Count;
            string packLabel = packCount == 1 ? "pack" : "packs";
            return $"{ModRuntime.Id} v{ModRuntime.Version} loaded with {packCount} rex {packLabel}";
        }
    }
    public static bool IsStatusVisible { get; private set; } = true;
    public static bool DEBUG => ConfigManager.Debug.Value;

    /// <summary>
    /// 跨线程调用转发到框架的主线程调度器；泵出由宿主承担。
    /// </summary>
    public static void RunOnMainThread(Action action) => ModRuntime.MainThread.RunOnMainThread(action);

    /// <summary>
    /// 每帧快捷键处理，由 <see cref="ModLoop"/>.Update 调用。
    /// </summary>
    public static void HandleShortcuts()
    {
        if (ModRuntime.Input.IsKeyDown(ConfigManager.KeyToggleLog.Value)) // MystiaKey.RightShift
        {
            Log.LogInfo($"\n");
        }
        if (ModRuntime.Input.IsKeyDown(ConfigManager.KeyToggleStatus.Value)) // MystiaKey.Backslash
        {
            ToggleStatusVisibility();
        }

        if (DEBUG)
        {
            if (ModRuntime.Input.IsKeyDown(MystiaKey.F1))
            {
                GameSession.StartHost();
                InGameConsole.ShowPassive("[DEBUG] Started as Host");
            }
            if (ModRuntime.Input.IsKeyDown(MystiaKey.F2))
            {
                GameSession.Connect("127.0.0.1");
                InGameConsole.ShowPassive("[DEBUG] Connecting to Self");
            }

            if (ModRuntime.Input.IsKeyDown(MystiaKey.F3))
            {
                QteSync.TriggerInfiniteFeverLocally(NightScene.CookingUtility.QTERewardManager.Instance?.CurrentBuffReward?.TryCast<MystiaQTEBuffReward>());
                InGameConsole.ShowPassive("触发永续热火朝天");
            }
        }
    }

    /// <summary>
    /// 绘制状态条，由 <see cref="ModLoop"/>.OnGui 调用。
    /// </summary>
    public static void DrawStatusOverlay(IIMGUIDrawer drawer)
    {
        if (!IsStatusVisible) return;

        var info = new System.Text.StringBuilder();
        info.AppendLine(Label);
        info.AppendLine(MetaMystia.UI.MultiplayerStatus.BriefStatus);
        drawer.Label(new Rect(10, drawer.ScreenSize.Y - 50, 600, 50), info.ToString());
    }

    private static void ToggleStatusVisibility()
    {
        IsStatusVisible = !IsStatusVisible;
        Log.LogMessage($"Toggled text visibility: " + IsStatusVisible);
        FloatingTextHelper.SetLabelsVisible(IsStatusVisible && GameSession.IsOnline);
    }
}
