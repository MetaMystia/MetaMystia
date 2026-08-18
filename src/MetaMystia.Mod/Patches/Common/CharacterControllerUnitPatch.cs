using HarmonyLib;

using Common.CharacterUtility;


namespace MetaMystia.Patch;

[HarmonyPatch(typeof(Common.CharacterUtility.CharacterControllerUnit))]
[AutoLog]
public partial class CharacterControllerUnitPatch
{
    /// <summary>
    /// 已知的远程玩家角色名称列表（用于识别对端角色）
    /// </summary>
    public static bool IsPeerCharacter(string label)
    {
        return PlayerManager.IsPeerCharacter(label);
    }

    [HarmonyPatch(nameof(CharacterControllerUnit.Initialize))]
    [HarmonyPrefix]
    public static void Initialize_Prefix(CharacterControllerUnit __instance, ref bool shouldTurnOnCollider)
    {
        if (IsPeerCharacter(__instance.name))
        {
            shouldTurnOnCollider = true;
            Log.LogMessage($"found {__instance.name}, forcing shouldTurnOnCollider to true");
        }
    }

    [HarmonyPatch(nameof(CharacterControllerUnit.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix(CharacterControllerUnit __instance)
    {
        if (IsPeerCharacter(__instance.name))
        {
            PlayerManager.EnablePeerCollision(__instance, true);
            Log.LogMessage($"found {__instance.name}, enabling collision");
        }
    }
}
