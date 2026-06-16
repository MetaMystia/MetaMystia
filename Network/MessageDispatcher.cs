using System;
using System.Collections.Generic;
using MetaMystia.Network.Handlers;
using MetaMystia.Network.Utilities;
using MetaMystia.Protocol.Messages;

namespace MetaMystia.Network;

[AutoLog]
public static partial class MessageDispatcher
{
    private static Dictionary<Type, Action<NetworkMessage>> _handlers = new();

    /// <summary>
    /// 注册特定消息类型的处理器
    /// </summary>
    /// <typeparam name="T">消息类型，必须继承 NetworkMessage</typeparam>
    /// <param name="handler">处理该消息的委托，参数类型为 T</param>
    public static void Register<T>(Action<T> handler) where T : NetworkMessage
    {
        // 将强类型委托包装为 Action<NetworkMessage>，内部进行类型转换
        _handlers[typeof(T)] = msg => handler((T)msg);
    }

    /// <summary>
    /// 分发消息到已注册的处理器
    /// </summary>
    /// <param name="msg">要处理的消息实例</param>
    /// <returns>是否找到并执行了处理器</returns>
    public static bool Dispatch(NetworkMessage msg)
    {
        var type = msg.GetType();
        LogUtils.LogMessageReceived(msg);
        if (_handlers.TryGetValue(type, out var handler))
        {
            handler(msg);
            return true;
        }
        Log.Error($"No handler registered for message type: {type.FullName}");
        return false;
    }

    /// <summary>
    /// 清除所有注册（通常用于热重载或测试）
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public static void Clear() => _handlers.Clear();

    // 批量注册所有处理器
    public static void RegisterAll()
    {
        SessionHandlers.Register();
        CommonHandlers.Register();
        PrepSceneHandlers.Register();
        DaySceneHandlers.Register();
        WorkSceneHandlers.Register();
        Log.Debug("MessageDispatcher: registered all message handlers");
    }
}
