using System;

namespace MetaMystia.Network.Handlers;

public static class HandlerAttributes
{
    /// <summary>
    /// 指定该 Handler 方法只在特定场景下执行。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class CheckSceneAttribute(Common.UI.Scene scene) : Attribute
    {
        public Common.UI.Scene Scene { get; } = scene;
    }

    /// <summary>
    /// 标记该 Handler 方法在剧情播放时不应执行。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class DiscardOnStoryAttribute : Attribute { }
}
