using System;

using HarmonyLib;

namespace MetaMystia;

/// <summary>
/// 旧 Harmony 补丁的应用入口。迁移后只剩没有中间件对应能力的缺口补丁（位于 <c>Patches/Compat</c>），
/// 它们在这里集中应用；其余行为一律走框架的监听与服务。
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
