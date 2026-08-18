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
[RoomRelay]
public partial class BuffAction : Action
{
    public QTEBuff Buff;
    protected override BepInEx.Logging.LogLevel OnReceiveLogLevel => BepInEx.Logging.LogLevel.Message;
    protected override BepInEx.Logging.LogLevel OnSendLogLevel => BepInEx.Logging.LogLevel.Message;

    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        CommandScheduler.Enqueue(
            executeWhen: () => !QTERewardManagerPatch.OnQTESucceededExecuting,
            executeInfo: "BuffAction OnQTESucceededExecuting",
            execute: () =>
            {
                QTERewardManagerPatch.BuffLocalTrigger = false; // 标记为非本地触发
                QTERewardManagerPatch.OnQTESucceeded(NightScene.CookingUtility.QTERewardManager.Instance, Buff.ID, true);
                QTERewardManagerPatch.BuffLocalTrigger = true;
                Log.Message($"triggered buff {Buff}");
            },
            timeoutSeconds: 10f
        );
    }

    public static void Send(QTEBuff buff)
    {
        new BuffAction { Buff = buff }.Enqueue();
    }
}
