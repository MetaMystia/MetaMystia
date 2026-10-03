using MemoryPack;

using Mystia.Listeners;

using MetaMystia.Listeners;

namespace MetaMystia.Multiplayer.Messages;

public enum QTEBuff
{
    InstantEvaluation, // 立即完食
    PatientFreeze, // 耐心不减
    ThrowDeliver,   // 投掷上菜

    Fever,      // 热火朝天
    Fever_Infinite  // 永续热火朝天
}

/// <summary>
/// 任何玩家 → 全体玩家：通告触发 QTE Buff
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class BuffMessage : MultiplayerMessage
{
    public QTEBuff Buff;
    protected override Mystia.LogLevel OnReceiveLogLevel => Mystia.LogLevel.Message;
    protected override Mystia.LogLevel OnSendLogLevel => Mystia.LogLevel.Message;

    protected override Common.UI.Scene? ReceiveScene => Common.UI.Scene.WorkScene;

    public override void OnReceivedDerived()
    {
        // 服务只在营业场景循环作用域内可用，奖励触发排到 GuestSync 的重放队列里执行。
        var kind = ToReward(Buff);
        GuestSync.EnqueueReplay($"buff {Buff}", services => QteSync.ReplayBuff(services, kind));
        Log.Message($"triggered buff {Buff}");
    }

    public static void Send(QTEBuff buff)
    {
        new BuffMessage { Buff = buff }.Enqueue();
    }

    private static RewardBuffKind ToReward(QTEBuff buff) => buff switch
    {
        QTEBuff.ThrowDeliver => RewardBuffKind.ThrowDeliver,
        QTEBuff.InstantEvaluation => RewardBuffKind.InstantEvaluation,
        QTEBuff.PatientFreeze => RewardBuffKind.PatientFreeze,
        QTEBuff.Fever_Infinite => RewardBuffKind.InfiniteFever,
        _ => RewardBuffKind.Fever,
    };
}
