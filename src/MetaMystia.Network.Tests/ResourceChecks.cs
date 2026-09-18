using MetaMystia;
using MetaMystia.Network;

static partial class Checks
{
    static void ResourceTables()
    {
        var resources = new ResourceDataBase
        {
            DlcFlags = DlcPack.Core | DlcPack.Dlc1, PackIds = ["shared.pack"],
            Foods = [6001], Recipes = [6002], Beverages = [6003], Ingredients = [6004],
            Cookers = [6005], Items = [6006], Izakayas = [6007], SpecialGuests = [6008], NormalGuests = [6009]
        };
        var player = Protocol.Read<Player>(Protocol.Pack(Player("resources", resources)));
        var received = player.Resources!;
        Protocol.Validate(player, true);
        Assert(received.DlcFlags == resources.DlcFlags && received.PackIds.SequenceEqual(resources.PackIds)
            && received.Foods.SequenceEqual(resources.Foods) && received.Recipes.SequenceEqual(resources.Recipes)
            && received.Beverages.SequenceEqual(resources.Beverages) && received.Ingredients.SequenceEqual(resources.Ingredients)
            && received.Cookers.SequenceEqual(resources.Cookers) && received.Items.SequenceEqual(resources.Items)
            && received.Izakayas.SequenceEqual(resources.Izakayas) && received.SpecialGuests.SequenceEqual(resources.SpecialGuests)
            && received.NormalGuests.SequenceEqual(resources.NormalGuests), "共用资源表通过玩家消息完整往返，九类资源不串位");

        var copy = resources.Copy();
        copy.PackIds[0] = "changed";
        copy.Clear();
        Assert(resources.PackIds[0] == "shared.pack" && resources.Foods.Count == 1 && resources.Recipes.Count == 1
            && resources.Beverages.Count == 1 && resources.Ingredients.Count == 1 && resources.Cookers.Count == 1
            && resources.Items.Count == 1 && resources.Izakayas.Count == 1
            && resources.SpecialGuests.Count == 1 && resources.NormalGuests.Count == 1,
            "资源表副本与源表的所有可变集合隔离");

        player.Resources!.Foods = null!;
        bool rejected = false;
        try { Protocol.Validate(player, true); }
        catch (NetworkException e) { rejected = e.Code == NetworkErrorCode.ResourcesNotReadyOrInvalid; }
        Assert(rejected, "共用资源表缺少分类列表时拒绝入房资料");
        Assert(!new ResourceDataBase().IsIncrementalReady && received.IsIncrementalReady,
            "资源就绪直接从 DLC 标识计算");
    }
}
