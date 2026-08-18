using System;
using System.Collections;

using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

using Common.UI;

using MetaMystia.UI;
using SgrYuki;

namespace MetaMystia;

/// <summary>
/// 模组主要的 MonoBehaviour 宿主：驱动帧循环（Update / FixedUpdate / OnGUI）与托管协程。
/// 业务逻辑均在静态 Manager 中，宿主仅负责转发。由 Plugin.BootstrapPatch 在首个 Canvas 出现时注入。
/// </summary>
[AutoLog]
public partial class PluginHost : MonoBehaviour
{
    public static PluginHost Instance { get; private set; }

    public PluginHost(IntPtr ptr) : base(ptr)
    {
        if (Instance != null)
        {
            Log.LogWarning($"Another instance of PluginHost already exists! Destroying this one.");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    internal static GameObject Create(string name)
    {
        var gameObject = new GameObject(name);
        DontDestroyOnLoad(gameObject);

        gameObject.AddComponent(Il2CppType.Of<PluginHost>());

        return gameObject;
    }

    private void Awake()
    {
        InGameConsole.Initialize();
        ResourceExManager.FlushPendingConsoleLogs();
    }

    [HideFromIl2Cpp]
    public Coroutine StartManagedCoroutine(IEnumerator routine) => MonoBehaviourExtensions.StartCoroutine(this, routine);

    private void Update()
    {
        PluginManager.TickMainThreadQueue();
        Network.MpWire.FlushInbox();
        MpManager.RefreshInStoryCache();
        GuestsMap.TickAllPending();

        InGameConsole.Update();
        PlayerListPanel.Update();

        PluginManager.HandleShortcuts();
    }

    private void FixedUpdate()
    {
        CommandScheduler.Tick();

        switch (MpManager.LocalScene)
        {
            case Scene.DayScene:
            case Scene.WorkScene:
                PlayerManager.OnFixedUpdate();
                break;
        }
    }

    private void OnGUI()
    {
        InGameConsole.OnGUI();
        PlayerListPanel.OnGUI();
        PluginManager.DrawStatusOverlay();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
