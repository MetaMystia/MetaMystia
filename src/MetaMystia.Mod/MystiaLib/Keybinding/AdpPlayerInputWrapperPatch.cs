using System.Linq;

using HarmonyLib;

using DEYU.AdpUISystem.ComponentCollection;

namespace MetaMystia.MystiaLib.Keybinding;

[HarmonyPatch(typeof(DEYU.AdpUISystem.ComponentCollection.AdpPlayerInputWrapper))]
internal static class AdpPlayerInputWrapperPatch
{
    // 原版在启动和生成按键面板时读取绑定覆盖：读取前加入待加入的映射，供面板按资源查找动作。
    [HarmonyPatch(nameof(AdpPlayerInputWrapper.UpdateBindings))]
    [HarmonyPrefix]
    public static void UpdateBindings_Prefix() => Keybinds.AddPendingMaps();

    // 原方法清空整个资源的覆盖后只读回原版文件，之后补回插件文件中的模组覆盖。原方法抛出时不执行。
    [HarmonyPatch(nameof(AdpPlayerInputWrapper.UpdateBindings))]
    [HarmonyPostfix]
    public static void UpdateBindings_Postfix() => KeybindStore.Apply(Keybinds.AddedMaps.ToList());
}
