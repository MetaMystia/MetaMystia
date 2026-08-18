using System;
using System.Collections.Concurrent;
using UnityEngine;

using MetaMystia.UI;

namespace MetaMystia;

/// <summary>
/// 模组静态业务入口（与其他 Manager 一致）。帧循环驱动见 <see cref="PluginHost"/>。
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
            return $"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} loaded with {packCount} rex {packLabel}";
        }
    }
    public static Debugger.WebDebugger Debugger;
    public static bool IsStatusVisible { get; private set; } = true;
    private static readonly ConcurrentQueue<Action> _mainThreadQueue = new();
    public static bool DEBUG => ConfigManager.Debug.Value;

    /// <summary>
    /// 跨线程入队到主线程执行（由 <see cref="PluginHost"/> 每帧泵出）。
    /// </summary>
    public static void RunOnMainThread(Action action) => _mainThreadQueue.Enqueue(action);

    /// <summary>
    /// 泵出主线程队列，由 <see cref="PluginHost"/>.Update 每帧调用。
    /// </summary>
    public static void TickMainThreadQueue()
    {
        while (_mainThreadQueue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Log.LogError($"Error executing on main thread: {e.Message}\n{e.StackTrace}");
            }
        }
    }

    /// <summary>
    /// 每帧快捷键处理，由 <see cref="PluginHost"/>.Update 调用。
    /// </summary>
    public static void HandleShortcuts()
    {
        if (Input.GetKeyDown(ConfigManager.KeyToggleLog.Value)) // KeyCode.RightShift
        {
            Log.LogInfo($"\n");
        }
        if (Input.GetKeyDown(ConfigManager.KeyToggleStatus.Value)) // KeyCode.Backslash
        {
            ToggleStatusVisibility();
        }

        if (DEBUG)
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                MpManager.Start(MpManager.ROLE.Server);
                InGameConsole.ShowPassive("[DEBUG] Started as Host");
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                _ = MpManager.ConnectToPeerAsync("127.0.0.1");
                InGameConsole.ShowPassive("[DEBUG] Connecting to Self");
            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
                StoryReplayManager.Test();
            }
            if (Input.GetKeyDown(KeyCode.F11))
            {
                Debugger ??= new Debugger.WebDebugger();
                Debugger?.Start();
            }
        }
    }

    /// <summary>
    /// 绘制状态条，由 <see cref="PluginHost"/>.OnGUI 调用。
    /// </summary>
    public static void DrawStatusOverlay()
    {
        if (!IsStatusVisible) return;

        var info = new System.Text.StringBuilder();
        info.AppendLine(Label);
        info.AppendLine(MpManager.BriefStatus);
        GUI.Label(new Rect(10, Screen.height - 50, 600, 50), info.ToString());
    }

    private static void ToggleStatusVisibility()
    {
        IsStatusVisible = !IsStatusVisible;
        Log.LogMessage($"Toggled text visibility: " + IsStatusVisible);
        FloatingTextHelper.SetLabelsVisible(IsStatusVisible && MpManager.CanSeeOnlinePlayers);
    }
}
