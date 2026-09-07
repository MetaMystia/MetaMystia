using System.Collections.Generic;

using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Attributes;

using GameData.Core.Collections.NightSceneUtility;

namespace MetaMystia.ResourceEx.SpellCollection;

public abstract class SpellBaseEx : SpellBase
{
    public abstract int SpellId { get; }
    public abstract string SpellOwnerIdentifier { get; }

    public abstract string PositiveName { get; }
    public abstract string PositiveDesc { get; }
    public abstract string PositivePortraitUri { get; }

    public abstract string NegativeName { get; }
    public abstract string NegativeDesc { get; }
    public abstract string NegativePortraitUri { get; }

    [HideFromIl2Cpp]
    protected virtual IReadOnlyCollection<string> ExtraResourceUris => [];

    [HideFromIl2Cpp]
    public virtual IReadOnlyCollection<string> ResourceUris =>
    [
        PositivePortraitUri,
        NegativePortraitUri,
        ..ExtraResourceUris
    ];

    public override string OnGettingSpellOwnerIdentifier() => SpellOwnerIdentifier;

    public override Il2CppSystem.Collections.IEnumerator OnPositiveBuffExecute(SpellExecutionContext spellExecutionContext)
        => PositiveBuffRoutine(spellExecutionContext).WrapToIl2Cpp();

    public override Il2CppSystem.Collections.IEnumerator OnNegativeBuffExecute(SpellExecutionContext spellExecutionContext)
        => NegativeBuffRoutine(spellExecutionContext).WrapToIl2Cpp();

    [HideFromIl2Cpp]
    protected abstract System.Collections.IEnumerator PositiveBuffRoutine(SpellExecutionContext spellExecutionContext);

    [HideFromIl2Cpp]
    protected abstract System.Collections.IEnumerator NegativeBuffRoutine(SpellExecutionContext spellExecutionContext);
}
