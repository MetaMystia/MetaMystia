using System.Collections.Generic;
using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 所有玩家：通告 PrepScene 的食谱/酒水/厨具变更，使用 Last-Write-Wins 策略合并数据，所有玩家对等
/// </summary>
[MemoryPackable]
[AutoLog]
[RoomRelay]
public partial class UpdatePrepAction : Action
{

    [MemoryPackable]
    public partial class Table
    {
        public Dictionary<int, long> RecipeAdditions { get; set; } = [];
        public Dictionary<int, long> RecipeDeletions { get; set; } = [];

        public Dictionary<int, long> BeverageAdditions { get; set; } = [];
        public Dictionary<int, long> BeverageDeletions { get; set; } = [];

        public CookerSlot[] Cookers { get; set; } = CookerSlot.CreateDefaultArray();

        public Table Clone()
        {
            var cookers = Cookers ?? CookerSlot.CreateDefaultArray();
            var clonedCookers = new CookerSlot[cookers.Length];
            for (int i = 0; i < cookers.Length; i++)
                clonedCookers[i] = cookers[i]?.Clone() ?? new CookerSlot();

            return new Table
            {
                RecipeAdditions = new Dictionary<int, long>(RecipeAdditions),
                RecipeDeletions = new Dictionary<int, long>(RecipeDeletions),
                BeverageAdditions = new Dictionary<int, long>(BeverageAdditions),
                BeverageDeletions = new Dictionary<int, long>(BeverageDeletions),
                Cookers = clonedCookers,
            };
        }
    }

    public Table PrepTable { get; set; } = new Table();

    protected override bool OnSendLogOnlyAction => true;
    protected override bool OnReceiveLogOnlyAction => true;

    public override void OnReceivedDerived()
    {
        switch (MpManager.LocalScene)
        {
            case Common.UI.Scene.IzakayaPrepScene:
                PrepSceneManager.MergeFromPeer(PrepTable);
                break;
            case Common.UI.Scene.DayScene:
                // Day→Prep 转场窗口期缓存，进入 PrepScene 后由 PrepSceneManager.FlushBufferedTables 重放
                PrepSceneManager.BufferPrepTable(PrepTable);
                break;
            default:
                Log.LogInfo($"Discarded UpdatePrepAction in {MpManager.LocalScene}");
                break;
        }
    }

    public static void Send(Table prepTable) =>
        new UpdatePrepAction { PrepTable = prepTable }.Enqueue();
}
