using System.Linq;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

using GameData.Core.Collections;
using GameData.Core.Collections.DaySceneUtility.Collections;
using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage;
using GameData.Profile;
using GameData.Profile.SchedulerNodeCollection;
using GameData.RunTime.DaySceneUtility.Collection;

using static GameData.Core.Collections.DaySceneUtility.Collections.Product;
using static GameData.Core.Collections.Sellable;
using static GameData.Profile.SchedulerNode;
using static GameData.Profile.SchedulerNode.Trigger;
using static GameData.Profile.SchedulerNodeCollection.MissionNode;
using static GameData.Profile.SchedulerNodeCollection.MissionNode.Reward;

using MetaMystia.ResourceEx.Models;
using SgrYuki.Utils;

namespace MetaMystia.ResourceEx.Mappers;

/// <summary>
/// Mapper layer for converting config DTOs to runtime game objects
/// </summary>
[AutoLog]
public static partial class Mappers
{
    #region Ingredient Mappers

    public static Ingredient ToIngredient(this IngredientConfig config)
    {
        return new Ingredient(
            id: config.id,
            baseValue: config.baseValue,
            level: config.level,
            prefix: config.prefix,
            tags: config.tags.ToArray()
        );
    }

    public static ObjectLanguageBase ToIngredientLanguage(this IngredientConfig config, Sprite sprite)
    {
        return new ObjectLanguageBase(
            name: config.name,
            Description: config.description,
            visual: sprite
        );
    }

    #endregion

    #region Food Mappers

    public static Sellable ToFood(this FoodConfig config)
    {
        return new Sellable(
            id: config.id,
            baseValue: config.baseValue,
            level: config.level,
            tags: config.tags.ToArray(),
            banTags: config.banTags.ToArray(),
            type: SellableType.Food,
            additiveTags: new Il2CppSystem.Collections.Generic.List<int>(),
            isCollab: false
        );
    }

    public static ObjectLanguageBase ToFoodLanguage(this FoodConfig config, Sprite sprite)
    {
        return new ObjectLanguageBase(
            name: config.name,
            Description: config.description,
            visual: sprite
        );
    }

    #endregion

    #region Beverage Mappers

    public static Sellable ToBeverage(this BeverageConfig config)
    {
        return new Sellable(
            id: config.id,
            baseValue: config.baseValue,
            level: config.level,
            tags: config.tags.ToArray(),
            banTags: new int[0],
            type: SellableType.Beverage,
            additiveTags: new Il2CppSystem.Collections.Generic.List<int>(),
            isCollab: false
        );
    }

    public static ObjectLanguageBase ToBeverageLanguage(this BeverageConfig config, Sprite sprite)
    {
        return new ObjectLanguageBase(
            name: config.name,
            Description: config.description,
            visual: sprite
        );
    }

    #endregion

    #region Recipe Mappers

    public static Recipe ToRecipe(this RecipeConfig config)
    {
        return new Recipe(
            id: config.id,
            foodID: config.foodId,
            cookerType: config.cookerType,
            cookTime: config.cookTime,
            ingredients: config.ingredients.ToArray()
        );
    }

    #endregion

    #region MissionNode Mappers

    public static LanguageBase ToMissionLanguage(this MissionNodeConfig config)
    {
        return new LanguageBase(config.title, config.description);
    }

    public static MissionNode ToMissionNode(this MissionNodeConfig config)
    {
        var missionNode = ScriptableObject.CreateInstance<MissionNode>();
        missionNode.name = config.label;
        missionNode.label = config.label;
        missionNode.missionFailedAction = MissionFailedAction.None;
        missionNode.missionType = config.missionType;

        missionNode.rewards = config.rewards?.Select(ToReward).ToArray() ?? new Il2CppReferenceArray<Reward>(0);
        missionNode.postRewards = config.postRewards?.Select(ToReward).ToArray() ?? new Il2CppReferenceArray<Reward>(0);
        missionNode.finishCondition = config.finishConditions?.Select(ToFinishCondition).ToArray() ?? new Il2CppReferenceArray<FinishCondition>(0);

        missionNode.hasSender = !string.IsNullOrEmpty(config.sender);
        missionNode.sender = config.sender;
        missionNode.hasReciever = !string.IsNullOrEmpty(config.reciever); // ignore typo
        missionNode.reciever = config.reciever; // ignore typo

        missionNode.missionFinishEvent = config.missionFinishEvent.ToEventData(config.debugLabel);

        missionNode.postMissionsAfterPerformance = config.postMissionsAfterPerformance?.ToArray() ?? new string[0];
        missionNode.postEvents = config.postEvents?.ToArray() ?? new string[0];

        return missionNode;
    }

    public static Reward ToReward(this MissionRewardConfig config)
    {
        var reward = new Reward();
        reward.rewardType = config.rewardType;

        switch (config.rewardType)
        {
            case RewardType.UpgradeKizunaLevel:
                reward.rewardId = config.rewardId;
                break;
            case RewardType.GiveItem:
                reward.objectType = config.objectType ?? ObjectType.Food;
                reward.rewardIntArray = config.rewardIntArray?.ToArray() ?? new int[0];
                break;
            default:
                break;
        }

        return reward;
    }

    public static FinishCondition ToFinishCondition(this MissionFinishConditionConfig config)
    {
        var condition = new FinishCondition();
        condition.conditionType = config.conditionType;
        condition.label = config?.label ?? "";

        switch (config.conditionType)
        {
            case FinishCondition.ConditionType.ServeInWork:
                condition.amount = config.amount.HasValue ? config.amount.Value : 0;
                condition.sellableType = config.sellableType.HasValue ? config.sellableType.Value : SellableType.Food;
                break;
            case FinishCondition.ConditionType.SubmitItem:
                var product = condition.product;
                product.productType = config.productType ?? ProductType.Food;
                product.productId = config.productId ?? 0;
                product.productAmount = config.productAmount ?? 0;
                condition.product = product;
                break;
            case FinishCondition.ConditionType.SubmitByTag:
                condition.amount = config?.amount ?? 0; // 所需物品的数量
                condition.tag = config?.tag ?? 0; // 所需物品的标签
                condition.sellableType = config?.sellableType ?? SellableType.Food; // 所需 Sellable 的类型
                break;
            case FinishCondition.ConditionType.SubmitByTags:
                condition.amount = config?.amount ?? 0; // 所需物品的数量
                condition.tags = config?.tags ?? new int[0]; // 所需物品的标签
                condition.sellableType = config?.sellableType ?? SellableType.Food; // 所需 Sellable 的类型
                break;
            case FinishCondition.ConditionType.SubmitByAnyOneTag:
                condition.amount = config?.amount ?? 0; // 所需物品的数量
                condition.tags = config?.tags ?? new int[0]; // 所需物品的标签
                condition.sellableType = config?.sellableType ?? SellableType.Food; // 所需 Sellable 的类型
                break;
            default:
                break;
        }

        return condition;
    }

    #endregion

    #region EventNode Mappers

    public static EventNode ToEventNode(this EventNodeConfig config)
    {
        var eventNode = ScriptableObject.CreateInstance<EventNode>();
        eventNode.name = config.label;
        eventNode.label = config.label;
        eventNode.debugLabel = config.debugLabel;

        eventNode.scheduledEvent = config.scheduledEvent.ToScheduledEvent(config.debugLabel);

        eventNode.rewards = config.rewards?.Select(ToReward).ToArray() ?? new Il2CppReferenceArray<Reward>(0);
        eventNode.postRewards = config.postRewards?.Select(ToReward).ToArray() ?? new Il2CppReferenceArray<Reward>(0);

        eventNode.postMissionsAfterPerformance = config.postMissionsAfterPerformance?.ToArray() ?? new string[0];
        eventNode.postEvents = config.postEvents?.ToArray() ?? new string[0];

        return eventNode;
    }

    public static SchedulerNode.ScheduledEvent ToScheduledEvent(this ScheduledEventConfig config, string debugLabel = "")
    {
        if (config == null) return new SchedulerNode.ScheduledEvent();

        return new SchedulerNode.ScheduledEvent()
        {
            trigger = config.trigger.ToTrigger(debugLabel),
            eventData = config.eventData.ToEventData(debugLabel),
        };
    }

    public static SchedulerNode.Trigger ToTrigger(this TriggerConfig config, string debugLabel)
    {
        if (config == null) return new SchedulerNode.Trigger();

        var trigger = new SchedulerNode.Trigger();
        trigger.triggerType = config.triggerType;

        switch (config.triggerType)
        {
            case TriggerType.KizunaCheckPoint:
                trigger.triggerId = config.triggerId;
                break;
            case TriggerType.OnTalkWithCharacter:
                trigger.triggerId = config.triggerId;
                break;
            default:
                Log.Error($"Unsupported event trigger type {config.triggerType} in EventNode {debugLabel}");
                break;
        }
        return trigger;
    }

    public static SchedulerNode.Event ToEventData(this EventDataConfig config, string debugLabel = "")
    {
        if (config == null) return new SchedulerNode.Event();

        switch (config.eventType)
        {
            case SchedulerNode.Event.EventType.Null:
                return new SchedulerNode.Event()
                {
                    eventType = config.eventType
                };
            case SchedulerNode.Event.EventType.Dialog:
                return new SchedulerNode.Event()
                {
                    eventType = config.eventType,
                    runtimeDialogPackage = ResourceExManager.GetBuiltDialogPackage(config.dialogPackageName)
                };
            default:
                Log.Error($"Unsupported event type {config.eventType} in EventNode {debugLabel}");
                return null;
        }
    }

    #endregion

    #region SpecialGuest Mappers

    public static SpecialGuest ToSpecialGuest(this CharacterConfig config, SpecialGuest template)
    {
        if (config.guest == null) return null;

        return SpecialGuestBuilder
            .FromTemplate(template)
            .WithId(config.id)
            .WithStringId(config.label)
            .WithFundRange(config.guest.fundRangeLower, config.guest.fundRangeUpper)
            .WithHateFoodTags(config.guest.hateFoodTag)
            .WithLikeFoodTags(config.guest.likeFoodTag)
            .WithLikeBevTags(config.guest.likeBevTag)
            .WithCommisionAreaLabel(config.kizuna?.commisionAreaLabel)
            // .WithDestination(config.label) // DO NOT ENABLE
            .WithCharacterKizunaLevel1Welcome(GetDialogPackagesFromNames(config.kizuna?.lv1Welcome))
            .WithCharacterKizunaLevel2Welcome(GetDialogPackagesFromNames(config.kizuna?.lv2Welcome))
            .WithCharacterKizunaLevel3Welcome(GetDialogPackagesFromNames(config.kizuna?.lv3Welcome))
            .WithCharacterKizunaLevel4Welcome(GetDialogPackagesFromNames(config.kizuna?.lv4Welcome))
            .WithCharacterKizunaLevel5Welcome(GetDialogPackagesFromNames(config.kizuna?.lv5Welcome))
            .WithCharacterKizunaLevel1ChatData(config.kizuna?.lv1ChatData)
            .WithCharacterKizunaLevel2ChatData(config.kizuna?.lv2ChatData)
            .WithCharacterKizunaLevel3ChatData(config.kizuna?.lv3ChatData)
            .WithCharacterKizunaLevel4ChatData(config.kizuna?.lv4ChatData)
            .WithCharacterKizunaLevel5ChatData(config.kizuna?.lv5ChatData)
            .WithCharacterKizunaLevel2InviteSucceed(GetDialogPackagesFromNames(config.kizuna?.lv2InviteSucceed))
            .WithCharacterKizunaLevel2InviteFailed(GetDialogPackagesFromNames(config.kizuna?.lv2InviteFailed))
            .WithCharacterKizunaLevel3InviteSucceed(GetDialogPackagesFromNames(config.kizuna?.lv3InviteSucceed))
            .WithCharacterKizunaLevel3InviteFailed(GetDialogPackagesFromNames(config.kizuna?.lv3InviteFailed))
            .WithCharacterKizunaLevel4InviteSucceed(GetDialogPackagesFromNames(config.kizuna?.lv4InviteSucceed))
            .WithCharacterKizunaLevel4InviteFailed(GetDialogPackagesFromNames(config.kizuna?.lv4InviteFailed))
            .WithCharacterKizunaLevel5InviteSucceed(GetDialogPackagesFromNames(config.kizuna?.lv5InviteSucceed))
            .WithCharacterKizunaLevel3RequestIngerdient(GetDialogPackagesFromNames(config.kizuna?.lv3RequestIngerdient))
            .WithCharacterKizunaLevel4RequestIngerdient(GetDialogPackagesFromNames(config.kizuna?.lv4RequestIngerdient))
            .WithCharacterKizunaLevel5RequestIngerdient(GetDialogPackagesFromNames(config.kizuna?.lv5RequestIngerdient))
            .WithCharacterKizunaLevel4RequestBeverage(GetDialogPackagesFromNames(config.kizuna?.lv4RequestBeverage))
            .WithCharacterKizunaLevel5RequestBeverage(GetDialogPackagesFromNames(config.kizuna?.lv5RequestBeverage))
            .WithCharacterKizunaLevel5Commision(GetDialogPackagesFromNames(config.kizuna?.lv5Commision))
            .WithCharacterKizunaLevel5CommisionFinish(GetDialogPackagesFromNames(config.kizuna?.lv5CommisionFinish))
            .WithHideInAlbum(config.hideInAlbum)
            .WithIsParticular(config.isParticular)
            .WithIsCollabCharacter(config.isCollabCharacter)
            .Build();
    }

    private static System.Collections.Generic.List<DialogPackage> GetDialogPackagesFromNames(System.Collections.Generic.List<string> packageNames)
    {
        if (packageNames == null || packageNames.Count == 0)
            return new System.Collections.Generic.List<DialogPackage>();

        return packageNames
            .Select(name => ResourceExManager.GetBuiltDialogPackage(name))
            .Where(package => package != null)
            .ToList();
    }

    #endregion

    #region Merchant Mappers
    public static Product ToProduct(this ProductConfig config, int? overrideAmount = null)
    {
        return new Product
        {
            productType = config.productType,
            productId = config.productId,
            productAmount = overrideAmount ?? config.productAmount,
            productLabel = config.productLabel
        };
    }
    public static Merchant.Merchandise ToMerchandise(this MerchandiseConfig config)
    {
        return new Merchant.Merchandise
        {
            item = config.item.ToProduct(UnityEngine.Random.Range(config.itemAmountMin, config.itemAmountMax)),
            sellProbability = config.sellProbability,
            itemAmountRange = new Vector2Int(config.itemAmountMin, config.itemAmountMax)
        };
    }

    public static TrackedMerchant GenTrackedMerchant(this MerchantConfig config)
    {
        return new TrackedMerchant()
        {
            key = config.key,
            products = config.merchandise
                .Where(x => UnityEngine.Random.value <= x.sellProbability)
                .Select(x => x.item.ToProduct(UnityEngine.Random.Range(x.itemAmountMin, x.itemAmountMax)))
                .ToIl2CppReferenceArray(),
            currentPriceMultiplier = UnityEngine.Random.Range(config.priceMultiplierMin, config.priceMultiplierMax),
        };
    }

    #endregion
}
