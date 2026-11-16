#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MetaMystia.UI;

/// <summary>
/// 控制台打开期间独占游戏输入。
/// 游戏的玩家、UI 层按键都走 Input System 的 action 回调（AdpPlayerInputWrapper），
/// 这里把所有已启用的 action 关掉并记录，关闭控制台后等 Enter/Esc 松开再恢复，
/// 避免松键时触发游戏的 Submit/Interact。不改 UniversalGameManager.IsInputEnabled，
/// 因此不会覆盖剧情、Boss 战等游戏自身的输入状态，也不会隐藏 HUD。
/// </summary>
public static class ConsoleInputGate
{
    private static readonly List<InputAction> _disabled = [];
    private static Il2CppSystem.Collections.Generic.List<InputAction>? _buffer;

    public static bool Engaged { get; private set; }
    public static bool ReleasePending { get; private set; }

    /// <summary>控制台正在占用键盘（打开中，或刚关闭、按键还未松开）。</summary>
    public static bool Busy => Engaged || ReleasePending;

    public static void Engage()
    {
        Engaged = true;
        ReleasePending = false;
        Sweep();
    }

    public static void Release()
    {
        if (!Engaged) return;
        Engaged = false;
        ReleasePending = true;
        Tick();
    }

    /// <summary>每帧调用：打开期间持续关掉游戏新启用的 action；关闭后在按键松开时恢复。</summary>
    public static void Tick()
    {
        if (Engaged)
            Sweep();
        else if (ReleasePending && !Input.GetKey(KeyCode.Return) && !Input.GetKey(KeyCode.KeypadEnter)
                 && !Input.GetKey(KeyCode.Escape))
            Restore();
    }

    /// <summary>立即恢复，用于插件销毁等场合。</summary>
    public static void ForceRestore()
    {
        Engaged = false;
        Restore();
    }

    private static void Sweep()
    {
        _buffer ??= new Il2CppSystem.Collections.Generic.List<InputAction>();
        InputSystem.ListEnabledActions(_buffer);
        for (int i = 0; i < _buffer.Count; i++)
        {
            var action = _buffer[i];
            action.Disable();
            _disabled.Add(action);
        }
        _buffer.Clear();
    }

    private static void Restore()
    {
        ReleasePending = false;
        foreach (var action in _disabled)
            if (!action.enabled) action.Enable();
        _disabled.Clear();
    }
}
