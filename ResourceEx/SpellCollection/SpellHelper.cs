#nullable enable

using System;
using System.Collections.Generic;
using GameData.CoreLanguage;
using GameData.CoreLanguage.Collections;
using NightScene.EventUtility;
using SgrYuki;
using UnityEngine;

namespace MetaMystia.ResourceEx.SpellCollection;

/// <summary>
/// 符卡系统跨类共享的静态工具，集中管理立绘偏移等需要跨符卡共享的状态
/// </summary>
internal static class SpellHelper
{
    private const string DaiyouseiOwnerIdentifier = "_ResourceExample_Daiyousei";
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

    /// <summary>
    /// 向游戏 Buff 描述字典注入一条自定义 Buff 的显示名、描述与图标，供右下角 Buff 栏显示。
    /// </summary>
    /// <param name="buffType">目标 Buff 类型，由调用方在各符卡 US 内定义并传入</param>
    /// <param name="title">显示名称，非空。须由调用方传入 L10n 解析后的文案</param>
    /// <param name="description">显示描述，非空。须由调用方传入 L10n 解析后的文案</param>
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
}
