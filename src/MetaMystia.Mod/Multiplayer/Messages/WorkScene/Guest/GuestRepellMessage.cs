using MemoryPack;

using Mystia.Scenes;

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

    protected override bool RequireHostSender => true;
    protected override bool ClientOnlyReceive => true;
    protected override bool DiscardOnStory => true;
    protected override Common.UI.Scene? ReceiveScene => Common.UI.Scene.WorkScene;

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

    public static void Send(int runtimeId, GuestHandle handle, GuestLeaveType leaveType, bool triggerLeaveBuff)
    {
        var mood = handle.TryGet(out var guest) ? guest.Mood : 0;
        new GuestRepellMessage
        {
            RuntimeId = runtimeId,
            LeaveType = (GuestGroupController.LeaveType)(int)leaveType,
            TriggerLeaveBuff = triggerLeaveBuff,
            Mood = mood,
            Combo = EventManager.Instance.CurrentCombo,
            LoseComboTimes = EventManager.Instance.LoseComboTimes,
            LoseComboTimeForPassion = EventManager.Instance.LoseComboTimeForPassion,
            LoseComboGuestSetNum = EventManager.Instance.LoseComboGuestSetNum,
        }.Enqueue();
    }
}
