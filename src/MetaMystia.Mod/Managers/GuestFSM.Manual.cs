using System.Linq;

using Il2CppSystem.Linq;

using NightScene.GuestManagementUtility;

namespace MetaMystia;

public partial class GuestFSM
{
    public bool IsManualGuest { get; private set; }
    private int manualOrderSeq;

    internal void SetManualOrderSeq(int sequence) => manualOrderSeq = sequence;

    // 剧情已经创建实体；这里只绑定网络身份，不重放出生、排队或普通首单。
    internal static GuestFSM BindManual(GuestGroupController controller, int runtimeId = 0)
    {
        var fsm = new GuestFSM
        {
            Controller = controller,
            GuestType = controller.ControllType,
            Ids = controller.GetAllGuests().ToArray().Select(guest => guest.Id).ToArray(),
            Fund = controller.GetFund,
            MaxFundCarry = controller.MaxFundCarry,
            IsManualGuest = true,
            CurrentState = State.Manual,
        };
        if (runtimeId == 0) GuestsMap.StoreGuest(fsm);
        else GuestsMap.StoreGuest(runtimeId, fsm);
        return fsm;
    }

    internal void SetManualState(State state)
    {
        WillServeFood = null;
        WillServeBeverage = null;
        CurrentState = state;
        // 原版回调可能同步触发下一步；由下一帧排空消息，避免在 Hook 内重入原流程。
    }

    internal void CancelManualOrder()
    {
        _pending.Clear();
        SetManualState(State.Manual);
    }
}
