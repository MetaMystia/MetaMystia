using System;
using System.Collections.Generic;
using UnityEngine;

using GameData.CoreLanguage;
using GameData.CoreLanguage.Collections;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

using SgrYuki.Utils;

namespace MetaMystia.ResourceEx.SpellCollection;

internal static class SpellHelper
{
    private static SgrYuki.LogWrapper Log => new(MetaMystia.Plugin.Instance.Log, nameof(SpellHelper));

    /// <summary>
    /// 自定义定时 buff 的总持续秒数，key 为 BuffType int 值。
    /// 注册 buff 前填入，供 BuffElementDescriptionPatch 读取以显示倒计时。
    /// </summary>
    public static readonly Dictionary<int, int> TimedBuffDurations = new();

    // ================================================================================
    // 符卡宣言立绘偏移（flag + 帧数过期，杜绝泄漏）
    // ================================================================================

    private static string _pendingCutinOwnerId;
    private static int _pendingCutinFrame;

    private static readonly Dictionary<string, float> CutinShift = new()
    {
        ["_ResourceExample_Daiyousei"] = -300f,
        ["_ResourceExample_Koakuma"] = -300f,
    };

    /// <summary>由 SpellBase.ShouldCallSpellDeclarationAuto 调用。</summary>
    internal static void SetCutinShift(string ownerIdentifier)
    {
        _pendingCutinOwnerId = ownerIdentifier;
        _pendingCutinFrame = Time.frameCount;
    }

    /// <summary>
    /// 由 SpellDeclareCutinCharacterPatch.OnEnable Postfix 调用。
    /// 读取并清除 flag，同时检查帧数是否过期（>5帧=游戏加载阶段的残留，忽略）。
    /// </summary>
    internal static bool TryGetCutinShift(out string ownerIdentifier, out float offsetY)
    {
        ownerIdentifier = _pendingCutinOwnerId;
        _pendingCutinOwnerId = null;
        offsetY = 0f;

        if (ownerIdentifier == null) return false;

        // 帧数过期检查：flag 设置超过 5 帧未消费 = 游戏加载阶段残留，丢弃
        if (Time.frameCount - _pendingCutinFrame > 5) return false;

        // 查静态表
        if (CutinShift.TryGetValue(ownerIdentifier, out offsetY))
            return true;

        // 查神绮动态 label
        if (Spell_Shinki.TryGetCutinOffset(ownerIdentifier, out offsetY))
            return true;

        return false;
    }

    /// <summary>
    /// 向 BuffDescription 字典注入自定义文本。
    /// 游戏原生字典的 key 是 EventManager.BuffType 枚举，需要反射写入。
    /// </summary>
    internal static void RegisterBuffDescription(EventManager.BuffType buffType, string title, string description, Sprite visual = null)
    {
        try
        {
            var dict = DataBaseLanguage.BuffDescription;
            if (dict == null) return;
            var lang = new ObjectLanguageBase(name: title, Description: description, visual: visual);
            var indexer = dict.GetType().GetProperty("Item");
            if (indexer != null)
            {
                var keyParamType = indexer.GetIndexParameters()[0].ParameterType;
                object key = Enum.ToObject(keyParamType, buffType);
                indexer.SetValue(dict, lang, new object[] { key });
            }
        }
        catch (Exception ex)
        {
            Log.LogError($"[SpellHelper] RegisterBuffDescription failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取场上所有活跃的稀客角色 ID。
    /// </summary>
    internal static HashSet<int> GetOnFieldSpecialGuestIds()
    {
        var result = new HashSet<int>();
        var allGuests = GuestsMap.GetAllGuestsSnapshot();
        if (allGuests == null) return result;

        foreach (var (_, fsm) in allGuests)
        {
            if (fsm?.Ids == null || fsm.Controller == null) continue;
            if (fsm.GuestType != GuestsManager.GuestType.Special) continue;

            var state = fsm.CurrentState;
            if (state == GuestFSM.State.Left || state == GuestFSM.State.Dead || state == GuestFSM.State.None)
                continue;
            if (!fsm.Controller.HaveNotLeft())
                continue;

            foreach (var id in fsm.Ids)
                result.Add(id);
        }
        return result;
    }
}
