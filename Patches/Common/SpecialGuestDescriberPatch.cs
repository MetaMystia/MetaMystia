using HarmonyLib;
using Il2CppSystem;
using Il2CppSystem.Threading;

using Common.UI;
using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage.Collections;
using MetaMystia.ResourceEx.SpellCollection;

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

        // 大妖精红卡召唤消息
        Log.Info($"[Daiyousei] Describe_Postfix: detail.Id={detail.Id}, LastSummoned={Spell_Daiyousei.LastSummonedGuestId}");
        if (Spell_Daiyousei.LastSummonedGuestId == detail.Id && Spell_Daiyousei.LastSummonedGuestId != -1)
        {
            var guestName = detail.Id.GetSpecialGuestLang().BriefName;
            string message = detail.Id == 4
                ? "慧音老师来惩罚不听话的翘课的孩子们了"
                : $"大妖精邀请{guestName}来吃饭了";

            // 尝试多个文本字段
            __instance.placeText.text = message;
            __instance.partnerProperties.text = message;
            Log.Info($"[Daiyousei] 设置描述栏消息: {message} (placeText + partnerProperties)");
            Spell_Daiyousei.LastSummonedGuestId = -1;
        }
    }
}
