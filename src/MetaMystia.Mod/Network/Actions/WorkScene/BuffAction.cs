using MemoryPack;

using MetaMystia.Patch;
using SgrYuki;

namespace MetaMystia.Network;

public enum QTEBuff
{
    InstantEvaluation, // 立即完食
    PatientFreeze, // 耐心不减
    ThrowDeliver,   // 投掷上菜

    Fever,      // 热火朝天
    Fever_Infinite  // 永续热火朝天
}

public static class QTEBuffExtension
{
    extension(QTEBuff buff)
    {
        public int ID => buff switch
        {
            QTEBuff.InstantEvaluation => 0,
            QTEBuff.PatientFreeze => 1,
            QTEBuff.ThrowDeliver => 2,

            QTEBuff.Fever => 3,
            QTEBuff.Fever_Infinite => -1,
            _ => 3,
        };
    }
}

/// <summary>
/// 任何玩家 → 全体玩家：通告触发 QTE Buff
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class BuffAction : Action
{
    public QTEBuff Buff;
    protected override BepInEx.Logging.LogLevel OnReceiveLogLevel => BepInEx.Logging.LogLevel.Message;
    protected override BepInEx.Logging.LogLevel OnSendLogLevel => BepInEx.Logging.LogLevel.Message;

    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        RoomGameplay.Run(ApplyBuff());
    }

    private System.Collections.IEnumerator ApplyBuff()
    {
        long deadline = MpWire.NowMs + 10_000;
        while (QTERewardManagerPatch.OnQTESucceededExecuting)
        {
            if (MpWire.NowMs >= deadline) { RoomGameplay.Abort("等待 QTE 奖励超时"); yield break; }
            yield return null;
        }
        var manager = NightScene.CookingUtility.QTERewardManager.Instance;
        if (manager == null) { RoomGameplay.Abort("QTE 奖励对象已失效"); yield break; }
        QTERewardManagerPatch.BuffLocalTrigger = false;
        QTERewardManagerPatch.OnQTESucceeded(manager, Buff.ID, true);
        QTERewardManagerPatch.BuffLocalTrigger = true;
    }

    public static void Send(QTEBuff buff)
    {
        new BuffAction { Buff = buff }.Enqueue();
    }
}
