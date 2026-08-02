using HarmonyLib;

using Common.DialogUtility;
using GameData.RunTime.Common;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(Common.DialogUtility.DialogPannel))]
[AutoLog]
public partial class DialogPannelPatch
{
    [HarmonyPatch(nameof(DialogPannel.GetSpeakerVisual))]
    [HarmonyPrefix]
    public static bool GetSpeakerVisual_Prefix(DialogMeta meta, ref UnityEngine.Sprite visual)
    {
        var id = meta.speakerIdentity.speakerId;
        var type = meta.speakerIdentity.speakerType;
        var pid = meta.speakerIdentity.speakerPortrayalVariationId;

        if (type != SpeakerIdentity.Identity.Special ||
            !ResourceExManager.TryGetSpecialGuestCustomPortrayal(id.RefSpecialPortrayal(), out var customPortrayal))
            return RunOriginal;

        if (pid >= 0 && pid < customPortrayal.Length)
            visual = customPortrayal[pid];

        return SkipOriginal;
    }
}
