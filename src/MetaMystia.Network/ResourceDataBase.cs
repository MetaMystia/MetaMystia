using System.Collections.Generic;

using MemoryPack;

namespace MetaMystia;

/// <summary>游戏端与网络端共用的玩家资源表。</summary>
[MemoryPackable]
public partial class ResourceDataBase
{
    /// <summary>None 表示全量，否则列表只保存未被完整 DLC 覆盖的资源。</summary>
    public DlcPack DlcFlags { get; set; }

    [MemoryPackIgnore]
    public bool IsIncrementalReady => DlcFlags != DlcPack.None;

    public string[] PackIds { get; set; } = [];
    public List<int> Foods { get; set; } = [];
    public List<int> Recipes { get; set; } = [];
    public List<int> Beverages { get; set; } = [];
    public List<int> Ingredients { get; set; } = [];
    public List<int> Cookers { get; set; } = [];
    public List<int> Items { get; set; } = [];
    public List<int> Izakayas { get; set; } = [];
    public List<int> SpecialGuests { get; set; } = [];
    public List<int> NormalGuests { get; set; } = [];

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
        DlcFlags = DlcPack.None;
        PackIds = [];
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
    public ResourceDataBase Copy() => new()
    {
        DlcFlags = DlcFlags,
        PackIds = [.. PackIds],
        Foods = [.. Foods],
        Recipes = [.. Recipes],
        Beverages = [.. Beverages],
        Ingredients = [.. Ingredients],
        Cookers = [.. Cookers],
        Items = [.. Items],
        Izakayas = [.. Izakayas],
        SpecialGuests = [.. SpecialGuests],
        NormalGuests = [.. NormalGuests],
    };
}
