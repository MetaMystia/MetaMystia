using System;

using HarmonyLib;

using GameData.CoreLanguage.Collections;
using MetaMystia.ResourceEx.SpellCollection;
using NightScene.EventUtility;
using NightScene.UI;

using SgrYuki.Utils;

namespace MetaMystia.Patch;

/// <summary>
/// RegisterTimedBuffRecord / RegisterCountedBuffRecord 不传 Description 给 RegisterBuff。
/// Prefix 在方法执行前将 buffLang.Description 存入 static 字段，
/// 供 BuffElementDescriptionPatch.InitializeVisual_Postfix 在 InitializeVisual 后读取。
/// </summary>
[HarmonyPatch(typeof(UIManager), "RegisterTimedBuffRecord")]
[HarmonyPatch(typeof(UIManager), "RegisterCountedBuffRecord")]
[AutoLog]
public partial class BuffInitialDescriptionPatch
{
    public static string PendingDescription;
    /// <summary>
    /// 从 SpellHelper.TimedBuffDurations 查到的总持续秒数，用于倒计时显示。
    /// 仅对自定义定时 buff 有值。
    /// </summary>
    public static int? PendingDuration;

    [HarmonyPrefix]
    public static void RegisterBuffRecord_Prefix(EventManager.BuffType buffType)
    {
        var buffLang = DataBaseLanguage.RefBuffLang(buffType);
        PendingDescription = buffLang?.Description ?? " ";

        if (SpellHelper.TimedBuffDurations.TryGetValue((int)buffType, out var dur))
            PendingDuration = dur;
        else
            PendingDuration = null;
    }
}
