using System.Collections.Generic;

using MemoryPack;

using MetaMystia.Multiplayer.Spells;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>主机：符卡正文第一步已执行，下发随机种子与按调用顺序选出的桌子、厨具。</summary>
[MemoryPackable]
public partial class SpellPlanMessage : MultiplayerMessage
{
    public int RunId { get; set; }
    public int Seed { get; set; }
    public int[][] Choices { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => SpellRegistry.Current.Find(RunId)?.Plan(Seed, Choices);

    public static void Send(int runId, int seed, List<int[]> choices) =>
        new SpellPlanMessage { RunId = runId, Seed = seed, Choices = choices.ToArray() }.Enqueue();
}
