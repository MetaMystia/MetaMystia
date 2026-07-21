using HarmonyLib;

using DayScene.Interactables.Collections.BehaviourComponents;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(CollabBehaviourComponent))]
[AutoLog]
public partial class CollabBehaviourComponentPatch
{
    internal static PatchBypassToken PendingCollabMenu = new();

    [HarmonyPatch(nameof(CollabBehaviourComponent.OnInteract))]
    [HarmonyPrefix]
    public static void OnInteract_Prefix() => PendingCollabMenu.SetCount(1);
}
