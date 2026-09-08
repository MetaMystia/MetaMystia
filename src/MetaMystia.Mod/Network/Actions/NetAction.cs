using System;
using System.Reflection;

using BepInEx.Logging;
using MemoryPack;

using MetaMystia.Network.Core;

namespace MetaMystia.Network;

// 游戏载荷和处理函数留在 Mod。类型、编号、路由只在 GameMessages 登记一次。
[AutoLog]
public abstract partial class Action
{
    [MemoryPackIgnore] public int SenderUid { get; internal set; }
    [MemoryPackIgnore] public int? WireTargetUid { get; set; }
    [MemoryPackIgnore] internal RoomBinding Binding { get; set; }
    [MemoryPackIgnore] internal long PhaseId { get; set; }
    [MemoryPackIgnore] protected virtual LogLevel OnReceiveLogLevel => LogLevel.Info;
    [MemoryPackIgnore] protected virtual LogLevel OnSendLogLevel => LogLevel.Info;

    public abstract void OnReceivedDerived();
    public void OnReceived()
    {
        Log._inner.Log(OnReceiveLogLevel, $"[Network] Receive {GetType().Name} from {SenderUid}; phase={PhaseId}");
        OnReceivedDerived();
    }

    protected void Enqueue()
    {
        if (!GameMessages.CanSend(this)) return;
        Log._inner.Log(OnSendLogLevel, $"[Network] Send {GetType().Name}; phase={RoomGameplay.PhaseId}");
        GameMessages.Send(this);
    }

    internal Common.UI.Scene? ReceiveScene => GetType().GetMethod(nameof(OnReceivedDerived))?.GetCustomAttribute<CheckSceneAttribute>()?.Scene;
    internal bool WaitForStory => GetType().GetMethod(nameof(OnReceivedDerived))?.IsDefined(typeof(WaitUntilStoryEndsAttribute), true) == true;

    [AttributeUsage(AttributeTargets.Method)]
    protected sealed class CheckSceneAttribute(Common.UI.Scene scene) : Attribute { public Common.UI.Scene Scene { get; } = scene; }
    [AttributeUsage(AttributeTargets.Method)] protected sealed class WaitUntilStoryEndsAttribute : Attribute;
}
