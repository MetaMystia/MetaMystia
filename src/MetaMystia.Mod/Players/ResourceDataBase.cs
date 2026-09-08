using System.Collections.Generic;
using System.Linq;

namespace MetaMystia;

// 本地目录保留全部 ID；远端目录只由完整资源清单创建，不按 DLC 推导。
public sealed class ResourceDataBase
{
    private readonly HashSet<int>[] _categories;
    public bool IsLoaded { get; }
    public static ResourceDataBase Empty { get; } = new(Enumerable.Range(0, 9).Select(_ => new HashSet<int>()).ToArray(), false);

    private ResourceDataBase(HashSet<int>[] categories, bool loaded)
    {
        _categories = categories;
        IsLoaded = loaded;
    }

    public static ResourceDataBase FromLocal(IEnumerable<int>[] categories)
    {
        var sets = categories.Select(ids => ids.ToHashSet()).ToArray();
        return new(sets, sets[0].Count > 0 && sets[1].Count > 0);
    }

    public bool FoodAvailable(int id) => _categories[0].Contains(id);
    public bool RecipeAvailable(int id) => _categories[1].Contains(id);
    public bool BeverageAvailable(int id) => _categories[2].Contains(id);
    public bool IngredientAvailable(int id) => _categories[3].Contains(id);
    public bool CookerAvailable(int id) => _categories[4].Contains(id);
    public bool ItemAvailable(int id) => _categories[5].Contains(id);
    public bool IzakayaAvailable(int id) => _categories[6].Contains(id);
    public bool SpecialGuestAvailable(int id) => _categories[7].Contains(id);
    public bool NormalGuestAvailable(int id) => _categories[8].Contains(id);

    public ResourceManifest ToManifest() => new()
    {
        Categories = _categories.Select(ids => ids.Where(id => id >= 6000).OrderBy(id => id).ToArray()).ToArray()
    };

    public static bool ValidManifest(ResourceManifest manifest) => manifest?.Categories?.Length == 9
        && manifest.Categories.All(ids => ids != null && ids.Length <= 65_536 && ids.All(id => id >= 6000));

    public static ResourceDataBase FromManifest(ResourceManifest manifest) =>
        new(manifest.Categories.Select(ids => ids.ToHashSet()).ToArray(), true);
}
