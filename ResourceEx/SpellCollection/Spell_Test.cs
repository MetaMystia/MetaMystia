using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

using GameData.Core.Collections.NightSceneUtility;

namespace MetaMystia.ResourceEx.SpellCollection;

[AutoLog]
public partial class Spell_Test : SpellBase
{
    public override string OnGettingSpellOwnerIdentifier()
    {
        return "_ResourceExample_Daiyousei";
    }

    public override Il2CppSystem.Collections.IEnumerator OnPositiveBuffExecute(SpellExecutionContext spellExecutionContext)
        => PositiveBuffRoutine(spellExecutionContext).WrapToIl2Cpp();

    public override Il2CppSystem.Collections.IEnumerator OnNegativeBuffExecute(SpellExecutionContext spellExecutionContext)
        => NegativeBuffRoutine(spellExecutionContext).WrapToIl2Cpp();

    [HideFromIl2Cpp]
    private System.Collections.IEnumerator PositiveBuffRoutine(SpellExecutionContext spellExecutionContext)
    {
        Log.Warning("Spell_Test+: step 0 enter");
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("大妖精红卡: 协程 Start + 0s");
        yield return new WaitForSeconds(1f);
        Log.Warning("Spell_Test+: step 1 (after 1s)");
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("大妖精红卡: 协程 Start + 1s");
        yield return new WaitForSeconds(1f);
        Log.Warning("Spell_Test+: step 2 (after 2s)");
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("大妖精红卡: 协程 Start + 2s");
        yield return new WaitForSeconds(1f);
        Log.Warning("Spell_Test+: step 3 done");
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("大妖精红卡: 协程 Start + 3s -> Done");
    }

    [HideFromIl2Cpp]
    private System.Collections.IEnumerator NegativeBuffRoutine(SpellExecutionContext spellExecutionContext)
    {
        Log.Warning("Spell_Test-: step 0 enter");
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("大妖精黑卡: 协程 Start + 0s");
        yield return new WaitForSeconds(1f);
        Log.Warning("Spell_Test-: step 1 (after 1s)");
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("大妖精黑卡: 协程 Start + 1s");
        yield return new WaitForSeconds(1f);
        Log.Warning("Spell_Test-: step 2 done");
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("大妖精黑卡: 协程 Start + 2s -> Done");
    }
}