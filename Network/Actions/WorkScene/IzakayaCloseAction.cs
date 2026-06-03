using System;
using System.Collections.Generic;
using MemoryPack;

using MetaMystia.Patch;
using MetaMystia.UI;
using NightScene.EventUtility;

namespace MetaMystia.Network;

/// <summary>
/// 主机 → 所有客机：广播打烊
/// 增加了一个注册列表，用于让一些永久持续的符卡在这里清理
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class IzakayaCloseAction : Action
{
    public override ActionType Type => ActionType.IZAKAYA_CLOSE;

    /// <summary>
    /// 打烊清理回调列表。任何需要在打烊时执行清理的符卡/系统，
    /// 调用 RegisterOnIzakayaClose(callback) 注册即可。
    /// </summary>
    private static readonly List<System.Action> _onIzakayaCloseCallbacks = [];

    public static void RegisterOnIzakayaClose(System.Action callback) => _onIzakayaCloseCallbacks.Add(callback);

    private static void RunCleanupCallbacks()
    {
        foreach (var cb in _onIzakayaCloseCallbacks)
        {
            try
            {
                cb();
            }
            catch (Exception e)
            {
                Log.LogError($"IzakayaClose cleanup callback error: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 客机收到主机广播的打烊命令 → 设置允许打烊标志并直接触发打烊流程
    /// </summary>
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            Log.Message($"Received close command from host");
            InGameConsole.ShowPassive(TextId.PeerClosedIzakaya.Get(PlayerManager.GetPeerName(SenderUid)));
            var eventManager = EventManager.Instance;
            if (eventManager == null)
            {
                Log.Warning("EventManager is null when replaying host close.");
                return;
            }

            RunCleanupCallbacks();
            NightSceneEventManagerPatch.HostCloseReplay.Grant();
            NightSceneEventManagerPatch.StopInstantiationLoopAndCloseIzakaya_ReversePatch(eventManager);
            NightSceneEventManagerPatch.HostCloseReplay.Reset();
        });
    }

    /// <summary>
    /// 主机 → 所有客机：广播打烊命令
    /// </summary>
    public static void Broadcast()
    {
        RunCleanupCallbacks();
        new IzakayaCloseAction().SendToHostOrBroadcast();
    }
}
