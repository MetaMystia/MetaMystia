using System.Collections.Generic;

using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>房主下发的料理对决统一库存（食材、酒水 ID → 数量，负数为无限）。</summary>
[MemoryPackable]
public partial class VersusStockMessage : MultiplayerMessage
{
    public Dictionary<int, int> Ingredients { get; set; } = new();
    public Dictionary<int, int> Beverages { get; set; } = new();

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => VersusMatch.ReceiveStock(this);

    public void Send() => Enqueue();
}
