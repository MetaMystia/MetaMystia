using MemoryPack;

using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 主机已进入驱赶清理；客机不重跑玩家赶客的判定与付款，只重放原版驱逐清理。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class GuestRepellMessage : MultiplayerMessage
{
    public int RuntimeId { get; set; }
    public GuestGroupController.LeaveType LeaveType { get; set; }
    public bool TriggerLeaveBuff { get; set; }
    public int Mood { get; set; }
    public int Combo { get; set; }
    public int LoseComboTimes { get; set; }
    public int LoseComboTimeForPassion { get; set; }
    public int LoseComboGuestSetNum { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var fsm = GuestsMap.GetGuestFsm(RuntimeId);
        if (fsm == null) return;
        // 驱逐清理走被关掉的离场接口，排队到营业场景循环的服务作用域内重放。
        QueueForGuest(fsm, nameof(GuestFSM.DoRepell), () =>
        {
            GuestFSM.DoRepell(this);
            return true;
        });
    }

    public static void Send(int runtimeId, GuestGroupController controller, GuestGroupController.LeaveType leaveType, bool triggerLeaveBuff)
        => new GuestRepellMessage
        {
            RuntimeId = runtimeId,
            LeaveType = leaveType,
            TriggerLeaveBuff = triggerLeaveBuff,
            Mood = controller.Mood,
            Combo = EventManager.Instance.CurrentCombo,
            LoseComboTimes = EventManager.Instance.LoseComboTimes,
            LoseComboTimeForPassion = EventManager.Instance.LoseComboTimeForPassion,
            LoseComboGuestSetNum = EventManager.Instance.LoseComboGuestSetNum,
        }.Enqueue();
}
