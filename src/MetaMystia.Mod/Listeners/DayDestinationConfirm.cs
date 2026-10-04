using Mystia.Listeners;

using MetaMystia.Multiplayer;

namespace MetaMystia.Listeners;

/// <summary>
/// 幽幽子挑战的确认回调。联机时这次确认不直接进入挑战，而是先提交当日去向（重打最终试炼），
/// 由主机确认后再调用框架交还的原版回调；回调里的排期事件与挑战会话启动因此只执行一次，也不会留下
/// 尚未进入的挑战事件。联机外与重放期间不拦截，放行原版。
/// </summary>
[AutoLog]
public sealed partial class DayDestinationConfirm : IChatConfirmationListener
{
    void IChatConfirmationListener.OnPreChatConfirmation(ChatConfirmationView confirmation, ref bool cancelInvocation)
    {
        if (confirmation.Kind != ChatConfirmationKind.YuyukoChallenge) return;
        if (!confirmation.Confirmed || !GameSession.IsInRoom || DayDestinationManager.ReplayingChallenge) return;
        cancelInvocation = true;
        DayDestinationManager.Submit(DayDestination.FinalTrialAgain, confirmation.Confirm);
    }
}
