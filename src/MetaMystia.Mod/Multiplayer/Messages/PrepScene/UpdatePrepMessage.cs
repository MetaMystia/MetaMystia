using System.Collections.Generic;
using System.Linq;

using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>客机提交备菜修改，主机按接收顺序处理并广播完整结果。</summary>
[MemoryPackable]
[AutoLog]
public partial class UpdatePrepMessage : MultiplayerMessage
{
    [MemoryPackable]
    public partial class Table
    {
        public List<int> Recipes { get; set; } = [];
        public List<int> Beverages { get; set; } = [];
        public CookerSlot[] Cookers { get; set; } = CookerSlot.CreateDefaultArray();

        public Table Clone() => new()
        {
            Recipes = new(Recipes),
            Beverages = new(Beverages),
            Cookers = Cookers.Select(slot => slot.Clone()).ToArray(),
        };
    }

    public Table PrepTable { get; set; }
    public int PrepRound { get; set; }
    public int[] AddedRecipes { get; set; } = [];
    public int[] RemovedRecipes { get; set; } = [];
    public int[] AddedBeverages { get; set; } = [];
    public int[] RemovedBeverages { get; set; } = [];
    public Dictionary<int, int> ChangedCookers { get; set; } = [];

    protected override bool OnSendLogOnlyMessage => true;
    protected override bool OnReceiveLogOnlyMessage => true;

    public override void OnReceivedDerived()
    {
        if (GameSession.IsRoomHost)
        {
            if (PrepTable != null) return;
        }
        else if (SenderUid != GameSession.Room?.Host || PrepTable == null) return;
        PrepSceneManager.ReceivePrepUpdate(this);
    }

    public static void Submit(Table before, Table after, bool preset)
    {
        var message = new UpdatePrepMessage
        {
            PrepRound = PrepSceneManager.IsYuyukoChallenge ? PrepSceneManager.YuyukoPrepRound : 0,
            AddedRecipes = preset ? after.Recipes.ToArray() : after.Recipes.Except(before.Recipes).ToArray(),
            RemovedRecipes = preset ? before.Recipes.ToArray() : before.Recipes.Except(after.Recipes).ToArray(),
            AddedBeverages = preset ? after.Beverages.ToArray() : after.Beverages.Except(before.Beverages).ToArray(),
            RemovedBeverages = preset ? before.Beverages.ToArray() : before.Beverages.Except(after.Beverages).ToArray(),
        };
        for (int i = 0; i < after.Cookers.Length; i++)
            if (before.Cookers[i].Id != after.Cookers[i].Id)
                message.ChangedCookers[i] = after.Cookers[i].Id;
        if (message.AddedRecipes.Length + message.RemovedRecipes.Length
            + message.AddedBeverages.Length + message.RemovedBeverages.Length
            + message.ChangedCookers.Count == 0) return;
        if (GameSession.IsRoomHost) PrepSceneManager.ReceivePrepUpdate(message);
        else message.Enqueue();
    }

    public static void Send(Table table) => new UpdatePrepMessage
    {
        PrepTable = table.Clone(),
        PrepRound = PrepSceneManager.IsYuyukoChallenge ? PrepSceneManager.YuyukoPrepRound : 0,
    }.Enqueue();

    public static void RequestState() => new UpdatePrepMessage
    {
        PrepRound = PrepSceneManager.IsYuyukoChallenge ? PrepSceneManager.YuyukoPrepRound : 0,
    }.Enqueue();
}
