using System.Collections.Generic;
using System.Linq;

using Mystia.Data;

using MetaMystia.Data.ResourceEx;
using MetaMystia.ResourceEx.Registries;

namespace MetaMystia.Data;

/// <summary>
/// 把 ResourceEx 资源包的内容以代理结构交给框架，由框架写入游戏数据库。
/// 本类负责 E1 表（食材、食物、饮料、菜谱、服饰、Buff 文本）、E2 的特殊客人与对话、
/// E3 的商人、白天地图与 E4 的符卡、任务／事件节点；NPC 不注入，理由见 <c>OnInjectNpcs</c>。
/// 框架在 DataBaseCore.Initialize 后缀收集数据（与原 DataBaseCore/DataBaseLanguage 后缀补丁同一时机），
/// 各表在对应的 Initialize 后缀写入。
///
/// 不重写 <c>OnInjectItems</c> 与 <c>OnInjectBadges</c>：资源包格式没有独立的道具与徽章声明，
/// 服装本身就派生一条道具（<c>Item.IsClothes</c> 只由 Clothes 表决定），框架在 <c>OnInjectClothes</c>
/// 链路里同时写 <c>DataBaseCore.Items</c>/<c>DataBaseLanguage.Items</c>，重复注入只会写同样的值。
/// </summary>
[AutoLog]
public sealed partial class ModDatabaseExtension : IDatabaseExtension
{
    public void OnInjectIngredients(List<IngredientData> ingredients)
    {
        ResourceExManager.PrepareDatabaseInjection();
        ingredients.AddRange(IngredientRegistry.Configs.Select(config => config.ToData()));
    }

    public void OnInjectFoods(List<FoodData> foods)
    {
        ResourceExManager.PrepareDatabaseInjection();
        foods.AddRange(FoodRegistry.Configs.Select(config => config.ToData()));
    }

    public void OnInjectBeverages(List<BeverageData> beverages)
    {
        ResourceExManager.PrepareDatabaseInjection();
        beverages.AddRange(BeverageRegistry.Configs.Select(config => config.ToData()));
    }

    public void OnInjectRecipes(List<RecipeData> recipes)
    {
        ResourceExManager.PrepareDatabaseInjection();
        recipes.AddRange(RecipeRegistry.Configs.Select(config => config.ToData()));
    }

    public void OnInjectClothes(List<ClothesData> clothes)
    {
        ResourceExManager.PrepareDatabaseInjection();
        // 按 id 升序注入，保持资源包原有的服装顺序（迁移前的 dlcs 下标即由此顺序决定）。
        clothes.AddRange(ClothRegistry.Configs.OrderBy(config => config.id).Select(config => config.ToData()));
    }

    /// <summary>
    /// 普通客人：资源包格式没有普通客人所需的字段（基础资金倍率、评价、喜好／讨厌标签、闲聊池），
    /// 迁移前对 <c>type</c> 为 Normal 的角色也只记录日志、未写表，因此这里不注入。
    /// </summary>
    public void OnInjectNormalGuests(List<NormalGuestData> normalGuests)
    {
    }

    /// <summary>
    /// 特殊客人：客人记录、像素集、立绘（相对模组目录的 PNG 路径，框架负责挂载）、角色文本、
    /// 点单请求行、评价与对话、店家刷客池。
    /// 桥接未写入的字段（<c>Kind</c>／<c>IsParticular</c>／<c>IsCollabCharacter</c>／<c>HideInAlbum</c>／
    /// <c>CommisionAreaLabel</c>／<c>Kizuna</c>／<c>SpawnMarker</c>／<c>GuestRequestLine.Enable</c>）见迁移缺口清单。
    /// </summary>
    public void OnInjectSpecialGuests(List<SpecialGuestData> specialGuests)
    {
        ResourceExManager.PrepareDatabaseInjection();
        // 迁移前的三条写入路径（客人记录／像素与立绘／角色文本）覆盖的是同一批配置。
        specialGuests.AddRange(SpecialGuestRegistry.GetAllCharacterConfigs()
            .Where(config => config.IsGuestConfig())
            .Select(config => config.ToData()));
    }

    /// <summary>
    /// 白天场景 NPC：不注入。资源包角色是特殊客人，其白天场景的 NPC 记录由原版
    /// <c>LoadingSceneManager</c> 从注入后的 <c>DataBaseCharacter.SpecialGuest</c>（<c>new NPC(specialGuest)</c>）生成，
    /// destination 与对话池键都取该特殊客人的 destination（`RunTimeAlbum.UpgradeOrGenerateSpecialNPCKizuna` 也按它写池）；
    /// 用 <c>OnInjectNpcs</c> 覆盖同名记录只会改掉定位点与对话池键，反而切断了模组的定位链路
    /// （点位由桥接的点位管线按角色 label 生成／定位，见资源包白天地图文档）。
    /// </summary>
    public void OnInjectNpcs(List<NpcData> npcs)
    {
    }

    /// <summary>
    /// 对话包：名字、逐行文本、说话人身份／位置，以及逐行动作。
    /// 桥接当前只写说话人与位置（<c>actions[]</c> 与 5 个行级 flag 仍复用模板第 0 行的 meta，见缺口清单），
    /// 因此需要行内动作的对话仍走模组自建的对话包（礼物信箱、剧情回放、商人迎宾按包名取用）。
    /// </summary>
    public void OnInjectDialogs(List<DialogData> dialogs)
    {
        ResourceExManager.PrepareDatabaseInjection();
        // 模组侧对话包（含逐行动作与文本覆盖回调）继续构建：框架只写游戏表，礼物信箱／剧情回放／商人迎宾仍用自建实例。
        DialogRegistry.BuildAllDialogPackages();
        dialogs.AddRange(DialogRegistry.Configs.Select(config => config.ToData()));
    }

    /// <summary>
    /// 符卡：由框架写符卡语言、宣言立绘、「是否拥有符卡」标记与符卡条目，效果实例是框架的
    /// <c>BridgeSpell</c>，执行时按 <c>SpellId</c> 匹配模组实现的 <c>ISpell</c>。
    /// 找不到所属角色、实现或资源不齐全的符卡不注入（迁移前同样跳过注册）。
    /// </summary>
    public void OnInjectSpells(List<SpellData> spells)
    {
        ResourceExManager.PrepareDatabaseInjection();
        foreach (var config in SpellRegistry.Configs)
        {
            if (config.ToData() is { } spell)
                spells.Add(spell);
        }
    }

    /// <summary>
    /// 任务节点：节点表、节点映射与任务名由框架写入；节点内引用的对话包按包名解析，
    /// 依赖对话注入（<c>OnInjectDialogs</c>）先写完包。
    /// </summary>
    public void OnInjectMissionNodes(List<MissionNodeData> nodes)
    {
        ResourceExManager.PrepareDatabaseInjection();
        nodes.AddRange(MissionNodeRegistry.Configs.Select(config => config.ToData()));
    }

    /// <summary>
    /// 事件节点：节点表与映射由框架写入；事件播放只表达对话包（Timeline 资源留在模组侧，见迁移缺口）。
    /// </summary>
    public void OnInjectEventNodes(List<EventNodeData> nodes)
    {
        ResourceExManager.PrepareDatabaseInjection();
        nodes.AddRange(EventNodeRegistry.Configs.Select(config => config.ToData()));
    }

    public void OnInjectBuffs(List<BuffData> buffs)
    {
        ResourceExManager.PrepareDatabaseInjection();
        buffs.AddRange(BuffRegistry.Configs.Select(config => config.ToData()));
    }

    /// <summary>
    /// 商人：框架负责写商人表、建立运行时追踪记录、按游戏原逻辑过滤已拥有食谱，并对未知键返回空商人。
    /// 本模组只交出配置（含迎宾／空手对话包名与商品表）。
    /// </summary>
    public void OnInjectMerchants(List<MerchantData> merchants)
    {
        ResourceExManager.PrepareDatabaseInjection();
        merchants.AddRange(MerchantRegistry.Configs.Select(config => config.ToData()));
    }

    /// <summary>
    /// 白天地图：先把地图本体（Tilemap／相机／高度图／内存 GameObject）构建并登记进运行时 Addressables，
    /// 再把数据面交给框架（mapData／刷新点与采集点标签／地图语言／映射）。
    /// 校验失败的地图不会构建，也不会进入数据面。
    /// </summary>
    public void OnInjectDayMaps(List<DayMapData> maps)
    {
        ResourceExManager.PrepareDatabaseInjection();
        DayMapRegistry.BuildAll();
        maps.AddRange(DayMapRegistry.BuiltConfigs.Select(config => config.ToData()));
    }
}
