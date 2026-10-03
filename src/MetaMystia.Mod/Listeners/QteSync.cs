using GameData.Profile;

using Mystia.Listeners;
using Mystia.Scenes;

using MetaMystia.Multiplayer.Messages;

namespace MetaMystia.Listeners;

/// <summary>
/// QTE 奖励与奖励 buff（原 <c>QTERewardManagerPatch</c> 与 <c>BuffPatch</c>）。
/// 本地的 QTE 结算先拦下，再由营业场景循环用 <see cref="IQteServices"/> 重放（原主线程调度 + 反向补丁）；
/// 奖励 buff 触发时广播给对端，重放期间不回声（原 <c>BuffLocalTrigger</c> 标记位）。
/// </summary>
[AutoLog]
public sealed partial class QteSync : IQteListener
{
    /// <summary>本模组正在重放 QTE 结算：重放里的再次结算不再拦下。</summary>
    private static bool s_replayingReward;

    /// <summary>本模组正在重放或直接触发奖励 buff：不再广播给对端。</summary>
    private static bool s_suppressBuffBroadcast;

    public void OnPreQteSucceeded(ref QteReward reward, ref bool cancelInvocation)
    {
        if (s_replayingReward) return;

        Log.Debug($"OnQTESucceeded, index {reward.Index}, mustSuccess {reward.MustSuccess}");
        cancelInvocation = true;
        // Buff 由重放侧自行判定（桥接只在可判定时填充，模组不依赖）；这里只重放槽位。
        var replay = new QteReward(reward.Index, reward.MustSuccess, null);
        GuestSync.EnqueueReplay("qte reward", services => ReplayReward(services, replay));
    }

    /// <summary>原 <c>QTERewardManagerPatch.OnQTESucceeded</c>（主线程调度 + 反向补丁）的重放侧。</summary>
    internal static void ReplayReward(IWorkSceneServices services, QteReward reward)
    {
        s_replayingReward = true;
        try
        {
            services.Qte.ApplyQteReward(reward);
        }
        finally
        {
            s_replayingReward = false;
        }
    }

    /// <summary>
    /// 原 <c>BuffPatch</c> 五个前缀（及 <c>NightSceneEventManagerPatch.Fever_Prefix</c>）的广播侧。
    /// 本地触发会随奖励结算一路走到这里；重放与作弊直接触发被挂起，避免回声。
    /// </summary>
    public void OnRewardBuffTriggered(RewardBuffKind kind)
    {
        if (s_suppressBuffBroadcast) return;

        BuffMessage.Send(ToBuff(kind));
    }

    /// <summary>远端 buff 重放（原 <c>BuffMessage</c> 里把 <c>BuffLocalTrigger</c> 置假的调用）。</summary>
    internal static void ReplayBuff(IWorkSceneServices services, RewardBuffKind kind)
    {
        s_suppressBuffBroadcast = true;
        try
        {
            services.Qte.TriggerRewardBuff(kind);
        }
        finally
        {
            s_suppressBuffBroadcast = false;
        }
    }

    /// <summary>
    /// 调试/作弊直接触发永续热火朝天：不广播，等价原反向补丁的直调（<c>MystiaQTEBuffReward.Player_Fever_Infinite</c> 是唯一公开入口）。
    /// </summary>
    internal static void TriggerInfiniteFeverLocally(MystiaQTEBuffReward reward)
    {
        if (reward == null) return;

        s_suppressBuffBroadcast = true;
        try
        {
            reward.Player_Fever_Infinite();
        }
        finally
        {
            s_suppressBuffBroadcast = false;
        }
    }

    private static QTEBuff ToBuff(RewardBuffKind kind) => kind switch
    {
        RewardBuffKind.ThrowDeliver => QTEBuff.ThrowDeliver,
        RewardBuffKind.InstantEvaluation => QTEBuff.InstantEvaluation,
        RewardBuffKind.PatientFreeze => QTEBuff.PatientFreeze,
        RewardBuffKind.InfiniteFever => QTEBuff.Fever_Infinite,
        _ => QTEBuff.Fever,
    };
}
