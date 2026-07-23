using HarmonyLib;

using MetaMystia.ResourceEx.SpellCollection;

using UnityEngine;
using UnityEngine.UI;

#nullable enable

namespace MetaMystia.Patch;

/// <summary>
/// 符卡宣言立绘的 Y 偏移落地 Patch：在游戏原生 OnEnable 把每个立绘图层 anchoredPosition 复位为 (0,0) 之后，
/// 消费 U1 的 single-slot 偏移 flag，把对应角色（大妖精/小红魔）的立绘整体下移，避免与宣言 UI 重叠。
/// </summary>
[AutoLog]
public partial class SpellDeclareCutinCharacterPatch
{
    /// <summary>
    /// Postfix 于 SpellDeclareCutinCharacter.OnEnable：将偏移 flag 的偏移值应用到立绘的全部图层。
    /// </summary>
    /// <param name="__instance">符卡宣言立绘控制类实例（IL2CPP 对象，可能已销毁为伪 null）。</param>
    /// <remarks>此方法由 SpellDeclareCutinCharacter.OnEnable 回调保证运行于 Unity 主线程。</remarks>
    [HarmonyPatch(typeof(SpellDeclareCutinCharacter))]
    [HarmonyPatch(nameof(SpellDeclareCutinCharacter.OnEnable))]
    [HarmonyPostfix]
    public static void OnEnable_Postfix(SpellDeclareCutinCharacter? __instance)
    {
        if (__instance == null) return;

        if (!SpellHelper.TryGetCutinShift(out _, out float offsetY)) return;

        Image[]? images = __instance.m_ImagesPivotFixed;
        if (images == null) return;
        foreach (Image? image in images)
        {
            RectTransform? rectTransform = image?.rectTransform;
            if (rectTransform == null) continue;
            rectTransform.anchoredPosition += Vector2.up * offsetY;
        }
    }
}
