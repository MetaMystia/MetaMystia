using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mystia.Data;

using Common.DialogUtility;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;
using MetaMystia.ResourceEx.Registries;

namespace MetaMystia.Data.ResourceEx;

/// <summary>
/// ResourceEx 配置对象到框架数据代理结构的映射（食材、食物、饮料、菜谱、服饰、Buff、符卡、任务／事件节点）。
///
/// 图片：资源包里的图片只存在于内存中的 ZIP 归档内，而框架只接受「相对模组目录的 PNG 路径」。
/// 因此这里把包内 PNG 原样解包到 <c>&lt;模组目录&gt;/ResourceExAssets/&lt;包 label&gt;/&lt;包内路径&gt;</c>，
/// 再把该相对路径交给框架：目录结构与 <c>rex://</c> 之后的包内路径一致，前缀换成了解包根。
/// 非 PNG 图片和包内找不到的图片记录警告并跳过（框架的 SpriteFiles 只解析 PNG），
/// 与迁移前的行为一致（迁移前取不到 Sprite 时同样不写图片）。
/// </summary>
[AutoLog]
public static partial class DatabaseMappings
{
    /// <summary>解包根目录，相对模组目录。</summary>
    private const string ExtractRoot = "ResourceExAssets";

    /// <summary>符卡宣言立绘的默认 pivot：宣言动画按 pivot 对位，取原版 104 张立绘的平均值。</summary>
    private static readonly (float X, float Y) DefaultPortrayalPivot = (0.497f, 0.644f);

    /// <summary>URI → 相对路径，避免同一次注入里重复解包与重复告警。</summary>
    private static readonly Dictionary<string, string> Materialized = new(StringComparer.Ordinal);

    public static IngredientData ToData(this IngredientConfig config) => new()
    {
        Id = config.id,
        Name = config.name,
        Description = config.description,
        Level = config.level,
        BaseValue = config.baseValue,
        Prefix = config.prefix,
        Tags = config.tags?.ToArray(),
        Picture = Picture(config.spritePath),
    };

    public static FoodData ToData(this FoodConfig config) => new()
    {
        Id = config.id,
        Name = config.name,
        Description = config.description,
        Level = config.level,
        BaseValue = config.baseValue,
        Tags = config.tags?.ToArray(),
        BannedTags = config.banTags?.ToArray(),
        Picture = Picture(config.spritePath),
    };

    public static BeverageData ToData(this BeverageConfig config) => new()
    {
        Id = config.id,
        Name = config.name,
        Description = config.description,
        Level = config.level,
        BaseValue = config.baseValue,
        Tags = config.tags?.ToArray(),
        BannedTags = [],
        Picture = Picture(config.spritePath),
    };

    public static RecipeData ToData(this RecipeConfig config) => new()
    {
        Id = config.id,
        FoodId = config.foodId,
        // 游戏 Cooker.CookerType 与框架 CookerKind 取值逐值一致。
        Cooker = (CookerKind)(int)config.cookerType,
        Seconds = config.cookTime,
        Ingredients = config.ingredients?.ToArray(),
    };

    public static ClothesData ToData(this ClothConfig config) => new()
    {
        Id = config.id,
        Name = config.name,
        Description = config.description,
        Picture = Picture(config.spritePath),
        IzakayaSkinIndex = config.izakayaSkinIndex,
        IzkayaHorizontalOffset = config.izkayaHorizontalOffset,
        NotebookHorizontalOffset = config.notebookHorizontalOffset,
        NotebookVerticalOffset = config.notebookVerticalOffset,
        NotebookTitleHorizontalOffset = config.notebookUITitleHorizontalOffset,
        NotebookTitleVerticalOffset = config.notebookUITitleVerticalOffset,
        Body = Pictures(config.pixelFullConfig?.mainSprite),
        Eyes = Pictures(config.pixelFullConfig?.eyeSprite),
    };

    public static BuffData ToData(this BuffConfig config) => new()
    {
        Id = config.id,
        Name = config.name,
        Description = config.description,
        Picture = Picture(config.icon),
    };

    /// <summary>
    /// 商人：商人定义、运行时追踪记录（含商品生成与已拥有食谱过滤）与缺键安全由框架建立，
    /// 这里只映射配置字段；迎宾／空手对话按包名交给框架在写入时从 <c>DataBaseDay.allDialogPackages</c> 解析。
    /// </summary>
    public static MerchantData ToData(this MerchantConfig config) => new()
    {
        Key = config.key,
        WelcomeDialogs = config.welcomeDialogPackageNames?.ToArray(),
        EmptyDialogs = config.nullDialogPackageNames?.ToArray(),
        PriceMin = config.priceMultiplierMin,
        PriceMax = config.priceMultiplierMax,
        LeastSellCount = config.leastSellNum,
        // 资源包的 productType 与框架 GoodsKind 逐值一致（同为游戏 Product.ProductType 的顺序）。
        Offers = config.merchandise?.Select(offer => new MerchantOffer
        {
            Kind = (GoodsKind)(int)offer.item.productType,
            ItemId = offer.item.productId,
            Label = offer.item.productLabel,
            Chance = offer.sellProbability,
            AmountMin = offer.itemAmountMin,
            AmountMax = offer.itemAmountMax,
        }).ToArray(),
    };

    /// <summary>
    /// 白天地图：只映射数据面字段。地图本体的引擎对象（Tilemap／相机／高度图／内存 GameObject）与
    /// 内存地图的地址引用仍由模组构建（见 <c>DayMapRegistry</c>），因此这里不填 <see cref="DayMapData.MapAsset"/>。
    /// 资源包不声明父区域、采集点与所属居酒屋：迁移前同样写空值／空集合。
    /// </summary>
    public static DayMapData ToData(this DayMapConfig config) => new()
    {
        Id = config.id,
        Label = DayMapRegistry.GetLabel(config.id),
        Name = config.name,
        Description = config.description ?? "",
        // 刷新点标签由框架拼成「地图 label + 点位名」，与迁移前 DayMapRegistry.GetMarker 的结果一致。
        SpawnMarkers = config.spawnMarkers?.Select(marker => new SpawnMarkerData
        {
            MapLabel = DayMapRegistry.GetLabel(config.id),
            Label = marker.name,
            X = marker.x,
            Y = marker.y,
            Rotation = (CharacterRotationKind)(int)marker.rotation,
        }).ToArray(),
    };

    /// <summary>
    /// 符卡：id 即所属角色 id（资源包里 spells 挂在 characters 下），label 取所属角色的标识。
    /// 找不到所属角色、实现或特效包不齐全、缺语言与立绘的符卡一律不注入（迁移前同样跳过注册）。
    /// 效果实现由框架按 <c>SpellId</c> 匹配模组的 <c>ISpell</c>；宣言立绘的 pivot 走数据面，见下。
    /// </summary>
    public static SpellData? ToData(this SpellConfig config)
    {
        var owner = SpecialGuestRegistry.GetCharacterConfig(config.id);
        if (owner is null || string.IsNullOrWhiteSpace(owner.label))
            return null;

        if (!SpellRegistry.IsReady(config)
            || string.IsNullOrWhiteSpace(config.positive?.name) || string.IsNullOrWhiteSpace(config.positive?.description)
            || string.IsNullOrWhiteSpace(config.negative?.name) || string.IsNullOrWhiteSpace(config.negative?.description)
            || string.IsNullOrWhiteSpace(config.positive?.portrait) || string.IsNullOrWhiteSpace(config.negative?.portrait))
        {
            Log.LogWarning($"符卡 {config.id} 的实现、特效包或名称、说明、立绘不齐全，跳过注入");
            return null;
        }

        // 宣言动画按 pivot 对位；资源包未声明时取原版 104 张立绘的平均值。
        var (pivotX, pivotY) = config.portrayalPivot is [var x, var y] ? (x, y) : DefaultPortrayalPivot;

        return new SpellData
        {
            Id = config.id,
            Label = owner.label,
            // 游戏以稀客 id 询问符卡与「是否拥有符卡」，本模组的符卡 id 即角色 id。
            CharacterId = config.id,
            Name = config.positive?.name,
            Description = config.positive?.description,
            NegativeName = config.negative?.name,
            NegativeDescription = config.negative?.description,
            Portrait = Picture(config.positive?.portrait),
            PositivePortrait = Picture(config.positive?.portrait),
            NegativePortrait = Picture(config.negative?.portrait),
            PortraitPivotX = pivotX,
            PortraitPivotY = pivotY,
        };
    }

    /// <summary>
    /// 任务节点：字段面即 <c>SchedulerNodeCollection.MissionNode</c> 的配置子集。
    /// 资源包不表达图连接（<c>preNodes</c>/<c>postMissions</c>）与对话包以外的演出资源：
    /// 后置节点由 <c>postEvents</c>/<c>postMissionsAfterPerformance</c> 给出，图连接写空数组。
    /// 对话包只交包名，由框架在写表时从 <c>DataBaseDay.allDialogPackages</c> 解析。
    /// </summary>
    public static MissionNodeData ToData(this MissionNodeConfig config) => new()
    {
        Label = config.label,
        DebugLabel = config.debugLabel,
        Name = config.title,
        Description = config.description,
        MissionType = (int)config.missionType,
        Sender = config.sender,
        Receiver = config.reciever,
        Rewards = config.rewards?.Select(ToData).ToArray(),
        PostRewards = config.postRewards?.Select(ToData).ToArray(),
        FinishConditions = config.finishConditions?.Select(ToData).ToArray(),
        MissionFinishEvent = config.missionFinishEvent.ToData(),
        MissionFailedEvent = config.missionFailedEvent.ToData(),
        MissionTimeLimit = config.missionTimeLimit.ToData(),
        IsTimedMission = config.isTimedMission,
        MissionFailedAction = (int)config.missionFailedAction,
        PostMissionsAfterPerformance = config.postMissionsAfterPerformance?.ToArray(),
        PostEvents = config.postEvents?.ToArray(),
    };

    /// <summary>
    /// 事件节点：触发时间、触发条件与播放的事件均为配置字段的直接映射。
    /// 资源包只表达对话包事件（<c>Event.EventType.Dialog</c>），Timeline 资源不在数据面（见迁移缺口）。
    /// </summary>
    public static EventNodeData ToData(this EventNodeConfig config) => new()
    {
        Label = config.label,
        DebugLabel = config.debugLabel,
        ScheduledEvent = config.scheduledEvent.ToData(),
        Trigger = config.scheduledEvent?.trigger.ToData(),
        Rewards = config.rewards?.Select(ToData).ToArray(),
        PostRewards = config.postRewards?.Select(ToData).ToArray(),
        PostMissionsAfterPerformance = config.postMissionsAfterPerformance?.ToArray(),
        PostEvents = config.postEvents?.ToArray(),
    };

    /// <summary>奖励：资源包只声明类型、目标标识、物品类型与数值载荷，其余字段保持默认。</summary>
    public static SchedulerRewardData ToData(this MissionRewardConfig config) => new()
    {
        RewardType = (int)config.rewardType,
        RewardId = config.rewardId,
        ObjectType = config.objectType is { } objectType ? (int)objectType : null,
        RewardIntArray = config.rewardIntArray?.ToArray(),
    };

    /// <summary>
    /// 完成条件：字段逐个对应；框架按条件类型决定哪些字段参与判定（与迁移前 <c>Mappers.ToFinishCondition</c> 同构）。
    /// </summary>
    public static MissionFinishConditionData ToData(this MissionFinishConditionConfig config) => new()
    {
        ConditionType = (int)config.conditionType,
        Amount = config.amount,
        Tag = config.tag,
        Tags = config.tags,
        SellableType = config.sellableType is { } sellableType ? (int)sellableType : null,
        Label = config.label,
        ProductType = config.productType is { } productType ? (int)productType : null,
        ProductId = config.productId,
        ProductAmount = config.productAmount,
    };

    /// <summary>
    /// 触发条件：迁移前 <c>Mappers.ToTrigger</c> 只对 KizunaCheckPoint／OnTalkWithCharacter 填 triggerId，
    /// 对 OnWorkEnd 填空 triggerId 与时间；数据面统一照搬配置，游戏按 triggerType 取用需要的字段
    /// （推断：未使用字段对判定无影响）。缺省时交空值，由框架写默认触发条件。
    /// </summary>
    public static SchedulerTriggerData? ToData(this TriggerConfig config) => config is null
        ? null
        : new SchedulerTriggerData
        {
            TriggerType = (int)config.triggerType,
            TriggerId = config.triggerId,
            Time = config.time.ToData(),
        };

    /// <summary>触发日：常量日与随机区间同时写入，游戏按 <c>dayCalcType</c> 取用（同迁移前的映射）。</summary>
    public static SchedulerDayData? ToData(this DayConfig config) => config is null
        ? null
        : new SchedulerDayData
        {
            DayType = (int)config.dayType,
            CalcType = (int)config.dayCalcType,
            Day = config.day,
            DayRangeMin = config.dayRangeMin,
            DayRangeMax = config.dayRangeMax,
        };

    /// <summary>播放的事件：只承载对话包名（框架写表时解析），Timeline 资源不在此表达。</summary>
    public static SchedulerEventData? ToData(this EventDataConfig config) => config is null
        ? null
        : new SchedulerEventData
        {
            EventType = (int)config.eventType,
            DialogPackage = config.dialogPackageName,
        };

    public static SchedulerEventData? ToData(this ScheduledEventConfig config) => config?.eventData.ToData();

    /// <summary>
    /// 特殊客人：一次覆盖迁移前 <c>SpecialGuestRegistry</c> 的四条写入路径
    /// （<c>DataBaseCharacter.SpecialGuest</c>／<c>SpecialGuestVisual</c>／<c>DataBaseLanguage.SpecialGuest</c>／
    /// 店家刷客池）。立绘、身体帧与眼睛帧都用解包后的 PNG 相对路径，不传 Sprite。
    ///
    /// 桥接未写入的字段（<c>Kind</c>／<c>IsParticular</c>／<c>IsCollabCharacter</c>／<c>HideInAlbum</c>／
    /// <c>CommisionAreaLabel</c>／<c>Kizuna</c>／<c>SpawnMarker</c>／<c>GuestRequestLine.Enable</c>）仍按配置填值，
    /// 但它们不会生效（见迁移缺口清单），对应行为回落为模板值或由模组运行时链路承担。
    /// </summary>
    public static SpecialGuestData ToData(this CharacterConfig config)
    {
        var (portraits, notebookFace) = Portraits(config);
        return new SpecialGuestData
        {
            Id = config.id,
            Label = config.label,
            Kind = Kind(config),
            Name = config.name,
            Description1 = Description(config, 0),
            Description2 = Description(config, 1),
            Description3 = Description(config, 2),
            FundMin = config.guest?.fundRangeLower ?? 0,
            FundMax = config.guest?.fundRangeUpper ?? 0,
            HateFoodTags = config.guest?.hateFoodTag?.ToArray(),
            LikeFood = Weights(config.guest?.likeFoodTag),
            LikeBeverages = Weights(config.guest?.likeBevTag),
            FoodRequests = Requests(config.guest?.foodRequests, ConfigManager.FoodRequestMode.Value),
            BeverageRequests = Requests(config.guest?.bevRequests, ConfigManager.BevRequestMode.Value),
            Evaluations = config.guest?.evaluation?.ToArray(),
            Conversations = config.guest?.conversation?.ToArray(),
            Portraits = portraits,
            NotebookFace = notebookFace,
            Body = Frames(config.characterSpriteSetCompact?.mainSprite),
            Eyes = Frames(config.characterSpriteSetCompact?.eyeSprite),
            Spawns = Spawns(config),
            IsParticular = config.isParticular,
            IsCollabCharacter = config.isCollabCharacter,
            HideInAlbum = config.hideInAlbum,
            CommisionAreaLabel = config.kizuna?.commisionAreaLabel,
            SpawnMarker = Marker(config),
            Kizuna = Kizuna(config.kizuna),
        };
    }

    /// <summary>
    /// 迁移前三条写入路径覆盖到的角色配置：客人记录（<c>guest</c>）、像素与立绘（<c>characterSpriteSetCompact</c>）、
    /// 角色文本与立绘（<c>portraits</c>）。只声明其中一部分的配置迁移前后写入的表相同。
    /// </summary>
    public static bool IsGuestConfig(this CharacterConfig config) =>
        config is { guest: not null }
        || config.characterSpriteSetCompact is not null
        || config.portraits is { Count: > 0 };

    /// <summary>
    /// 对话包：名字、逐行文本、说话人身份／位置与逐行动作一起交出。
    /// <para>
    /// 注意：桥接当前只用**说话人身份、位置与文本**重建每一行（其余字段与 <c>actions[]</c> 仍复用模板第 0 行的 meta，
    /// 见迁移缺口清单），因此需要行内动作的对话必须继续走模组自建的对话包
    /// （礼物信箱、剧情回放、商人迎宾用的是 <c>DialogRegistry.GetBuiltDialogPackage</c>）。
    /// </para>
    /// </summary>
    public static DialogData ToData(this DialogPackageConfig config) => new()
    {
        Name = config.name,
        Lines = config.dialogList?.Select(line => new DialogLine
        {
            // SpeakerKind／DialogSide／DialogActionKind 与游戏 SpeakerIdentity.Identity／Position／ActionType 逐值一致。
            Speaker = (SpeakerKind)(int)line.characterType,
            SpeakerId = line.characterId,
            Portrait = line.pid,
            Side = (DialogSide)(int)line.position,
            Text = line.text,
            Actions = line.actions?.Select(action => action.ToData()).ToArray(),
            // 与迁移前 BuildDialogPackage 写死的行级 flag 相同。
            IsSpeakInForeground = true,
            IsDark = false,
            UseNameInText = true,
            UseOverrideSprite = false,
        }).ToArray(),
    };

    /// <summary>行内动作：CG／BG 的图按 PNG 相对路径给出；音效留给模组（见缺口：需要解包音频，且桥接未写逐行动作）。</summary>
    public static DialogActionData ToData(this DialogActionConfig config) => new()
    {
        ActionType = (DialogActionKind)(int)config.actionType,
        Sprite = config.actionType is ActionType.CG or ActionType.BG ? Picture(config.sprite) : null,
        Sound = null,
        Options = config.options?.Select(option => new DialogBranchOptionData
        {
            // 资源包的 jump 与框架的 Jump 同为 1 起始的对话序号（对话数 + 1 表示结束）。
            Text = option.text,
            Jump = option.jump,
            Price = option.price,
        }).ToArray(),
        Index = config.index,
        ExitCode = config.exitCode,
        ShouldSet = config.shouldSet,
    };

    /// <summary>
    /// 立绘：迁移前按 <c>portraits</c> 的数组下标索引（<c>faceInNoteBook</c> 就是该下标），
    /// 而框架只接受能解包的 PNG。失败的条目必须丢弃，所以这里重建下标映射，
    /// 让笔记本脸仍指向同一张图（对应不上时回落 0，与迁移前指向空条目的效果一致）。
    /// </summary>
    private static (string[] Paths, int NotebookFace) Portraits(CharacterConfig config)
    {
        var wanted = config.faceInNoteBook ?? 0;
        if (config.portraits is not { Count: > 0 })
            return (null, wanted);

        var paths = new List<string>(config.portraits.Count);
        var notebookFace = -1;
        for (var index = 0; index < config.portraits.Count; index++)
        {
            var path = Picture(config.portraits[index]?.path);
            if (path is null)
                continue;
            if (index == wanted)
                notebookFace = paths.Count;
            paths.Add(path);
        }

        return (paths.ToArray(), notebookFace < 0 ? wanted : notebookFace);
    }

    /// <summary>像素帧：与立绘同理，解包失败的帧必须丢弃，否则框架加载时会抛异常。</summary>
    private static string[] Frames(List<string> uris) =>
        uris?.Select(Picture).Where(path => path is not null).ToArray();

    private static CharacterKind Kind(CharacterConfig config)
    {
        // 资源包的 type 用的是 SpeakerIdentity.Identity 的名字（Special／Normal／Self）。
        if (string.IsNullOrEmpty(config.type))
            return CharacterKind.Special;
        if (Enum.TryParse<SpeakerIdentity.Identity>(config.type, true, out var identity))
            return identity switch
            {
                SpeakerIdentity.Identity.Self => CharacterKind.Self,
                SpeakerIdentity.Identity.Normal => CharacterKind.Normal,
                _ => CharacterKind.Special,
            };
        Log.LogWarning($"角色 {config.name} ({config.id}) 的 type「{config.type}」不是 Special／Normal／Self，按特殊客人注入。");
        return CharacterKind.Special;
    }

    private static string Description(CharacterConfig config, int index) =>
        config.descriptions is { Count: > 0 } && config.descriptions.Count > index ? config.descriptions[index] : "";

    private static TagWeight[] Weights(List<WeightedTagConfig> configs) =>
        configs?.Select(tag => new TagWeight { TagId = tag.tagId, Weight = tag.weight }).ToArray();

    /// <summary>
    /// 点单请求行：迁移前按 <c>FoodRequestMode</c>／<c>BevRequestMode</c> 过滤（包内开关／强制开／强制关），
    /// 写进语言表时是「标签 id → 台词」的字典（同标签后者覆盖前者）。桥接不读 <c>Enable</c>，
    /// 所有过滤都在这里完成，因此交出的行一律 <c>Enable = true</c>（见缺口清单）。
    /// </summary>
    private static GuestRequestLine[] Requests(List<RequestConfig> configs, RequestEnableMode mode)
    {
        if (configs is null || mode == RequestEnableMode.ForceDisable)
            return null;
        return configs
            .Where(request => mode == RequestEnableMode.ForceEnable || request.enable)
            .GroupBy(request => request.tagId)
            .Select(group => group.Last())
            .Select(request => new GuestRequestLine { TagId = request.tagId, Line = request.request, Enable = true })
            .ToArray();
    }

    private static GuestSpawn[] Spawns(CharacterConfig config) =>
        config.guest?.spawn?.Select(spawn => new GuestSpawn
        {
            IzakayaId = spawn.izakayaId,
            GuestId = config.id,
            Probability = spawn.relativeProb,
            OnlyAfterUnlock = spawn.onlySpawnAfterUnlocking,
            OnlyWhenPlaceRecorded = spawn.onlySpawnWhenPlaceBeRecorded,
        }).ToArray();

    /// <summary>
    /// 白天刷新点。点位名取角色 label：桥接按 <c>SpecialGuestData.SpawnMarker</c> 生成的点位就叫 label，
    /// 「把角色挪到该图」也按 label 定位。
    /// </summary>
    private static SpawnMarkerData? Marker(CharacterConfig config) => config.spawnMarker is null
        ? null
        : new SpawnMarkerData
        {
            MapLabel = config.spawnMarker.mapLabel,
            Label = config.label,
            X = config.spawnMarker.x,
            Y = config.spawnMarker.y,
            Rotation = (CharacterRotationKind)(int)config.spawnMarker.rotation,
        };

    /// <summary>羁绊（Kizuna）配置：源包字段拼写（Ingerdient／Commision）与框架字段一一对应。桥接不写（见缺口清单）。</summary>
    private static SpecialGuestKizunaData? Kizuna(KizunaEventConfig kizuna) => kizuna is null
        ? null
        : new SpecialGuestKizunaData
        {
            PrerequisiteEvent1 = kizuna.lv1UpgradePrerequisiteEvent,
            PrerequisiteEvent2 = kizuna.lv2UpgradePrerequisiteEvent,
            PrerequisiteEvent3 = kizuna.lv3UpgradePrerequisiteEvent,
            PrerequisiteEvent4 = kizuna.lv4UpgradePrerequisiteEvent,
            Welcome1 = kizuna.lv1Welcome?.ToArray(),
            Welcome2 = kizuna.lv2Welcome?.ToArray(),
            Welcome3 = kizuna.lv3Welcome?.ToArray(),
            Welcome4 = kizuna.lv4Welcome?.ToArray(),
            Welcome5 = kizuna.lv5Welcome?.ToArray(),
            ChatData1 = kizuna.lv1ChatData?.ToArray(),
            ChatData2 = kizuna.lv2ChatData?.ToArray(),
            ChatData3 = kizuna.lv3ChatData?.ToArray(),
            ChatData4 = kizuna.lv4ChatData?.ToArray(),
            ChatData5 = kizuna.lv5ChatData?.ToArray(),
            InviteSucceed2 = kizuna.lv2InviteSucceed?.ToArray(),
            InviteSucceed3 = kizuna.lv3InviteSucceed?.ToArray(),
            InviteSucceed4 = kizuna.lv4InviteSucceed?.ToArray(),
            InviteSucceed5 = kizuna.lv5InviteSucceed?.ToArray(),
            InviteFailed2 = kizuna.lv2InviteFailed?.ToArray(),
            InviteFailed3 = kizuna.lv3InviteFailed?.ToArray(),
            InviteFailed4 = kizuna.lv4InviteFailed?.ToArray(),
            RequestIngredient3 = kizuna.lv3RequestIngerdient?.ToArray(),
            RequestIngredient4 = kizuna.lv4RequestIngerdient?.ToArray(),
            RequestIngredient5 = kizuna.lv5RequestIngerdient?.ToArray(),
            RequestBeverage4 = kizuna.lv4RequestBeverage?.ToArray(),
            RequestBeverage5 = kizuna.lv5RequestBeverage?.ToArray(),
            Commision5 = kizuna.lv5Commision?.ToArray(),
            CommisionFinish5 = kizuna.lv5CommisionFinish?.ToArray(),
        };

    private static string[] Pictures(List<string> uris) => uris?.Select(Picture).ToArray();

    private static string Picture(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return null;

        if (Materialized.TryGetValue(uri, out var cached))
            return cached;

        var relative = Extract(uri);
        Materialized[uri] = relative;
        return relative;
    }

    private static string Extract(string uri)
    {
        if (!RexAssetRegistry.Assets.TryGetValue(uri, out var asset) || asset is not RexImageAsset image)
        {
            Log.LogWarning($"资源包内找不到图片 {uri}，已跳过。");
            return null;
        }

        if (!string.Equals(Path.GetExtension(image.Path), ".png", StringComparison.OrdinalIgnoreCase))
        {
            Log.LogWarning($"图片 {uri} 不是 PNG，框架只接受 PNG 路径，已跳过。");
            return null;
        }

        var relative = Path.Combine(ExtractRoot, image.PackageName, Path.ChangeExtension(image.Path, ".png"));
        var full = Path.Combine(ModRuntime.Paths.ModDirectory, relative);
        if (!File.Exists(full) || new FileInfo(full).Length != image.Bytes.Length)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, image.Bytes);
        }

        return relative.Replace('\\', '/');
    }
}
