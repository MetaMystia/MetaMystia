using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Common.CharacterUtility;
using GameData.Core.Collections;
using GameData.Core.Collections.CharacterUtility;
using GameData.RunTime.Common;

using MetaMystia.Network;
using SgrYuki.Utils;

namespace MetaMystia;

// 本地输入与皮肤驱动；网络身份由 ClientSession 提供。
public sealed class LocalPlayer : NetPlayer
{
    public override int Uid => MpWire.Session.SelfUid;
    public override PlayerSkin Skin { get; } = new();
    private ResourceDataBase _resources = ResourceDataBase.Empty;
    public override ResourceDataBase DataBase => _resources;
    public override MapLabel MapLabel => CurrentMapLabel;
    public bool Sprinting { get; set; }
    public Vector2 Direction { get; set; }
    public override bool IsSprinting => Sprinting;
    public override Vector2 InputDirection => Direction;
    public override float Speed => unit?.MoveSpeedMultiplier ?? 1f;
    public bool CharacterSpawnedAndInitialized => unit != null && rb2d != null && cl2d != null;
    public bool IsCustomSkinOverride { get; set; }

    public override CharacterControllerUnit GetCharacterUnit()
    {
        var director = Common.SceneDirector.instance;
        return director != null && director.characterCollection.TryGetValue("Self", out var character) ? character : null;
    }

    public static MapLabel CurrentMapLabel
    {
        get
        {
            MapLabelExtensions.TryFromMapKey(Common.SceneDirector.Instance?.currentActiveScene?.Key, out var label);
            return label;
        }
    }

    public void ReloadResourceTable()
    {
        _resources = ResourceDataBase.FromLocal(new IEnumerable<int>[]
        {
            DataBaseCore.Foods.ToList().Select(p => p.Key),
            DataBaseCore.Recipes.ToList().Select(p => p.Key),
            DataBaseCore.Beverages.ToList().Select(p => p.Key),
            DataBaseCore.Ingredients.ToList().Select(p => p.Key),
            DataBaseCore.Cookers.ToList().Select(p => p.Key),
            DataBaseCore.Items.ToList().Select(p => p.Key),
            DataBaseCore.Izakayas.ToList().Select(p => p.Key),
            DataBaseCharacter.SpecialGuest.ToList().Select(p => p.Key),
            DataBaseCharacter.NormalGuest.ToList().Select(p => p.Key)
        });
        ResourceSnapshotAction.Send();
    }
    public void InitSkin()
    {
        if (IsCustomSkinOverride) return;
        var clothes = RunTimeAlbum.GetPlayerClothes();
        Skin.SetSkin(-1, clothes.skinIndex.selectedType, clothes.skinIndex.index);
    }
}
