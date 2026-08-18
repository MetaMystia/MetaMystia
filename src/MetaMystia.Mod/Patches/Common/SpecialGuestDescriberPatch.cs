using HarmonyLib;
using Il2CppSystem;
using Il2CppSystem.Threading;

using Common.UI;
using GameData.Core.Collections.NightSceneUtility;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(Common.UI.SpecialGuestDescriber))]
[AutoLog]
public partial class SpecialGuestDescriberPatch
{
    [HarmonyPatch(nameof(SpecialGuestDescriber.Describe))]
    [HarmonyPrefix]
    public static void Describe_Prefix(SpecialGuest detail, ref CancellationToken cancellationToken)
    {
        var src = new CancellationTokenSource();
        cancellationToken = src.Token;
        src.Cancel();
        Log.Info($"cancelled token for special guest ID {detail.Id}");
    }

    [HarmonyPatch(nameof(SpecialGuestDescriber.Describe))]
    [HarmonyPostfix]
    public static void Describe_Postfix(SpecialGuestDescriber __instance, SpecialGuest detail, CancellationToken cancellationToken)
    {
        var portrayal = detail.CharacterDefaultPortrayal;
        if (ResourceExManager.TryGetSpecialGuestCustomPortrayal(portrayal, out var portrayalSprites, out var faceInNoteBook))
        {
            portrayal.faceInNoteBook = faceInNoteBook;
            if (portrayal.faceInNoteBook >= 0 && portrayal.faceInNoteBook < portrayalSprites.Length)
            {
                __instance.portrayal.sprite = portrayalSprites[portrayal.faceInNoteBook];
                __instance.portrayal.enabled = true;
                Log.Info($"Updated portrayal sprite for custom special guest ID {detail.Id}");
            }
            else
            {
                Log.Warning($"Custom portrayal index {portrayal.faceInNoteBook} out of range for special guest ID {detail.Id}");
            }
        }
        else
        {
            __instance.portrayal.AssignImageSpriteAsync(portrayal.LoadNotebookVisual(UniversalGameManager.PlatformAssetLifetime, new Nullable<CancellationToken>(CancellationToken.None)), CancellationToken.None);
            Log.Info($"default portrayal sprite for special guest ID {detail.Id}");
        }
    }
}
