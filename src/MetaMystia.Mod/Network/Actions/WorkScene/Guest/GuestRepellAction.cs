using MemoryPack;

using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

namespace MetaMystia.Network;

/// <summary>主机已进入驱赶清理；客机不重跑玩家赶客的判定与付款。</summary>
[MemoryPackable]
[AutoLog]
public partial class GuestRepellAction : Action
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
        => GuestFSM.DoRepell(this);

    public static void Send(int runtimeId, GuestGroupController controller, GuestGroupController.LeaveType leaveType, bool triggerLeaveBuff)
        => new GuestRepellAction
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
