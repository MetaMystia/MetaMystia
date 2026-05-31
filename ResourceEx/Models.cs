using System.Collections.Generic;
using Common.DialogUtility;
using GameData.Profile;
using static GameData.Core.Collections.DaySceneUtility.Collections.Product;
using static GameData.Core.Collections.Sellable;
using static GameData.Profile.SchedulerNode;
using static GameData.Profile.SchedulerNode.Trigger;
using GameData.Profile.SchedulerNodeCollection;
using static GameData.Profile.SchedulerNodeCollection.MissionNode.FinishCondition;

namespace MetaMystia.ResourceEx.Models;

public class CharacterConfig
{
    public int id { get; set; }
    public string name { get; set; }
    public string label { get; set; }
    public List<string> descriptions { get; set; }
    public string type { get; set; }
    public List<PortraitConfig> portraits { get; set; }
    public int? faceInNoteBook { get; set; }
    public GuestConfig guest { get; set; }
    public CharacterSpriteSetCompactConfig characterSpriteSetCompact { get; set; }
    public KizunaEventConfig kizuna { get; set; }
    public SpawnMarkerConfig spawnMarker { get; set; }
    public bool hideInAlbum { get; set; }
    public bool isParticular { get; set; }
    public bool isCollabCharacter { get; set; }
}

public class SpawnMarkerConfig
{
    public string mapLabel { get; set; } = "BeastForest";
    public float x { get; set; } = 0f;
    public float y { get; set; } = 0f;
    public DayScene.Input.DayScenePlayerInputGenerator.CharacterRotation rotation { get; set; } = DayScene.Input.DayScenePlayerInputGenerator.CharacterRotation.Down;
}


public class KizunaEventConfig
{
    public string lv1UpgradePrerequisiteEvent { get; set; }
    public string lv2UpgradePrerequisiteEvent { get; set; }
    public string lv3UpgradePrerequisiteEvent { get; set; }
    public string lv4UpgradePrerequisiteEvent { get; set; }

    public List<string> lv1Welcome { get; set; }
    public List<string> lv2Welcome { get; set; }
    public List<string> lv3Welcome { get; set; }
    public List<string> lv4Welcome { get; set; }
    public List<string> lv5Welcome { get; set; }

    public List<string> lv1ChatData { get; set; }
    public List<string> lv2ChatData { get; set; }
    public List<string> lv3ChatData { get; set; }
    public List<string> lv4ChatData { get; set; }
    public List<string> lv5ChatData { get; set; }

    public List<string> lv2InviteSucceed { get; set; }
    public List<string> lv2InviteFailed { get; set; }
    public List<string> lv3InviteSucceed { get; set; }
    public List<string> lv3InviteFailed { get; set; }
    public List<string> lv4InviteSucceed { get; set; }
    public List<string> lv4InviteFailed { get; set; }
    public List<string> lv5InviteSucceed { get; set; }

    public List<string> lv3RequestIngerdient { get; set; } // ignore typo
    public List<string> lv4RequestIngerdient { get; set; } // ignore typo
    public List<string> lv5RequestIngerdient { get; set; } // ignore typo
    public List<string> lv4RequestBeverage { get; set; }
    public List<string> lv5RequestBeverage { get; set; }
    public List<string> lv5Commision { get; set; }
    public List<string> lv5CommisionFinish { get; set; }
    public string commisionAreaLabel { get; set; } // ignore typo
}

public class GuestConfig
{
    public int fundRangeLower { get; set; }
    public int fundRangeUpper { get; set; }
    public List<string> evaluation { get; set; }
    public List<string> conversation { get; set; }
    public List<RequestConfig> foodRequests { get; set; }
    public List<RequestConfig> bevRequests { get; set; }
    public List<int> hateFoodTag { get; set; }
    public List<WeightedTagConfig> likeFoodTag { get; set; }
    public List<WeightedTagConfig> likeBevTag { get; set; }
    public List<SpawnConfig> spawn { get; set; }
}

public class SpawnConfig
{
    public int izakayaId { get; set; }
    public float relativeProb { get; set; }
    public bool onlySpawnAfterUnlocking { get; set; }
    public bool onlySpawnWhenPlaceBeRecorded { get; set; }
}

public class RequestConfig
{
    public int tagId { get; set; }
    public string request { get; set; }
    public bool enable { get; set; } = true;
}

public class WeightedTagConfig
{
    public int tagId { get; set; }
    public int weight { get; set; }
}

public class CharacterSpriteSetCompactConfig
{
    public string name { get; set; }
    public List<string> mainSprite { get; set; }
    public List<string> eyeSprite { get; set; }
}

public class ClothConfig
{
    public int id { get; set; }
    public string name { get; set; }
    public string description { get; set; }
    public string spritePath { get; set; }
    public string portraitPath { get; set; }
    public CharacterSpriteSetFullConfig pixelFullConfig { get; set; }
    public int izakayaSkinIndex { get; set; } = -1;
    public float izkayaHorizontalOffset { get; set; } = 0f;
    public float notebookHorizontalOffset { get; set; } = 0f;
    public float notebookVerticalOffset { get; set; } = 0f;
    public float notebookUITitleHorizontalOffset { get; set; } = 0f;
    public float notebookUITitleVerticalOffset { get; set; } = 0f;
}

public class CharacterSpriteSetFullConfig
{
    public string name { get; set; }
    public List<string> mainSprite { get; set; }
    public List<string> eyeSprite { get; set; }
    public List<string> hairSprite { get; set; }
    public List<string> backSprite { get; set; }
}

public class PortraitConfig
{
    public int pid { get; set; }
    public string path { get; set; }
}

public class PackInfoConfig
{
    public string name { get; set; }
    public string label { get; set; }
    public List<string> authors { get; set; }
    public string description { get; set; }
    public string version { get; set; }
    public string license { get; set; }
    public int? idRangeStart { get; set; }
    public int? idRangeEnd { get; set; }
    public string idSignature { get; set; }
}

public class ResourceConfig
{
    public PackInfoConfig packInfo { get; set; }
    public List<CharacterConfig> characters { get; set; }
    public List<DialogPackageConfig> dialogPackages { get; set; }
    public List<IngredientConfig> ingredients { get; set; }
    public List<RecipeConfig> recipes { get; set; }
    public List<FoodConfig> foods { get; set; }
    public List<BeverageConfig> beverages { get; set; }
    public List<ClothConfig> clothes { get; set; }
    public List<MissionNodeConfig> missionNodes { get; set; }
    public List<EventNodeConfig> eventNodes { get; set; }
    public List<MerchantConfig> merchants { get; set; }
}

public class DialogConfig
{
    public int characterId { get; set; }
    public SpeakerIdentity.Identity characterType { get; set; }
    public int pid { get; set; }
    public Position position { get; set; }
    public DialogActionConfig[] actions { get; set; }
    public string text { get; set; }
}

public class DialogPackageConfig
{
    public string name { get; set; }
    public List<DialogConfig> dialogList { get; set; }

    public int Count => dialogList?.Count ?? 0;

    public DialogConfig this[int index] => dialogList[index];

    public System.Action<Il2CppSystem.Collections.Generic.Dictionary<int, string>> GetOverrideReplaceTextCallback()
    {
        return replaceDict =>
        {
            for (int i = 0; i < Count; i++)
                replaceDict[i] = this[i].text;
        };
    }
}

public class DialogActionConfig
{
    public ActionType actionType { get; set; }

    /// <summary>
    /// For CG/BG actions: relative path to sprite image (e.g. "assets/CG/painting.png").
    /// Prefer a full rex URI in ResourceEx JSON config.
    /// </summary>
    public string sprite { get; set; }

    /// <summary>
    /// For Sound actions: relative path or rex URI to a WAV asset.
    /// </summary>
    public string sound { get; set; }

    public bool shouldSet { get; set; } = true;
}

public class IngredientConfig
{
    public int id { get; set; }
    public string name { get; set; }
    public string description { get; set; }
    public int level { get; set; }
    public int prefix { get; set; }
    public bool isFish { get; set; }
    public bool isMeat { get; set; }
    public bool isVeg { get; set; }
    public int baseValue { get; set; }
    public List<int> tags { get; set; }

    public string spritePath { get; set; }
}

public class BeverageConfig
{
    public int id { get; set; }
    public string name { get; set; }
    public string description { get; set; }
    public int level { get; set; }
    public int baseValue { get; set; }
    public List<int> tags { get; set; }

    public string spritePath { get; set; }
}

public class RecipeConfig
{
    public int id { get; set; }
    public int foodId { get; set; }
    public GameData.Core.Collections.Cooker.CookerType cookerType { get; set; }
    public float cookTime { get; set; }
    public List<int> ingredients { get; set; }
}

public class FoodConfig
{
    public int id { get; set; }
    public string name { get; set; }
    public string description { get; set; }
    public int level { get; set; }
    public int baseValue { get; set; }
    public List<int> tags { get; set; }
    public List<int> banTags { get; set; }
    public string spritePath { get; set; }
}

public class MissionNodeConfig
{
    public string title { get; set; }
    public string description { get; set; }
    public string label { get; set; }
    public string debugLabel { get; set; }
    public SchedulerNode.SchedulerType missionType { get; set; }
    public string sender { get; set; }
    public string reciever { get; set; } // ignore typo
    public List<MissionRewardConfig> rewards { get; set; }
    public List<MissionRewardConfig> postRewards { get; set; }
    public List<MissionFinishConditionConfig> finishConditions { get; set; }
    public EventDataConfig missionFinishEvent { get; set; }
    public EventDataConfig missionFailedEvent { get; set; }
    public List<string> postMissionsAfterPerformance { get; set; }
    public List<string> postEvents { get; set; }
    public bool isTimedMission { get; set; } = false;
    public MissionNode.MissionFailedAction missionFailedAction { get; set; } = MissionNode.MissionFailedAction.None;
    public TriggerConfig missionTimeLimit { get; set; }
}

public class MissionRewardConfig
{
    public Reward.RewardType rewardType { get; set; }
    public string rewardId { get; set; }
    public Reward.ObjectType? objectType { get; set; }
    public List<int> rewardIntArray { get; set; }
}

public class MissionFinishConditionConfig
{
    public ConditionType conditionType { get; set; }
    public int? amount { get; set; }
    public int? tag { get; set; }
    public int[] tags { get; set; }
    public SellableType? sellableType { get; set; }
    public string label { get; set; }
    public ProductType? productType { get; set; }
    public int? productId { get; set; }
    public int? productAmount { get; set; }
}

public class EventNodeConfig
{
    public string label { get; set; }
    public string debugLabel { get; set; }
    public ScheduledEventConfig scheduledEvent { get; set; }
    public List<MissionRewardConfig> rewards { get; set; }
    public List<MissionRewardConfig> postRewards { get; set; }
    public List<string> postMissionsAfterPerformance { get; set; }
    public List<string> postEvents { get; set; }
}

public class DayConfig
{
    public Day.DayType dayType { get; set; }
    public Day.CalculateType dayCalcType { get; set; }
    public int day { get; set; }
    public int dayRangeMin { get; set; }
    public int dayRangeMax { get; set; }
}

public class TriggerConfig
{
    public TriggerType triggerType { get; set; }
    public string triggerId { get; set; }
    public DayConfig time { get; set; }
}

public class ScheduledEventConfig
{
    public EventDataConfig eventData { get; set; }
    public TriggerConfig trigger { get; set; }
}

public class EventDataConfig
{
    public Event.EventType eventType { get; set; }
    public string dialogPackageName { get; set; } // -> SchedulerNode.Event.runtimeDialogPackage: DialogPackage
}

public class MerchantConfig
{
    public string key { get; set; }
    public List<string> welcomeDialogPackageNames { get; set; }
    public List<string> nullDialogPackageNames { get; set; }
    public float priceMultiplierMin { get; set; }
    public float priceMultiplierMax { get; set; }
    public int leastSellNum { get; set; }
    public List<MerchandiseConfig> merchandise { get; set; }
}

public class MerchandiseConfig
{
    public ProductConfig item { get; set; }
    public int itemAmountMin { get; set; }
    public int itemAmountMax { get; set; }
    public float sellProbability { get; set; }
}

public class ProductConfig
{
    public ProductType productType { get; set; }
    public int productId { get; set; }
    public int productAmount { get; set; }
    public string productLabel { get; set; }
}
