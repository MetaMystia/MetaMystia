using Il2CppInterop.Runtime.Attributes;

using Common.CharacterUtility;
using GameData.Profile;

using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.DecorationCollection;

/// <summary>夜间沿用原版 DecorationBuffEnterNight；白天回调仅作用于当前玩家角色。</summary>
public abstract class DecorationBaseEx : DecorationBase
{
    [HideFromIl2Cpp] public DecorationConfig Config { get; internal set; }

    [HideFromIl2Cpp]
    public virtual void OnDayEquip(CharacterControllerUnit player) { }

    [HideFromIl2Cpp]
    public virtual void OnDayUnequip(CharacterControllerUnit player) { }
}
