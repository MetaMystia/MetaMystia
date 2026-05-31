using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using Il2CppInterop.Runtime;

using Common.UI;

using MetaMystia.UI;
using SgrYuki;

namespace MetaMystia;

[AutoLog]
public partial class PluginManager : MonoBehaviour
{
    public static PluginManager Instance { get; private set; }
    public static readonly string Label = $"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} loaded";
    public static Debugger.WebDebugger Debugger = null;
    public static bool IsStatusVisible { get; private set; } = true;
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
    private readonly List<(Action action, Func<bool> condition)> _conditionalActions = new List<(Action, Func<bool>)>();
    public static bool DEBUG => ConfigManager.Debug.Value;

    public PluginManager(IntPtr ptr) : base(ptr)
    {
        if (Instance != null)
        {
            Log.LogWarning($"Another instance of PluginManager already exists! Destroying this one.");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    internal static GameObject Create(string name)
    {
        var gameObject = new GameObject(name);
        DontDestroyOnLoad(gameObject);

        gameObject.AddComponent(Il2CppType.Of<PluginManager>());

        return gameObject;
    }

    private void Awake()
    {
        InGameConsole.Initialize();
        ResourceExManager.FlushPendingConsoleLogs();
    }

    private void OnGUI()
    {
        InGameConsole.OnGUI();
        PlayerListPanel.OnGUI();

        if (IsStatusVisible)
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine(Label);
            info.AppendLine(MpManager.BriefStatus);
            GUI.Label(new Rect(10, Screen.height - 50, 600, 50), info.ToString());
        }
    }

    private void Update()
    {
        UpdateRunOnMainThreadQueue();
        MpManager.RefreshInStoryCache();
        GuestsMap.TickAllPending();

        InGameConsole.Update();
        PlayerListPanel.Update();

        if (Input.GetKeyDown(ConfigManager.KeyToggleLog.Value)) // KeyCode.RightShift
        {
            Log.LogInfo($"\n");
        }
        if (Input.GetKeyDown(ConfigManager.KeyToggleStatus.Value)) // KeyCode.Backslash
        {
            IsStatusVisible = !IsStatusVisible;
            Log.LogMessage($"Toggled text visibility: " + IsStatusVisible);
            FloatingTextHelper.SetLabelsVisible(IsStatusVisible && MpManager.IsConnected);
        }

        if (DEBUG)
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                MpManager.Start(MpManager.ROLE.Host);
                InGameConsole.ShowPassive("[DEBUG] Started as Host");
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                _ = MpManager.ConnectToPeerAsync("127.0.0.1");
                InGameConsole.ShowPassive("[DEBUG] Connecting to Self");
            }
            if (Input.GetKeyDown(KeyCode.F3))
            {
                ResourceExManager.SpellTest();
            }

            if (Input.GetKeyDown(KeyCode.F4))
            {
                ResourceEx.AssetBundles.Test.Test1();
            }

            if (Input.GetKeyDown(KeyCode.F11))
            {
                Debugger ??= new Debugger.WebDebugger();
                Debugger?.Start();
            }
        }
    }

    private void UpdateRunOnMainThreadQueue()
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

    public void RunOnMainThread(Action action) => _mainThreadQueue.Enqueue(action);

    private void FixedUpdate()
    {
        CommandScheduler.Tick();

        switch (MpManager.LocalScene)
        {
            case Scene.DayScene:
                PlayerManager.OnFixedUpdate();
                break;
            case Scene.WorkScene:
                PlayerManager.OnFixedUpdate();
                break;
            default:
                break;
        }
    }

    private void OnDestroy()
    {
    }
}
