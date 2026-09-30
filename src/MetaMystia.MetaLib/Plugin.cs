using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

using MetaMystia.MetaLib.Patches;
using MetaMystia.MetaLib.Storage;

namespace MetaMystia.MetaLib;

[BepInPlugin(Id, "MetaMystia.MetaLib", "0.1.0")]
public sealed class Plugin : BasePlugin
{
    public const string Id = "MetaMystia.MetaLib";
    public static Plugin Instance { get; private set; } = null!;

    public override void Load()
    {
        Instance = this;
        ModSaveData.SetMainThread();
        LabChecks.RegisterCallbacks();
        PatchRegistry.Apply(new Harmony(Id));
        AddComponent<LabHost>();
        Log.LogInfo("MetaLib 已加载，F1 查看存储状态，F2–F9 执行存储检查。");
    }
}
