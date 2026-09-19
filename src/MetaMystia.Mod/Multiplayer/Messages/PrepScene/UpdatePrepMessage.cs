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

    public void Submit()
    {
        if (!PrepSceneManager.CanSubmitEdits) return;
        PrepRound = PrepSceneManager.IsYuyukoChallenge
            ? PrepSceneManager.YuyukoPrepRound + (PrepSceneManager.IsOpeningPanel ? 1 : 0) : 0;
        // 本机原注册方法尚未返回，只更新主机记录，不在这里回写游戏配置。
        if (GameSession.IsRoomHost) PrepSceneManager.ReceivePrepUpdate(this, false);
        else Enqueue();
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
