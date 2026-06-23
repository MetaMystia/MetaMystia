using System;
using System.Collections.Generic;

namespace MetaMystia.Network;

internal enum NetReceiveScope
{
    Any,
    ClientOnly,
    HostOnly,
}

internal sealed class NetActionDispatcher
{
    private readonly Dictionary<Type, BehaviorEntry> _behaviors = new();

    public void Register<TAction>(
        System.Action<TAction> behavior,
        Common.UI.Scene? scene = null,
        bool discardOnStory = false,
        bool requireHostSender = false,
        NetReceiveScope receiveScope = NetReceiveScope.Any)
        where TAction : NetAction
    {
        _behaviors[typeof(TAction)] = new BehaviorEntry(
            action => behavior((TAction)action),
            scene,
            discardOnStory,
            requireHostSender,
            receiveScope);
    }

    public bool Dispatch(NetAction action)
    {
        if (action == null) return false;
        if (!_behaviors.TryGetValue(action.GetType(), out var behavior))
        {
            Plugin.Instance?.Log.LogError(
                $"No behavior registered for {action.GetType().Name}; action dropped.");
            return false;
        }
        action.OnReceivedByHandler(
            behavior.Scene,
            behavior.DiscardOnStory,
            behavior.RequireHostSender,
            behavior.ReceiveScope,
            () => behavior.Handle(action));
        return true;
    }

    public bool ShouldDiscardOnStory(NetAction action)
    {
        if (action == null) return false;
        return _behaviors.TryGetValue(action.GetType(), out var behavior) && behavior.DiscardOnStory;
    }

    private readonly record struct BehaviorEntry(
        System.Action<NetAction> Handle,
        Common.UI.Scene? Scene,
        bool DiscardOnStory,
        bool RequireHostSender,
        NetReceiveScope ReceiveScope);
}
