using System;

using HarmonyLib;

using UnityEngine;

using MetaMystia.ResourceEx.SpellCollection;

namespace MetaMystia.Patch;

/// <summary>
/// 神绮 / 大妖精 / 小恶魔 符卡宣言立绘下移。
/// 自修正方案：记录当前实际偏移量，每次 OnEnable 计算 delta = target - current，
/// 自动偏移或回正。支持不同角色不同偏移量，以及复用对象的 transform 残留清除。
/// </summary>
[HarmonyPatch(typeof(SpellDeclareCutinCharacter), "OnEnable")]
internal class SpellDeclareCutinCharacterPatch
{
    /// <summary>当前立绘的实际 Y 偏移量（0 = 未偏移）。</summary>
    private static float _currentShiftY;

    [HarmonyPostfix]
    private static void OnEnable_Postfix(SpellDeclareCutinCharacter __instance)
    {
        try
        {
            // 读取 flag：目标偏移量（非目标角色 = 0）
            bool shouldShift = SpellHelper.TryGetCutinShift(out _, out var targetY);
            float curY = _currentShiftY;


            float delta = targetY - curY;

            if (Math.Abs(delta) > 0.01f)
            {
                __instance.transform.localPosition += new Vector3(0f, delta, 0f);
                _currentShiftY = targetY;
            }
        }
        catch
        {
            // 任何异常静默吞掉
        }
    }
}
