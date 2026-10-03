using Mystia.Listeners;
using Mystia.Scenes;

using NightScene.EventUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;

namespace MetaMystia.Listeners;

/// <summary>
/// 营业场景金钱/小费/经验/热情编辑的通知与同步（原 <c>NightSceneEventManagerPatch</c> 的
/// Fund/Tip/Exp/Passion 前缀与后缀）。客机拦下原版编辑，主机在编辑后广播编辑量与运算类型；
/// 远端编辑由消息排到营业场景循环内用 <see cref="IWorkSceneEconomyServices"/> 重放。
/// </summary>
/// <remarks>
/// 后缀只带结算后的数值，运算类型必须由紧随其前的前缀记住；各数值各自记一份，避免一条编辑内部再触发
/// 另一条同族编辑时相互覆盖。
/// </remarks>
[AutoLog]
public sealed partial class MetricsSync : IWorkMetricsListener
{
    /// <summary>本模组正在重放远端编辑：此时不拦下，也不回声。</summary>
    private static bool s_applyingRemote;

    private static EventManager.MathOperation s_fundOperation;
    private static EventManager.MathOperation s_experienceOperation;
    private static EventManager.MathOperation s_passionOperation;

    private static bool SyncActive => !GameFlow.ShouldSkipAction && GameSession.HasRoomPeers;

    public void OnPreFundEdit(ref float value, ref EventManager.MathOperation operation, ref bool cancelInvocation)
    {
        s_fundOperation = operation;
        if (s_applyingRemote || !SyncActive || !GameSession.IsRoomClient) return;

        cancelInvocation = true;
    }

    public void OnFundEdited(float value)
    {
        if (!SyncActive || !GameSession.IsRoomHost) return;

        FundEditMessage.Send(value, s_fundOperation);
    }

    public void OnPreTipEdit(
        ref int value,
        ref EventManager.ServeType serveType,
        ref float comboBuff,
        ref float moodBuff,
        ref float extraBuff,
        ref bool cancelInvocation)
    {
        if (s_applyingRemote || !SyncActive || !GameSession.IsRoomClient) return;

        cancelInvocation = true;
    }

    public void OnTipEdited(int value, EventManager.ServeType serveType, float comboBuff, float moodBuff, float extraBuff)
    {
        if (!SyncActive || !GameSession.IsRoomHost) return;

        TipEditMessage.Send(value, serveType, comboBuff, moodBuff, extraBuff);
    }

    public void OnPreExperienceEdit(ref float value, ref EventManager.MathOperation operation, ref bool cancelInvocation)
    {
        s_experienceOperation = operation;
        if (s_applyingRemote || !SyncActive || !GameSession.IsRoomClient) return;

        cancelInvocation = true;
    }

    public void OnExperienceEdited(float value)
    {
        if (!SyncActive || !GameSession.IsRoomHost) return;

        ExpEditMessage.Send(value, s_experienceOperation);
    }

    public void OnPrePassionEdit(ref float value, ref EventManager.MathOperation operation, ref bool cancelInvocation)
    {
        s_passionOperation = operation;
        if (s_applyingRemote || !SyncActive || !GameSession.IsRoomClient) return;

        cancelInvocation = true;
    }

    public void OnPassionEdited(float value)
    {
        if (!SyncActive || !GameSession.IsRoomHost) return;

        PassionEditMessage.Send(value, s_passionOperation);
    }

    #region 远端编辑重放

    internal static void ReplayFund(IWorkSceneServices services, float value, EventManager.MathOperation operation)
        => Replay(() => services.Economy.EditFund(value, operation));

    internal static void ReplayTip(
        IWorkSceneServices services,
        int value,
        EventManager.ServeType serveType,
        float comboBuff,
        float moodBuff,
        float extraBuff)
        => Replay(() => services.Economy.EditTip(value, serveType, comboBuff, moodBuff, extraBuff));

    internal static void ReplayExperience(IWorkSceneServices services, float value, EventManager.MathOperation operation)
        => Replay(() => services.Economy.EditExperience(value, operation));

    internal static void ReplayPassion(IWorkSceneServices services, float value, EventManager.MathOperation operation)
        => Replay(() => services.Economy.EditPassion(value, operation));

    private static void Replay(System.Action apply)
    {
        s_applyingRemote = true;
        try
        {
            apply();
        }
        finally
        {
            s_applyingRemote = false;
        }
    }

    #endregion
}
