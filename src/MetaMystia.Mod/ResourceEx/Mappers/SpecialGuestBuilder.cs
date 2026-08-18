using System.Linq;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using GameData.Core.Collections.NightSceneUtility;
using GameData.Core.Collections.DaySceneUtility.Collections;
using GameData.Profile;

using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Mappers;

/// <summary>
/// Builder for creating SpecialGuest instances from template (DataBaseCharacter.SpecialGuest[0])
/// </summary>
internal class SpecialGuestBuilder
{
    private readonly SpecialGuest _template;

    private int _id;
    private string _stringId;
    private Vector2Int _fundRange;
    private int[] _hateFoodTag;
    private Il2CppReferenceArray<SpecialGuest.WeightedTag> _likeFoodTag;
    private Il2CppReferenceArray<SpecialGuest.WeightedTag> _likeBevTag;
    private string _commisionAreaLabel;
    // private NPC.Destination _destination;

    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel1Welcome;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel2Welcome;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel3Welcome;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel4Welcome;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel5Welcome;

    private Il2CppStringArray _characterKizunaLevel1ChatData;
    private Il2CppStringArray _characterKizunaLevel2ChatData;
    private Il2CppStringArray _characterKizunaLevel3ChatData;
    private Il2CppStringArray _characterKizunaLevel4ChatData;
    private Il2CppStringArray _characterKizunaLevel5ChatData;

    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel2InviteSucceed;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel2InviteFailed;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel3InviteSucceed;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel3InviteFailed;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel4InviteSucceed;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel4InviteFailed;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel5InviteSucceed;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel3RequestIngerdient;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel4RequestIngerdient;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel5RequestIngerdient;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel4RequestBeverage;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel5RequestBeverage;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel5Commision;
    private Il2CppReferenceArray<DialogPackage> _characterKizunaLevel5CommisionFinish;
    public bool? _hideInAlbum;
    public bool? _isParticular;
    public bool? _isCollabCharacter;

    private SpecialGuestBuilder(SpecialGuest template)
    {
        _template = template ?? throw new System.ArgumentNullException(nameof(template));
    }

    public static SpecialGuestBuilder FromTemplate(SpecialGuest template) => new SpecialGuestBuilder(template);

    public SpecialGuestBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public SpecialGuestBuilder WithStringId(string stringId)
    {
        _stringId = stringId;
        return this;
    }

    public SpecialGuestBuilder WithFundRange(int lower, int upper)
    {
        _fundRange = new Vector2Int(lower, upper);
        return this;
    }

    public SpecialGuestBuilder WithHateFoodTags(System.Collections.Generic.List<int> tags)
    {
        _hateFoodTag = tags?.ToArray() ?? [];
        return this;
    }

    public SpecialGuestBuilder WithLikeFoodTags(System.Collections.Generic.List<WeightedTagConfig> tags)
    {
        _likeFoodTag = new Il2CppReferenceArray<SpecialGuest.WeightedTag>(
            tags?.Select(x => new SpecialGuest.WeightedTag(x.tagId, x.weight)).ToArray() ?? []);
        return this;
    }

    public SpecialGuestBuilder WithLikeBevTags(System.Collections.Generic.List<WeightedTagConfig> tags)
    {
        _likeBevTag = new Il2CppReferenceArray<SpecialGuest.WeightedTag>(
            tags?.Select(x => new SpecialGuest.WeightedTag(x.tagId, x.weight)).ToArray() ?? []);
        return this;
    }

    public SpecialGuestBuilder WithCommisionAreaLabel(string label)
    {
        _commisionAreaLabel = label;
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel1Welcome(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel1Welcome = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel2Welcome(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel2Welcome = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel3Welcome(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel3Welcome = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel4Welcome(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel4Welcome = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel5Welcome(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel5Welcome = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel1ChatData(System.Collections.Generic.List<string> packages)
    {
        _characterKizunaLevel1ChatData = packages != null
            ? new Il2CppStringArray(packages.ToArray())
            : new Il2CppStringArray(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel2ChatData(System.Collections.Generic.List<string> packages)
    {
        _characterKizunaLevel2ChatData = packages != null
            ? new Il2CppStringArray(packages.ToArray())
            : new Il2CppStringArray(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel3ChatData(System.Collections.Generic.List<string> packages)
    {
        _characterKizunaLevel3ChatData = packages != null
            ? new Il2CppStringArray(packages.ToArray())
            : new Il2CppStringArray(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel4ChatData(System.Collections.Generic.List<string> packages)
    {
        _characterKizunaLevel4ChatData = packages != null
            ? new Il2CppStringArray(packages.ToArray())
            : new Il2CppStringArray(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel5ChatData(System.Collections.Generic.List<string> packages)
    {
        _characterKizunaLevel5ChatData = packages != null
            ? new Il2CppStringArray(packages.ToArray())
            : new Il2CppStringArray(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel2InviteSucceed(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel2InviteSucceed = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel2InviteFailed(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel2InviteFailed = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel3InviteSucceed(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel3InviteSucceed = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel3InviteFailed(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel3InviteFailed = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel4InviteSucceed(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel4InviteSucceed = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel4InviteFailed(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel4InviteFailed = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel5InviteSucceed(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel5InviteSucceed = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel3RequestIngerdient(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel3RequestIngerdient = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel4RequestIngerdient(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel4RequestIngerdient = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel5RequestIngerdient(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel5RequestIngerdient = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel4RequestBeverage(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel4RequestBeverage = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel5RequestBeverage(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel5RequestBeverage = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel5Commision(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel5Commision = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }

    public SpecialGuestBuilder WithCharacterKizunaLevel5CommisionFinish(System.Collections.Generic.List<DialogPackage> packages)
    {
        _characterKizunaLevel5CommisionFinish = packages != null
            ? new Il2CppReferenceArray<DialogPackage>(packages.ToArray())
            : new Il2CppReferenceArray<DialogPackage>(0);
        return this;
    }
    public SpecialGuestBuilder WithHideInAlbum(bool? hideInAlbum)
    {
        _hideInAlbum = hideInAlbum;
        return this;
    }

    public SpecialGuestBuilder WithIsParticular(bool? isParticular)
    {
        _isParticular = isParticular;
        return this;
    }
    public SpecialGuestBuilder WithIsCollabCharacter(bool? isCollabCharacter)
    {
        _isCollabCharacter = isCollabCharacter;
        return this;
    }

    // public SpecialGuestBuilder WithDestination(string spawnMarkerName)
    // {
    //     _destination = new NPC.Destination()
    //     {
    //         spawnMarker = spawnMarkerName,
    //         initialDialogPackIDs = new Il2CppStringArray(0),
    //         interactiveAreaSize = _template.destination.interactiveAreaSize,
    //         stayTime = _template.destination.stayTime
    //     };
    //     return this;
    // }

    public SpecialGuest Build()
    {
        var specialGuest = new SpecialGuest(
            _id,
            _stringId,  // 字符串 id，label
            _fundRange, // 资金范围
            _hateFoodTag,   // 讨厌的食物标签
            _likeFoodTag,   // 喜欢的食物标签权重
            _likeBevTag,    // 喜欢的饮品标签权重
            _template.Reaction, // ？
            _template.destination,  // 白天生成位置
            _commisionAreaLabel ?? _template.CommisionAreaLabel,   // 委托采集对应地图标签
            _characterKizunaLevel1Welcome ?? _template.characterKizunaLevel1Welcome,
            _characterKizunaLevel2Welcome ?? _template.characterKizunaLevel2Welcome,
            _characterKizunaLevel3Welcome ?? _template.characterKizunaLevel3Welcome,
            _characterKizunaLevel4Welcome ?? _template.characterKizunaLevel4Welcome,
            _characterKizunaLevel5Welcome ?? _template.characterKizunaLevel5Welcome,
            _characterKizunaLevel1ChatData ?? _template.characterKizunaLevel1ChatData,
            _characterKizunaLevel2ChatData ?? _template.characterKizunaLevel2ChatData,
            _characterKizunaLevel3ChatData ?? _template.characterKizunaLevel3ChatData,
            _characterKizunaLevel4ChatData ?? _template.characterKizunaLevel4ChatData,
            _characterKizunaLevel5ChatData ?? _template.characterKizunaLevel5ChatData,
            _characterKizunaLevel2InviteSucceed ?? _template.characterKizunaLevel2InviteSucceed,
            _characterKizunaLevel2InviteFailed ?? _template.characterKizunaLevel2InviteFailed,
            _characterKizunaLevel3InviteSucceed ?? _template.characterKizunaLevel3InviteSucceed,
            _characterKizunaLevel3InviteFailed ?? _template.characterKizunaLevel3InviteFailed,
            _characterKizunaLevel4InviteSucceed ?? _template.characterKizunaLevel4InviteSucceed,
            _characterKizunaLevel4InviteFailed ?? _template.characterKizunaLevel4InviteFailed,
            _characterKizunaLevel5InviteSucceed ?? _template.characterKizunaLevel5InviteSucceed,
            _characterKizunaLevel3RequestIngerdient ?? _template.characterKizunaLevel3RequestIngerdient,
            _characterKizunaLevel4RequestIngerdient ?? _template.characterKizunaLevel4RequestIngerdient,
            _characterKizunaLevel5RequestIngerdient ?? _template.characterKizunaLevel5RequestIngerdient,
            _characterKizunaLevel4RequestBeverage ?? _template.characterKizunaLevel4RequestBeverage,
            _characterKizunaLevel5RequestBeverage ?? _template.characterKizunaLevel5RequestBeverage,
            _characterKizunaLevel5Commision ?? _template.characterKizunaLevel5Commision,
            _characterKizunaLevel5CommisionFinish ?? _template.characterKizunaLevel5CommisionFinish,
            _hideInAlbum ?? _template.hideInAlbum,
            _isParticular ?? _template.IsParticular,
            _isCollabCharacter ?? _template.IsCollabCharacter,
            _template.SpawnType,
            _template.unifiedSpawnExclusion,
            _template.unifiedSpawnWhereAfterEventOrMission,
            _template.unifiedSpawnProb,
            _template.m_SpecialGuestExtraDialogDataAsset,
            _template.doNotShowInNightByDefault,
            _template.doNotShowInChallenge,
            _template.guestFoodEasterEggData
        );

        specialGuest.stringId = _stringId;
        return specialGuest;
    }
}


/* 字段猜测
GameData.Core.Collections.CharacterUtility.DataBaseCharacter.SpecialGuest

public SpecialGuest(int id,
	string stringId,
	Vector2Int fundRange, // 资金范围
	int[] hateFoodTag, // 讨厌的食物标签
	SpecialGuest.WeightedTag[] likeFoodTag, // 喜欢的食物标签权重
	SpecialGuest.WeightedTag[] likeBevTag,  // 喜欢的饮品标签权重
	SpecialGuest.Prespective reaction, // ？
	NPC.Destination destination,  // ？
	string commisionAreaLabel,  // 委托采集对应地图标签，例如 (0)Wriggle 是 BeastForest，(3)Akyuu 是 HumanVillage，似乎对尚未实现自定义地图的 mod 无用
	DialogPackage[] characterKizunaLevel1Welcome,
	DialogPackage[] characterKizunaLevel2Welcome,
	DialogPackage[] characterKizunaLevel3Welcome,
	DialogPackage[] characterKizunaLevel4Welcome,
	DialogPackage[] characterKizunaLevel5Welcome,
	string[] characterKizunaLevel1ChatData,
	string[] characterKizunaLevel2ChatData,
	string[] characterKizunaLevel3ChatData,
	string[] characterKizunaLevel4ChatData,
	string[] characterKizunaLevel5ChatData,
	DialogPackage[] characterKizunaLevel2InviteSucceed,
	DialogPackage[] characterKizunaLevel2InviteFailed,
	DialogPackage[] characterKizunaLevel3InviteSucceed,
	DialogPackage[] characterKizunaLevel3InviteFailed,
	DialogPackage[] characterKizunaLevel4InviteSucceed,
	DialogPackage[] characterKizunaLevel4InviteFailed,
	DialogPackage[] characterKizunaLevel5InviteSucceed,
	DialogPackage[] characterKizunaLevel3RequestIngerdient,
	DialogPackage[] characterKizunaLevel4RequestIngerdient,
	DialogPackage[] characterKizunaLevel5RequestIngerdient,
	DialogPackage[] characterKizunaLevel4RequestBeverage,
	DialogPackage[] characterKizunaLevel5RequestBeverage,
	DialogPackage[] characterKizunaLevel5Commision,
	DialogPackage[] characterKizunaLevel5CommisionFinish,
	bool hideInAlbum,
	bool isParticular,
	bool isCollabCharacter,
	SpecialGuest.WorkSceneSpawnType spawnType,
	string[] unifiedSpawnExclusion,
	string unifiedSpawnWhereAfterEventOrMission,
	float unifiedSpawnProb,
	AssetReferenceT<SpecialGuestExtraDialogData> specialGuestExtraDialogData,
	bool doNotShowInNightByDefault,
	bool doNotShowInChallenge,
	GuestFoodEasterEggData guestFoodEasterEggData);

*/
