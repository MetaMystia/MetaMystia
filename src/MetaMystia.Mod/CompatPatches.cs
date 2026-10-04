using System;

using HarmonyLib;

namespace MetaMystia;

/// <summary>
/// 旧 Harmony 补丁的应用入口。Compat 尚未清空，因此本节仍集中应用 <c>Patches/Compat</c> 下的缺口补丁；
/// 其余行为一律走框架的监听与服务。
/// <para>
/// 当前在册缺口：<c>YuyukoChallengeContextPatch</c>/<c>YuyukoRetakeContextPatch</c>（改判回调的台词与闭包倍率）、
/// <c>YuyukoBossDataPatch</c>（失败整段重放）、<c>YuyukoTimedNegativeSpellPatch</c>（限时负面符卡协程）、
/// <c>YuyukoExtraDialogData__c__DisplayClass4_0Patch</c>（挑战确认回调）、
/// <c>NightSceneDirectorPatch</c>（试炼返回标记与本体捕获）。每一份都写明了框架侧缺的能力。
/// </para>
/// </summary>
[AutoLog]
public static partial class CompatPatches
{
    private static bool s_applied;

    /// <summary>应用失败时记录原因；<see cref="Applied"/> 为假时联机功能会主动拒绝进入。</summary>
    public static Exception Failed { get; set; }

    public static bool Applied => Failed is null;

    public static void ApplyAll()
    {
        if (s_applied)
            return;
        s_applied = true;

        var harmony = new Harmony("MetaMystia.Compat");
        try
        {
            harmony.PatchAll(typeof(CompatPatches).Assembly);
            Log.Info("Compat patches applied.");
        }
        catch (Exception ex)
        {
            Failed = ex;
            Log.Error($"Compat patches failed: {ex.Message}");
        }
    }
}
