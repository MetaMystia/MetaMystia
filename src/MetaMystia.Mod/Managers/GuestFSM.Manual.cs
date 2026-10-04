using System.Linq;

using Il2CppSystem.Linq;

using Mystia.Scenes;

using NightScene.GuestManagementUtility;

namespace MetaMystia;

public partial class GuestFSM
{
    public bool IsManualGuest { get; private set; }
    private int manualOrderSeq;

    /// <summary>
    /// 剧情路径手里的控制器。剧情实体由游戏自己创建，捕获它的是保留的剧情补丁；框架句柄随后由挑战服务
    /// <c>IWorkSceneChallengeServices.BossGuest</c> 补上（见 <see cref="SetManualHandle"/>），
    /// 之后一切框架面都走句柄，这个引用只留给剧情路径自己的游戏调用。
    /// </summary>
    internal GuestGroupController? ManualController { get; private set; }

    internal void SetManualOrderSeq(int sequence) => manualOrderSeq = sequence;

    /// <summary>
    /// 补上本体的框架句柄：由营业场景循环内的挑战服务读到，因此这里只做登记（句柄解析不依赖服务作用域）。
    /// </summary>
    internal void SetManualHandle(GuestHandle handle)
    {
        if (handle.IsNone) return;
        Handle = handle;
        GuestsMap.Bind(this);
    }

    // 剧情已经创建实体；这里只绑定网络身份，不重放出生、排队或普通首单。
    internal static GuestFSM BindManual(GuestGroupController controller, int runtimeId = 0)
    {
        var fsm = new GuestFSM
        {
            ManualController = controller,
            GuestType = (GuestsManager.GuestType)(int)controller.ControllType,
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
