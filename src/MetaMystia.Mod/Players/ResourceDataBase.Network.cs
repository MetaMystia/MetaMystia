using System.Linq;


using MetaMystia.Network;

namespace MetaMystia;

public partial class ResourceDataBase
{
    public Resources ToNetwork() => new()
    {
        Ready = IsIncrementalReady,
        DlcFlags = DlcFlags,
        PackIds = ResourceExManager.LoadedPackages.Select(p => p.PackageLabel).OrderBy(p => p).ToArray(),
        ExtraIds = [Foods.ToArray(), Recipes.ToArray(), Beverages.ToArray(), Ingredients.ToArray(), Cookers.ToArray(),
            Items.ToArray(), Izakayas.ToArray(), SpecialGuests.ToArray(), NormalGuests.ToArray()]
    };

    public static ResourceDataBase FromNetwork(Resources resources) => resources == null ? new() : new()
    {
        DlcFlags = resources.DlcFlags,
        Foods = resources.ExtraIds[0].ToList(), Recipes = resources.ExtraIds[1].ToList(),
        Beverages = resources.ExtraIds[2].ToList(), Ingredients = resources.ExtraIds[3].ToList(),
        Cookers = resources.ExtraIds[4].ToList(), Items = resources.ExtraIds[5].ToList(),
        Izakayas = resources.ExtraIds[6].ToList(), SpecialGuests = resources.ExtraIds[7].ToList(),
        NormalGuests = resources.ExtraIds[8].ToList()
    };
}
