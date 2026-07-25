#nullable enable

using System;
using System.Collections.Generic;
using GameData.CoreLanguage;
using GameData.CoreLanguage.Collections;
using Il2CppInterop.Runtime;
using MetaMystia;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;
using SgrYuki;
using UnityEngine;

namespace MetaMystia.ResourceEx.SpellCollection;

/// <summary>
/// 符卡系统跨类共享的静态工具，集中管理立绘偏移等需要跨符卡共享的状态
/// </summary>
internal static class SpellHelper
{
    internal const string DaiyouseiOwnerIdentifier = "_ResourceExample_Daiyousei";
    private const string KoakumaOwnerIdentifier = "_ResourceExample_Koakuma";
    private const float CutinOffsetY = -300f;
    private const int CutinFlagExpireFrames = 5;

    // 单例静态待消费状态：运行时仅保留最后一次 Set 的结果
    // 仅限主线程调用
    private static string? _pendingCutinOwnerId;
    private static int _pendingCutinFrame;

    // 模块日志通道
    private static readonly LogWrapper Log = new(BepInEx.Logging.Logger.CreateLogSource(nameof(SpellHelper)), nameof(SpellHelper));

    /// <summary>
    /// 每次 Mod 初始化时重置符卡立绘偏移的静态待消费状态，避免上一局残留造成幽灵符卡宣言。
    /// </summary>
    internal static void ResetCutinState()
    {
        _pendingCutinOwnerId = null;
        _pendingCutinFrame = 0;
    }

    // 立绘偏移静态表
    private static readonly Dictionary<string, float> CutinShift = new()
    {
        [DaiyouseiOwnerIdentifier] = CutinOffsetY,
        [KoakumaOwnerIdentifier] = CutinOffsetY,
    };

    /// <summary>
    /// 将待消费的立绘偏移标识写入 pending flag，并记录当前帧以便过期检查。
    /// 仅支持单次待消费状态：同一帧内连续调用会覆盖前一次设置。
    /// 若 ownerIdentifier 不在 CutinShift 静态表中，将输出警告并直接返回，不写入 flag。
    /// </summary>
    /// <param name="ownerIdentifier">触发符卡宣言的角色标识。若不在 <see cref="CutinShift"/> 表中，将忽略本次设置并记录警告。</param>
    internal static void SetCutinShift(string ownerIdentifier)
    {
        ArgumentNullException.ThrowIfNull(ownerIdentifier);
        if (!CutinShift.ContainsKey(ownerIdentifier))
        {
            Log.LogWarning($"Invalid owner identifier: {ownerIdentifier}");
            return;
        }
        _pendingCutinOwnerId = ownerIdentifier;
        _pendingCutinFrame = Time.frameCount;
    }

    /// <summary>
    /// 读取并清除 pending flag，检查帧数是否过期（加载阶段残留则忽略），再查静态偏移表。
    /// </summary>
    /// <param name="ownerIdentifier">输出被消费的标识；未取到时（返回 false）为 null。</param>
    /// <param name="offsetY">输出 Y 轴偏移。</param>
    /// <returns>是否成功取到偏移（true 时 out 参数有效）。</returns>
    internal static bool TryGetCutinShift(out string? ownerIdentifier, out float offsetY)
    {
        ownerIdentifier = _pendingCutinOwnerId;
        _pendingCutinOwnerId = null;
        offsetY = 0f;

        if (ownerIdentifier == null) return false;

        // 帧差过期判断
        if ((uint)(Time.frameCount - _pendingCutinFrame) > CutinFlagExpireFrames) return false;

        return CutinShift.TryGetValue(ownerIdentifier, out offsetY);
    }

    // 符卡 Buff 注册封装（适配原生 EventManager 四种形态：持续 / 次数 / 手动 / 常驻）

    /// <summary>
    /// 数值型 Buff 描述模板中的剩余量占位符，模板须先经 RegisterBuffDescription 注入。
    /// </summary>
    internal const string RemainingValuePlaceholder = "$t";

    /// <summary>
    /// il2cpp 委托保活表：注册任意形态 Buff 时转换出的委托须在 Buff 存续期间持有托管引用，Buff 结束/中断时移除对应条目。
    /// </summary>
    private static readonly List<object> BuffDelegateKeepAlive = new();

    /// <summary>
    /// 描述模板为 null 时的一次性告警标记，避免每帧回调重复刷屏；每次 Mod 初始化由 ResetBuffDelegateState 重置。
    /// </summary>
    private static bool BuffDescriptionTemplateNullWarned;

    /// <summary>
    /// 每次 Mod 初始化时清空 Buff 委托保活表，避免上一局残留。
    /// </summary>
    internal static void ResetBuffDelegateState()
    {
        BuffDelegateKeepAlive.Clear();
        BuffDescriptionTemplateNullWarned = false;
    }

    /// <summary>
    /// 描述模板为 null 时一次性告警，返回空串兜底。
    /// </summary>
    /// <returns>空串。</returns>
    private static string WarnNullBuffDescriptionTemplate()
    {
        if (!BuffDescriptionTemplateNullWarned)
        {
            BuffDescriptionTemplateNullWarned = true;
            Log.LogError("[SpellHelper] Buff 描述模板为 null，请确认已通过 RegisterBuffDescription 注入（数值占位符 $t 无法替换）。");
        }
        return string.Empty;
    }

    /// <summary>
    /// 把托管 Action 转换为 il2cpp 委托。
    /// </summary>
    /// <param name="managed">托管回调。</param>
    /// <param name="role">回调角色名，用于异常信息。</param>
    /// <returns>转换后的 il2cpp 委托。</returns>
    private static Il2CppSystem.Action ToIl2CppAction(Action managed, string role)
        => DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(managed)
           ?? throw new InvalidOperationException($"Buff {role} 回调的 il2cpp 委托转换失败。");

    /// <summary>
    /// 构建数值剩余量描述回调（持续/次数通用），每帧把 $t 替换为剩余值。
    /// </summary>
    /// <returns>il2cpp Func&lt;int,string,string&gt; 委托。</returns>
    private static Il2CppSystem.Func<int, string, string> BuildNumericContextOverride()
        => DelegateSupport.ConvertDelegate<Il2CppSystem.Func<int, string, string>>(
            (Func<int, string, string>)((remainingValue, template) =>
                template == null ? WarnNullBuffDescriptionTemplate()
                                 : template.Replace(RemainingValuePlaceholder, remainingValue.ToString())))
           ?? throw new InvalidOperationException("Buff 描述回调（数值型）的 il2cpp 委托转换失败。");

    /// <summary>
    /// 构建进度描述回调（手动形态），每帧把 $t 替换为进度值。
    /// </summary>
    /// <returns>il2cpp Func&lt;float,string,string&gt; 委托。</returns>
    private static Il2CppSystem.Func<float, string, string> BuildProgressContextOverride()
        => DelegateSupport.ConvertDelegate<Il2CppSystem.Func<float, string, string>>(
            (Func<float, string, string>)((progress, template) =>
                template == null ? WarnNullBuffDescriptionTemplate()
                                 : template.Replace(RemainingValuePlaceholder, progress.ToString())))
           ?? throw new InvalidOperationException("Buff 描述回调（进度型）的 il2cpp 委托转换失败。");

    /// <summary>
    /// 把托管 Func&lt;string,string&gt; 转换为 il2cpp 委托（常驻形态描述用，调用方自行处理 $a/$b 占位符）。
    /// </summary>
    /// <param name="managed">托管描述回调。</param>
    /// <returns>转换后的 il2cpp 委托。</returns>
    private static Il2CppSystem.Func<string, string> ToIl2CppFuncStringString(Func<string, string> managed)
        => DelegateSupport.ConvertDelegate<Il2CppSystem.Func<string, string>>(managed)
           ?? throw new InvalidOperationException("Buff 描述回调（常驻型）的 il2cpp 委托转换失败。");

    /// <summary>
    /// 把托管 Func&lt;float,string,string&gt; 转换为 il2cpp 委托（手动形态自定义描述用）。
    /// </summary>
    /// <param name="managed">托管描述回调。</param>
    /// <returns>转换后的 il2cpp 委托。</returns>
    private static Il2CppSystem.Func<float, string, string> ToIl2CppFuncFloatStringString(Func<float, string, string> managed)
        => DelegateSupport.ConvertDelegate<Il2CppSystem.Func<float, string, string>>(managed)
           ?? throw new InvalidOperationException("Buff 描述回调（手动自定义）的 il2cpp 委托转换失败。");

    /// <summary>
    /// 把托管 Func&lt;float,float&gt; 转换为 il2cpp 委托（手动形态进度映射用）。
    /// </summary>
    /// <param name="managed">托管进度映射回调。</param>
    /// <returns>转换后的 il2cpp 委托。</returns>
    private static Il2CppSystem.Func<float, float> ToIl2CppFuncFloatFloat(Func<float, float> managed)
        => DelegateSupport.ConvertDelegate<Il2CppSystem.Func<float, float>>(managed)
           ?? throw new InvalidOperationException("Buff 进度映射回调的 il2cpp 委托转换失败。");

    /// <summary>
    /// 构建结束/退出回调并登记保活，Buff 结束或手动退出时移除保活条目。
    /// </summary>
    /// <param name="keepAliveEntry">本 Buff 的保活条目。</param>
    /// <param name="onBuffEnd">Buff 结束时的托管回调。</param>
    /// <returns>转换后的 il2cpp 结束回调。</returns>
    private static Il2CppSystem.Action BuildEndCallback(List<object> keepAliveEntry, Action? onBuffEnd)
    {
        var endCallback = ToIl2CppAction(() =>
        {
            BuffDelegateKeepAlive.Remove(keepAliveEntry);
            onBuffEnd?.Invoke();
        }, "结束");
        keepAliveEntry.Add(endCallback);
        return endCallback;
    }

    /// <summary>
    /// 封装原生中断/退出回调，触发时先清理保活条目再调用原生回调。
    /// </summary>
    /// <param name="keepAliveEntry">本 Buff 的保活条目。</param>
    /// <param name="nativeInterrupt">原生提供的中断/退出回调。</param>
    /// <returns>转换后的 il2cpp 中断回调。</returns>
    private static Il2CppSystem.Action BuildInterruptCallback(List<object> keepAliveEntry, Il2CppSystem.Action nativeInterrupt)
    {
        var interruptCallback = ToIl2CppAction(() =>
        {
            BuffDelegateKeepAlive.Remove(keepAliveEntry);
            nativeInterrupt?.Invoke();
        }, "中断");
        keepAliveEntry.Add(interruptCallback);
        return interruptCallback;
    }

    /// <summary>
    /// 注册一个持续定时 Buff，按 durationSeconds 自动倒数，计时与 UI 刷新由游戏原生接管，描述每帧将模板中的 $t 替换为剩余秒数。
    /// </summary>
    /// <param name="eventManager">夜晚场景事件管理器实例，非空。</param>
    /// <param name="durationSeconds">Buff 总持续秒数，须为正数。</param>
    /// <param name="buffType">自定义 Buff 类型。</param>
    /// <param name="onInterruptThisBuffCallback">输出：主动中断此 Buff 的回调。</param>
    /// <param name="onBuffEnd">Buff 结束时的托管回调，无则传 null。</param>
    internal static void RegisterTimedBuff(
        EventManager eventManager,
        int durationSeconds,
        EventManager.BuffType buffType,
        out Il2CppSystem.Action onInterruptThisBuffCallback,
        Action? onBuffEnd = null)
    {
        ArgumentNullException.ThrowIfNull(eventManager);
        if (durationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Buff 持续秒数必须为正数。");
        }

        var keepAliveEntry = new List<object>();
        var descriptionCallback = BuildNumericContextOverride();
        keepAliveEntry.Add(descriptionCallback);
        var onBuffEndCallback = BuildEndCallback(keepAliveEntry, onBuffEnd);

        eventManager.RegisterTimedBuff(
            durationSeconds, buffType, out onInterruptThisBuffCallback, onBuffEndCallback, descriptionCallback, null);

        onInterruptThisBuffCallback = BuildInterruptCallback(keepAliveEntry, onInterruptThisBuffCallback);
        BuffDelegateKeepAlive.Add(keepAliveEntry);
    }

    /// <summary>
    /// 注册一个次数 Buff，每触发一次扣除 value 直到耗尽自动结束，UI 刷新由游戏原生接管，描述每帧将模板中的 $t 替换为剩余次数。
    /// </summary>
    /// <param name="eventManager">夜晚场景事件管理器实例，非空。</param>
    /// <param name="value">总次数，须为正数。</param>
    /// <param name="mathOperation">次数变动的运算方式。</param>
    /// <param name="buffType">自定义 Buff 类型。</param>
    /// <param name="onBuffDeduct">每次扣除时调用的托管回调，非空。</param>
    /// <param name="onBuffEnd">Buff 结束时的托管回调，无则传 null。</param>
    /// <param name="allowZero">是否允许次数降至 0 仍不结束，默认 false。</param>
    internal static void RegisterCountedBuff(
        EventManager eventManager,
        float value,
        EventManager.MathOperation mathOperation,
        EventManager.BuffType buffType,
        Action onBuffDeduct,
        Action? onBuffEnd = null,
        bool allowZero = false)
    {
        ArgumentNullException.ThrowIfNull(eventManager);
        ArgumentNullException.ThrowIfNull(onBuffDeduct);
        if (value <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Buff 次数必须为正数。");
        }

        var keepAliveEntry = new List<object>();
        var descriptionCallback = BuildNumericContextOverride();
        keepAliveEntry.Add(descriptionCallback);
        var onBuffEndCallback = BuildEndCallback(keepAliveEntry, onBuffEnd);
        var onBuffDeductCallback = ToIl2CppAction(onBuffDeduct, "次数扣除");
        keepAliveEntry.Add(onBuffDeductCallback);

        eventManager.RegisterCountedBuff(
            buffType, value, mathOperation, onBuffDeductCallback, onBuffEndCallback, descriptionCallback, allowZero, null);
    }

    /// <summary>
    /// 注册一个手动带进度 Buff，进度由调用方经 onUpdatingBuffCallback 自行驱动，不自动到期，描述每帧将模板中的 $t 替换为当前进度。
    /// </summary>
    /// <param name="eventManager">夜晚场景事件管理器实例，非空。</param>
    /// <param name="buffType">自定义 Buff 类型。</param>
    /// <param name="onUpdatingBuffCallback">输出：推送进度的更新回调。</param>
    /// <param name="onBuffExitCallback">输出：主动结束此 Buff 的退出回调。</param>
    /// <param name="onBuffEnd">Buff 结束时的托管回调，无则传 null。</param>
    /// <param name="onGettingCurrentBuffContext">可选的自定义描述回调，不传则用默认 $t 进度描述。</param>
    /// <param name="onGettingCurrentBuffProgress">可选的进度映射回调，不传则由游戏按默认进度显示。</param>
    internal static void RegisterPermanentBuff(
        EventManager eventManager,
        EventManager.BuffType buffType,
        out Il2CppSystem.Action<float> onUpdatingBuffCallback,
        out Il2CppSystem.Action onBuffExitCallback,
        Action? onBuffEnd = null,
        Func<float, string, string>? onGettingCurrentBuffContext = null,
        Func<float, float>? onGettingCurrentBuffProgress = null)
    {
        ArgumentNullException.ThrowIfNull(eventManager);

        var keepAliveEntry = new List<object>();
        var descriptionCallback = onGettingCurrentBuffContext == null
            ? BuildProgressContextOverride()
            : ToIl2CppFuncFloatStringString(onGettingCurrentBuffContext);
        keepAliveEntry.Add(descriptionCallback);
        var onBuffEndCallback = BuildEndCallback(keepAliveEntry, onBuffEnd);

        var progressCallback = onGettingCurrentBuffProgress == null
            ? null
            : ToIl2CppFuncFloatFloat(onGettingCurrentBuffProgress);
        if (progressCallback != null) keepAliveEntry.Add(progressCallback);

        eventManager.RegisterManualControlledBuff(
            buffType, onBuffEndCallback, out onUpdatingBuffCallback, descriptionCallback, progressCallback, out onBuffExitCallback);

        onBuffExitCallback = BuildInterruptCallback(keepAliveEntry, onBuffExitCallback);
        BuffDelegateKeepAlive.Add(keepAliveEntry);
    }

    /// <summary>
    /// 注册一个常驻 Buff，无自动倒数或计数，由调用方经 onInterruptThisBuffCallback 主动中断或 onBuffEnd 结束，描述由 getBuffDescriptionCallback 计算且不随帧变化。
    /// </summary>
    /// <param name="eventManager">夜晚场景事件管理器实例，非空。</param>
    /// <param name="buffType">自定义 Buff 类型。</param>
    /// <param name="getBuffDescriptionCallback">描述回调，将模板转为显示文本，无则传 null。</param>
    /// <param name="onBuffEnd">Buff 结束时的托管回调，无则传 null。</param>
    /// <param name="onInterruptThisBuffCallback">输出：主动中断此 Buff 的回调。</param>
    internal static void RegisterConsistentBuff(
        EventManager eventManager,
        EventManager.BuffType buffType,
        Func<string, string>? getBuffDescriptionCallback,
        Action? onBuffEnd,
        out Il2CppSystem.Action onInterruptThisBuffCallback)
    {
        ArgumentNullException.ThrowIfNull(eventManager);

        var keepAliveEntry = new List<object>();
        Il2CppSystem.Func<string, string>? descriptionCallback = getBuffDescriptionCallback == null
            ? null
            : ToIl2CppFuncStringString(getBuffDescriptionCallback);
        if (descriptionCallback != null) keepAliveEntry.Add(descriptionCallback);
        var onBuffEndCallback = BuildEndCallback(keepAliveEntry, onBuffEnd);

        eventManager.RegisterConsistentBuffInternal(
            buffType, descriptionCallback, onBuffEndCallback, out onInterruptThisBuffCallback, null);

        onInterruptThisBuffCallback = BuildInterruptCallback(keepAliveEntry, onInterruptThisBuffCallback);
        BuffDelegateKeepAlive.Add(keepAliveEntry);
    }

    /// <summary>
    /// 向游戏 Buff 描述字典注入一条自定义 Buff 的显示名、描述与图标，供右下角 Buff 栏显示。
    /// </summary>
    /// <param name="buffType">目标 Buff 类型。</param>
    /// <param name="title">显示名称，非空，须为 L10n 解析后的文案。</param>
    /// <param name="description">显示描述，非空，须为 L10n 解析后的文案。</param>
    /// <param name="visual">显示图标，无则传 null。</param>
    internal static void RegisterBuffDescription(
        EventManager.BuffType buffType, string title, string description, Sprite? visual = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);
        var buffDescription = DataBaseLanguage.BuffDescription;
        if (buffDescription == null)
        {
            Log.LogError("[SpellHelper] BuffDescription 未初始化，无法注入自定义 Buff 描述。");
            return;
        }
        buffDescription[buffType] = new ObjectLanguageBase(name: title, Description: description, visual: visual);
    }

    /// <summary>
    /// 获取当前在场的所有稀客角色 ID 集合，用于符卡拉卡召唤前去重。
    /// </summary>
    /// <returns>在场稀客的 ID 集合，无符合条件者返回空集合。</returns>
    internal static HashSet<int> GetOnFieldSpecialGuestIds()
    {
        var onFieldSpecialIds = new HashSet<int>();
        var allGuests = GuestsMap.GetAllGuestsSnapshot();

        foreach (var (_, fsm) in allGuests)
        {
            if (fsm.GuestType != GuestsManager.GuestType.Special) continue;
            if (fsm.CurrentState is GuestFSM.State.Left or GuestFSM.State.Dead or GuestFSM.State.None) continue;
            if (fsm.Controller?.HaveNotLeft() is not true) continue;

            onFieldSpecialIds.UnionWith(fsm.Ids);
        }

        return onFieldSpecialIds;
    }
}
