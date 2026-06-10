using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

using NightScene.CookingUtility;

using MetaMystia.ResourceEx.SpellCollection;
using SgrYuki.Utils;

namespace MetaMystia.Patch;

// ============================================================================
// 共享辅助类 — 为 KoakumaCookingPatch_OnPanelOpen.Prefix 提供随机厨具选择
// ============================================================================

/// <summary>
/// 小恶魔黑卡厨具随机化 — 共享逻辑。
/// </summary>
[AutoLog]
public static partial class KoakumaCookwareChaosPatchHelper
{
    private static readonly System.Random _rng = new();
    private static int _diagCounter;

    public static int DiagCounter => _diagCounter;
    public static void IncrementDiag() => _diagCounter++;

    /// <summary>
    /// 从所有厨具中找一个随机的空闲厨具（排除当前厨具和空桌子）。
    /// 使用 AllCookers 字典的 foreach（Il2Cpp Dictionary 支持此迭代方式）。
    /// </summary>
    public static CookController FindRandomIdleController(CookController exclude)
    {
        var csm = CookSystemManager.Instance;
        if (csm == null)
        {
            UnityEngine.Debug.LogWarning("[Koakuma] FindRandom: CookSystemManager.Instance is null");
            Log.LogWarning("[Koakuma] FindRandom: CookSystemManager.Instance is null");
            return null;
        }

        var allCookersDict = csm.AllCookers;
        if (allCookersDict == null)
        {
            UnityEngine.Debug.LogWarning("[Koakuma] FindRandom: AllCookers dictionary is null");
            Log.LogWarning("[Koakuma] FindRandom: AllCookers dictionary is null");
            return null;
        }

        // 收集候选者：排除当前厨具 + 排除空桌 + 优先选空闲
        var candidates = new List<CookController>();
        var idleCandidates = new List<CookController>();

        foreach (var kvp in allCookersDict)
        {
            var c = kvp.Value;
            if (c == null) continue;
            if (c.Pointer == exclude.Pointer) continue; // 排除当前厨具
            if (c.IsEmptyDesk) continue;                // 排除空桌子

            candidates.Add(c);
            if (c.Phase == CookController.CookPhase.Idle)
                idleCandidates.Add(c);
        }

        if (candidates.Count == 0)
        {
            UnityEngine.Debug.LogWarning("[Koakuma] FindRandom: no candidates after filtering");
            Log.LogWarning("[Koakuma] FindRandom: no candidates after filtering");
            return null;
        }

        // 优先选空闲厨具，没有则从所有候选者中随机选
        var pool = idleCandidates.Count > 0 ? idleCandidates : candidates;
        var picked = pool[_rng.Next(pool.Count)];

        Log.LogInfo($"[Koakuma] FindRandom: {candidates.Count} candidates, {idleCandidates.Count} idle, "
            + $"picked GridIndex={picked.GridIndex}");
        return picked;
    }
}
