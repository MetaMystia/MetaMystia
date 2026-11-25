using MemoryPack;

using MetaMystia.Multiplayer.Spells;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>主机：一张符卡开始执行（出队、宣言之前）。客机把同一张卡排入本地符卡队列。</summary>
[MemoryPackable]
public partial class SpellBeginMessage : MultiplayerMessage
{
    public int RunId { get; set; }
    public int RuntimeId { get; set; }
    public int? SpellOwner { get; set; }
    public SpellKind Kind { get; set; }
    public bool RecordSpellNum { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var run = new SpellRun
        {
            RunId = RunId,
            RuntimeId = RuntimeId,
            SpellOwner = SpellOwner,
            Kind = Kind,
            RecordSpellNum = RecordSpellNum,
            Replica = true,
        };
        SpellRegistry.Current.Add(run);
        run.Enqueue();
    }

    public static void Send(SpellRun run) => new SpellBeginMessage
    {
        RunId = run.RunId,
        RuntimeId = run.RuntimeId,
        SpellOwner = run.SpellOwner,
        Kind = run.Kind,
        RecordSpellNum = run.RecordSpellNum,
    }.Enqueue();
}
