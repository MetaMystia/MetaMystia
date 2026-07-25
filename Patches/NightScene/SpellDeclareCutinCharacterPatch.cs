using HarmonyLib;

using MetaMystia.ResourceEx.SpellCollection;

using UnityEngine;

#nullable enable

namespace MetaMystia.Patch;

/// <summary>
/// 符卡宣言立绘的 Y 偏移落地 Patch：在游戏原生 OnEnable 把每个立绘图层 anchoredPosition 复位为 (0,0) 之后，
/// 消费立绘偏移 pending flag，把对应角色的立绘整体下移
/// </summary>
[AutoLog]
public partial class SpellDeclareCutinCharacterPatch
{
    /// <summary>
    /// Postfix 于 SpellDeclareCutinCharacter.OnEnable：将待消费的立绘偏移值施加到立绘整体根节点 transform 上。
    /// </summary>
    /// <param name="__instance">符卡宣言立绘控制类实例（IL2CPP 对象，可能已销毁为伪 null）。</param>
    /// <remarks>此方法由 SpellDeclareCutinCharacter.OnEnable 回调保证运行于 Unity 主线程。</remarks>
    [HarmonyPatch(typeof(SpellDeclareCutinCharacter))]
    [HarmonyPatch(nameof(SpellDeclareCutinCharacter.OnEnable))]
    [HarmonyPostfix]
    public static void OnEnable_Postfix(SpellDeclareCutinCharacter? __instance)
    {
        if (__instance == null) return;

        bool consumed = SpellHelper.TryGetCutinShift(out _, out float offsetY);
        if (!consumed) return;

        Transform? root = __instance.transform;
        if (root == null) return;
        root.localPosition += Vector3.up * offsetY;
    }
}
