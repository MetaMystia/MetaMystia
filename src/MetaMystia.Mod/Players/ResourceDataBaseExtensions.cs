using System.Collections.Generic;
using System.Linq;

using GameData.Core.Collections;
using GameData.Core.Collections.CharacterUtility;

using SgrYuki.Utils;

namespace MetaMystia;

[AutoLog]
public static partial class ResourceDataBaseExtensions
{
    public static ResourceDataBase LoadResourceIds(this ResourceDataBase resources)
    {
        resources.Foods = DataBaseCore.Foods.ToList().Select(f => f.Key).ToList();
        resources.Recipes = DataBaseCore.Recipes.ToList().Select(r => r.Key).ToList();
        resources.Beverages = DataBaseCore.Beverages.ToList().Select(b => b.Key).ToList();
        resources.Ingredients = DataBaseCore.Ingredients.ToList().Select(i => i.Key).ToList();
        resources.Cookers = DataBaseCore.Cookers.ToList().Select(c => c.Key).ToList();
        resources.Items = DataBaseCore.Items.ToList().Select(i => i.Key).ToList();
        resources.Izakayas = DataBaseCore.Izakayas.ToList().Select(i => i.Key).ToList();

        resources.SpecialGuests = DataBaseCharacter.SpecialGuest.ToList().Select(s => s.Key).ToList();
        resources.NormalGuests = DataBaseCharacter.NormalGuest.ToList().Select(n => n.Key).ToList();

        return resources;
    }

    public static void LogDataBase(this ResourceDataBase resources)
    {
        Log.Warning($"Foods: {string.Join(", ", resources.Foods)}");
        Log.Warning($"Recipes: {string.Join(", ", resources.Recipes)}");
        Log.Warning($"Beverages: {string.Join(", ", resources.Beverages)}");
        Log.Warning($"Ingredients: {string.Join(", ", resources.Ingredients)}");
        Log.Warning($"Cookers: {string.Join(", ", resources.Cookers)}");
        Log.Warning($"Items: {string.Join(", ", resources.Items)}");
        Log.Warning($"Izakayas: {string.Join(", ", resources.Izakayas)}");

        Log.Warning($"SpecialGuests: {string.Join(", ", resources.SpecialGuests)}");
        Log.Warning($"NormalGuests: {string.Join(", ", resources.NormalGuests)}");
    }

    #region 增量传输

    /// <summary>
    /// 将全量数据库压缩为增量格式。
    /// 逐 DLC 检查：只有当该 DLC 所有分类的标准数据都是玩家数据的子集时才设 flag。
    /// 未被 flag 覆盖的 ID（不完整 DLC + ResourceEx）保留在 extras 中。
    /// </summary>
    public static ResourceDataBase ToIncremental(this ResourceDataBase resources)
    {
        var flags = ComputeDlcFlags(resources);
        return new ResourceDataBase
        {
            DlcFlags = flags,
            PackIds = [.. resources.PackIds],
            Foods = FilterExtras(resources.Foods, flags),
            Recipes = FilterExtras(resources.Recipes, flags),
            Beverages = FilterExtras(resources.Beverages, flags),
            Ingredients = FilterExtras(resources.Ingredients, flags),
            Cookers = FilterExtras(resources.Cookers, flags),
            Items = FilterExtras(resources.Items, flags),
            Izakayas = FilterExtras(resources.Izakayas, flags),
            SpecialGuests = FilterExtras(resources.SpecialGuests, flags),
            NormalGuests = FilterExtras(resources.NormalGuests, flags),
        };
    }

    /// <summary>
    /// 将增量数据库展开为全量：按 flags 从标准分表重建 + extras
    /// </summary>
    public static ResourceDataBase Expand(this ResourceDataBase incremental)
    {
        if (incremental.DlcFlags == DlcPack.None) return incremental;

        var flags = incremental.DlcFlags;
        var db = new ResourceDataBase { DlcFlags = DlcPack.None, PackIds = [.. incremental.PackIds] };

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
    private static DlcPack ComputeDlcFlags(ResourceDataBase resources)
    {
        var flags = DlcPack.None;
        foreach (var dlc in DlcStandardTable.AllDlcs)
        {
            if (HasCompleteDlc(resources, dlc))
                flags |= dlc;
        }
        return flags;
    }

    private static bool HasCompleteDlc(ResourceDataBase resources, DlcPack dlc)
    {
        return IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.Foods), resources.Foods) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.Recipes), resources.Recipes) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.Beverages), resources.Beverages) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.Ingredients), resources.Ingredients) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.Cookers), resources.Cookers) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.Items), resources.Items) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.Izakayas), resources.Izakayas) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.SpecialGuests), resources.SpecialGuests) &&
               IsSubset(DlcStandardTable.Get(dlc, ResourceCategory.NormalGuests), resources.NormalGuests);
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

    #endregion
}
