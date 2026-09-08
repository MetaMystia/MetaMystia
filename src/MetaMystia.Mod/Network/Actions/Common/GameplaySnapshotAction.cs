using System;

using MemoryPack;

namespace MetaMystia.Network;

public enum GameplayPhase { None, Day, Selection, Prep, Night }

[MemoryPackable]
public sealed partial record PlayerSelection(int Uid, MapLabel Map, int Level);

[MemoryPackable]
public sealed partial class GameplaySnapshotAction : Action
{
    public long Epoch { get; set; }
    public long Revision { get; set; }
    public GameplayPhase Phase { get; set; }
    public bool Locked { get; set; }
    public int[] Participants { get; set; } = Array.Empty<int>();
    public int[] Ready { get; set; } = Array.Empty<int>();
    public PlayerSelection[] Selections { get; set; } = Array.Empty<PlayerSelection>();
    public UpdatePrepAction.Table PrepTable { get; set; }

    public override void OnReceivedDerived() => RoomGameplay.ApplySnapshot(this);
    internal void Publish() => Enqueue();
}
