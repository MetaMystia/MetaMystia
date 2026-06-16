using System.Collections.Generic;
using System.Linq;

using GameData.Core.Collections;
using GameData.Core.Collections.CharacterUtility;

using MemoryPack;
using SgrYuki.Utils;

using MetaMystia.Protocol.Data;
using MetaMystia.Protocol.Enums;

namespace MetaMystia;

// 依赖: DataBaseCore, DataBaseCharacter, ResourceEx

[MemoryPackable]
[AutoLog]
public partial class ResourceDataBase
{
    /// <summary>
    /// DLC 标识位：None = 全量模式（向后兼容），非 None = 增量模式（Lists 仅含 ResourceEx extras）
    /// </summary>
    public DlcPack DlcFlags { get; set; } = DlcPack.None;

    /// <summary>增量表已算出至少一个完整 DLC（None 表示资源尚未加载或列表为空）。</summary>
    public bool IsIncrementalReady => DlcFlags != DlcPack.None;

    // from DataBaseCore
    public List<int> Foods { get; private set; } = [];
    public List<int> Recipes { get; private set; } = [];
    public List<int> Beverages { get; private set; } = [];
    public List<int> Ingredients { get; private set; } = [];
    public List<int> Cookers { get; private set; } = [];
    public List<int> Items { get; private set; } = [];
    public List<int> Izakayas { get; private set; } = [];

    // from DataBaseCharacter
    public List<int> SpecialGuests { get; private set; } = [];
    public List<int> NormalGuests { get; private set; } = [];

    public ResourceDataBase LoadResourceIds()
    {
        Foods = DataBaseCore.Foods.ToList().Select(f => f.Key).ToList();
        Recipes = DataBaseCore.Recipes.ToList().Select(r => r.Key).ToList();
        Beverages = DataBaseCore.Beverages.ToList().Select(b => b.Key).ToList();
        Ingredients = DataBaseCore.Ingredients.ToList().Select(i => i.Key).ToList();
        Cookers = DataBaseCore.Cookers.ToList().Select(c => c.Key).ToList();
        Items = DataBaseCore.Items.ToList().Select(i => i.Key).ToList();
        Izakayas = DataBaseCore.Izakayas.ToList().Select(i => i.Key).ToList();

        SpecialGuests = DataBaseCharacter.SpecialGuest.ToList().Select(s => s.Key).ToList();
        NormalGuests = DataBaseCharacter.NormalGuest.ToList().Select(n => n.Key).ToList();

        return this;
    }

    #region 单实例可用性判断

    public bool FoodAvailable(int id) => Foods.Contains(id);
    public bool RecipeAvailable(int id) => Recipes.Contains(id);
    public bool BeverageAvailable(int id) => Beverages.Contains(id);
    public bool IngredientAvailable(int id) => Ingredients.Contains(id);
    public bool CookerAvailable(int id) => Cookers.Contains(id);
    public bool ItemAvailable(int id) => Items.Contains(id);
    public bool IzakayaAvailable(int id) => Izakayas.Contains(id);
    public bool NormalGuestAvailable(int id) => NormalGuests.Contains(id);
    public bool SpecialGuestAvailable(int id) => SpecialGuests.Contains(id);

    #endregion

    public void Clear()
    {
        Foods.Clear();
        Recipes.Clear();
        Beverages.Clear();
        Ingredients.Clear();
        Cookers.Clear();
        Items.Clear();
        Izakayas.Clear();

        SpecialGuests.Clear();
        NormalGuests.Clear();
    }

    public void LogDataBase()
    {
        Log.Warning($"Foods: {string.Join(", ", Foods)}");
        Log.Warning($"Recipes: {string.Join(", ", Recipes)}");
        Log.Warning($"Beverages: {string.Join(", ", Beverages)}");
        Log.Warning($"Ingredients: {string.Join(", ", Ingredients)}");
        Log.Warning($"Cookers: {string.Join(", ", Cookers)}");
        Log.Warning($"Items: {string.Join(", ", Items)}");
        Log.Warning($"Izakayas: {string.Join(", ", Izakayas)}");

        Log.Warning($"SpecialGuests: {string.Join(", ", SpecialGuests)}");
        Log.Warning($"NormalGuests: {string.Join(", ", NormalGuests)}");
    }

    #region 增量传输

    /// <summary>
    /// 将全量数据库压缩为增量格式。
    /// 逐 DLC 检查：只有当该 DLC 所有分类的标准数据都是玩家数据的子集时才设 flag。
    /// 未被 flag 覆盖的 ID（不完整 DLC + ResourceEx）保留在 extras 中。
    /// </summary>
    public ResourceDataBase ToIncremental()
    {
        var flags = ComputeDlcFlags();
        return new ResourceDataBase
        {
            DlcFlags = flags,
            Foods = FilterExtras(Foods, flags),
            Recipes = FilterExtras(Recipes, flags),
            Beverages = FilterExtras(Beverages, flags),
            Ingredients = FilterExtras(Ingredients, flags),
            Cookers = FilterExtras(Cookers, flags),
            Items = FilterExtras(Items, flags),
            Izakayas = FilterExtras(Izakayas, flags),
            SpecialGuests = FilterExtras(SpecialGuests, flags),
            NormalGuests = FilterExtras(NormalGuests, flags),
        };
    }

    /// <summary>
    /// 将增量数据库展开为全量：按 flags 从标准分表重建 + extras
    /// </summary>
    public static ResourceDataBase Expand(ResourceDataBase incremental)
    {
        if (incremental.DlcFlags == DlcPack.None) return incremental;

        var flags = incremental.DlcFlags;
        var db = new ResourceDataBase { DlcFlags = DlcPack.None };

        db.Foods = ExpandCategory(ResourceCategory.Foods, flags, incremental.Foods);
        db.Recipes = ExpandCategory(ResourceCategory.Recipes, flags, incremental.Recipes);
        db.Beverages = ExpandCategory(ResourceCategory.Beverages, flags, incremental.Beverages);
        db.Ingredients = ExpandCategory(ResourceCategory.Ingredients, flags, incremental.Ingredients);
        db.Cookers = ExpandCategory(ResourceCategory.Cookers, flags, incremental.Cookers);
        db.Items = ExpandCategory(ResourceCategory.Items, flags, incremental.Items);
        db.Izakayas = ExpandCategory(ResourceCategory.Izakayas, flags, incremental.Izakayas);
        db.SpecialGuests = ExpandCategory(ResourceCategory.SpecialGuests, flags, incremental.SpecialGuests);
        db.NormalGuests = ExpandCategory(ResourceCategory.NormalGuests, flags, incremental.NormalGuests);

        return db;
    }

    /// <summary>
    /// 逐 DLC 检查子集关系：只有所有 9 个分类的标准数据都完整包含在玩家数据中时才设 flag
    /// </summary>
    private DlcPack ComputeDlcFlags()
    {
        var flags = DlcPack.None;
        foreach (var dlc in DlcStandardTable.AllDlcs)
        {
            if (HasCompleteDlc(dlc))
                flags |= dlc;
        }
        return flags;
    }

    private bool HasCompleteDlc(DlcPack dlc)
    {
        return IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.Foods), Foods) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.Recipes), Recipes) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.Beverages), Beverages) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.Ingredients), Ingredients) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.Cookers), Cookers) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.Items), Items) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.Izakayas), Izakayas) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.SpecialGuests), SpecialGuests) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.NormalGuests), NormalGuests);
    }

    private static bool IsSubset(int[] standard, List<int> actual)
    {
        if (standard.Length == 0) return true;
        var set = new HashSet<int>(actual);
        foreach (var id in standard)
            if (!set.Contains(id))
                return false;
        return true;
    }

    /// <summary>
    /// 保留不被任何已 flag DLC 覆盖的 ID（不完整 DLC 的 ID + ResourceEx ID）
    /// </summary>
    private static List<int> FilterExtras(List<int> ids, DlcPack flags)
    {
        return ids.Where(id => (DlcStandardTable.IdToDlc(id) & flags) == 0).ToList();
    }

    private static List<int> ExpandCategory(ResourceCategory cat, DlcPack flags, List<int> extras)
    {
        var result = new List<int>();
        foreach (var dlc in DlcStandardTable.AllDlcs)
        {
            if ((dlc & flags) != 0)
                result.AddRange(DlcStandardTable.Get(dlc, cat));
        }
        result.AddRange(extras);
        return result;
    }

    #region Protocol 转换

    public ResourceDatabaseData ToDatabaseData()
    {
        return new ResourceDatabaseData
        {
            DLCFlags = (DLCPack)(int)DlcFlags,
            Foods = Foods,
            Recipes = Recipes,
            Beverages = Beverages,
            Ingredients = Ingredients,
            Cookers = Cookers,
            Items = Items,
            Izakayas = Izakayas,
            SpecialGuests = SpecialGuests,
            NormalGuests = NormalGuests
        };
    }

    public static ResourceDataBase FromDatabaseData(ResourceDatabaseData data)
    {
        return new ResourceDataBase
        {
            DlcFlags = (DlcPack)(int)data.DLCFlags,
            Foods = data.Foods,
            Recipes = data.Recipes,
            Beverages = data.Beverages,
            Ingredients = data.Ingredients,
            Cookers = data.Cookers,
            Items = data.Items,
            Izakayas = data.Izakayas,
            SpecialGuests = data.SpecialGuests,
            NormalGuests = data.NormalGuests
        };
    }

    #endregion

    #endregion
}
