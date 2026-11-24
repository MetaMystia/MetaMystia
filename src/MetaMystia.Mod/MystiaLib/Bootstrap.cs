using BepInEx.Logging;
using HarmonyLib;

using MetaMystia.MystiaLib.Keybinding;

namespace MetaMystia.MystiaLib;

/// <summary>
/// MystiaLib 入口。现阶段随 MetaMystia 打包，由其插件 <c>Load</c> 调用；拆分为独立插件后由自身 <c>Load</c> 调用。
/// </summary>
public static class Bootstrap
{
    /// <summary>写入磁盘与存档的标识前缀，与命名空间无关，不得修改。</summary>
    public const string Id = "MystiaLib";

    internal static readonly ManualLogSource Log = Logger.CreateLogSource(Id);

    private static readonly System.Type[] Patches = [
        typeof(AdpPlayerInputWrapperPatch),
        typeof(EscBindingPannelPatch),
        typeof(SaveManagementPatch),
    ];

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        var harmony = new Harmony(Id);
        foreach (var patch in Patches)
            harmony.PatchAll(patch);
        Log.LogInfo($"Initialized, {Patches.Length} patches");
    }
}
