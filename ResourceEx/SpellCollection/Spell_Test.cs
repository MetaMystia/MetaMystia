using System;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

using GameData.Core.Collections.NightSceneUtility;
using GameData.Core.Collections.NightSceneUtility.SkillCollection;
using NightScene.EventUtility;

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
    private IEnumerator PositiveBuffRoutine(SpellExecutionContext spellExecutionContext)
    {
        var star = UnityEngine.Object.Instantiate(ResourceEx.AssetBundles.Test.TestObj);
        star.transform.position = Vector3.zero;

        for (var t = 0f; t < 8f; t += Time.deltaTime)
        {
            star.transform.Rotate(0f, 0f, 90f * Time.deltaTime);
            star.transform.position = new Vector3(Mathf.Sin(t) * 0.5f, Mathf.Cos(t) * 0.5f, 0f);
            yield return null;
        }

        UnityEngine.Object.Destroy(star);
    }

    [HideFromIl2Cpp]
    private IEnumerator NegativeBuffRoutine(SpellExecutionContext spellExecutionContext)
    {
        Log.Warning("NegativeBuffRoutine + 0s (START)");
        yield return new WaitForSeconds(1f);
        Log.Warning("NegativeBuffRoutine + 1s");
        yield return new WaitForSeconds(1f);
        Log.Warning("NegativeBuffRoutine + 2s");
        yield return new WaitForSeconds(1f);
        Log.Warning("NegativeBuffRoutine + 3s");
        yield return new WaitForSeconds(1f);
        Log.Warning("NegativeBuffRoutine + 4s");
        yield return new WaitForSeconds(1f);
        Log.Warning("NegativeBuffRoutine + 5s (END)");
    }
}
