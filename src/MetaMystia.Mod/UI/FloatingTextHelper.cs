using System;
using System.Collections.Generic;

using Mystia.Assets;
using Mystia.Scenes;

namespace MetaMystia.UI;

/// <summary>
/// 运行中场景会话的展示服务（<c>IPresentationServices</c>）：标签与角色精灵集都作用在「当前场景」上，
/// 只在场景循环的 <c>Setup</c>/<c>Update</c>/<c>Shutdown</c> 窗口内可用。
/// <para>
/// 场景循环进入时 <see cref="Begin"/> 写入服务、离开时 <see cref="End"/> 清空；模组在窗口外（网络消息、
/// 游戏回调）发起的动作经 <see cref="Enqueue"/> 排队，由场景循环逐帧调用 <see cref="Pump"/> 在窗口内执行。
/// 没有场景会话时排队的动作会在 <see cref="End"/> 一并丢弃。
/// </para>
/// </summary>
internal static class ScenePresentation
{
    private static readonly Queue<Action<IPresentationServices>> Pending = new();
    private static IPresentationServices s_services;

    internal static void Begin(IPresentationServices services) => s_services = services;

    /// <summary>登记一个必须在场景循环服务窗口内执行的展示动作。</summary>
    internal static void Enqueue(Action<IPresentationServices> work)
    {
        if (work is not null) Pending.Enqueue(work);
    }

    /// <summary>由场景循环的 <c>Update</c> 调用：在服务窗口内执行排队中的动作。</summary>
    internal static void Pump()
    {
        if (s_services is not { } services) return;
        if (Pending.Count == 0) return;

        // 先取走本帧的待办：动作里再排进来的留到下一帧，避免同一帧自排自执行。
        var batch = Pending.ToArray();
        Pending.Clear();
        foreach (var work in batch)
        {
            try { work(services); }
            catch (Exception e) { ModRuntime.Log.Warning($"[ScenePresentation] 展示动作失败：{e.Message}"); }
        }
    }

    /// <summary>场景循环退出时调用：服务窗口关闭，未执行的待办一并丢弃。</summary>
    internal static void End()
    {
        s_services = null;
        Pending.Clear();
    }
}

/// <summary>
/// 世界内的浮字：临时浮字（聊天气泡）与玩家头顶名牌。
/// 字体、描边、颜色与淡出都由框架的 <c>IPresentationServices</c> 承担（原实现里的 TextMeshPro／
/// GameObject／TMP_FontAsset 用法已全部移除），模组只保管 <c>IFloatingLabel</c> 句柄。
/// </summary>
[AutoLog]
public static partial class FloatingTextHelper
{
    private static IFloatingLabel activeTextPeer;
    private static IFloatingLabel activeTextSelf;

    public static void ShowFloatingTextOnMainThread(object host, string Message) =>
        PluginManager.RunOnMainThread(() => ShowFloatingText(host, Message));

    public static void ShowFloatingTextSelfOnMainThread(string Message) =>
        PluginManager.RunOnMainThread(() => ShowFloatingTextSelf(Message));

    /// <summary>在宿主上方弹一条临时浮字（框架的 SpawnLabel：到点自行淡出并销毁）。</summary>
    private static void ShowFloatingText(object host, string text, float duration = 5f)
    {
        if (host is null) return;

        ScenePresentation.Enqueue(services =>
        {
            activeTextPeer?.Stop();
            activeTextPeer = null;
            if (services.Bind(host) is not { } target) return;
            activeTextPeer = services.SpawnLabel(target, text, FloatingLabelStyle.Default, duration);
        });
    }

    /// <summary>在本地玩家头顶弹一条临时浮字。</summary>
    private static void ShowFloatingTextSelf(string text, float duration = 5f)
    {
        ScenePresentation.Enqueue(services =>
        {
            activeTextSelf?.Stop();
            activeTextSelf = null;
            if (PlayerManager.Local.GetCharacterUnit() is not { } character) return;
            if (services.Bind(character) is not { } target) return;
            activeTextSelf = services.SpawnLabel(target, text, FloatingLabelStyle.Default, duration);
        });
    }
}
