using MetaMystia;
using MetaMystia.Multiplayer;

static class YuyukoPrepChecks
{
    public static void Run(Action<bool, string> check)
    {
        GameSession.IsInRoom = true;
        GameSession.IsRoomHost = false;
        GameFlow.Destination = DayDestination.FinalTrial;
        PlayerManager.Local = new() { Uid = 1 };
        PlayerManager.Peers = new() { [2] = new() { Uid = 2 } };
        PrepSceneManager.ResetYuyukoPrep();
        PlayerProfile.PrepStates.Clear();

        PrepSceneManager.ReceiveYuyukoPrepReady(2, 1);
        check(PlayerManager.Peers[2].IsPrepOver && !PrepSceneManager.IsYuyukoPrepActive,
            "远端先准备好时登记个人就绪，不要求本地已打开准备面板");
        PrepSceneManager.TryBeginYuyukoPrep();
        check(PlayerProfile.PrepStates.SequenceEqual(new[] { true })
            && PrepSceneManager.IsYuyukoPrepReady(2), "打开本地准备广播阶段并保留提前到达的本轮就绪");
        PrepSceneManager.EndYuyukoPrep();
        check(PlayerProfile.PrepStates.SequenceEqual(new[] { true, false })
            && PlayerManager.Peers[2].IsPrepOver, "结束本地准备广播阶段，不清除远端个人就绪");

        PrepSceneManager.TryBeginYuyukoPrep();
        check(PrepSceneManager.YuyukoPrepRound == 2 && !PrepSceneManager.IsYuyukoPrepReady(2),
            "第二轮准备不能沿用第一轮业务就绪");
        PlayerManager.Peers[2].IsPrepOver = false;
        PrepSceneManager.ReceiveYuyukoPrepReady(2, 1);
        check(!PlayerManager.Peers[2].IsPrepOver, "过期就绪消息不能点亮本轮标记");
        PrepSceneManager.ReceiveYuyukoPrepReady(2, 2);
        check(PlayerManager.Peers[2].IsPrepOver && PrepSceneManager.IsYuyukoPrepReady(2),
            "第二轮就绪更新个人标记与业务状态");
        PrepSceneManager.ResetYuyukoPrep();
        check(PrepSceneManager.YuyukoPrepRound == 0 && !PrepSceneManager.IsYuyukoPrepActive,
            "退出试炼清除本地准备轮次");
    }
}

// 游戏组件与发送入口替身；测试直接执行模组的试炼准备状态逻辑。
namespace NightScene
{
    static class NightSceneDirector
    {
        public enum ChallengeType { Story_Yuyuko, Challenge_Yuyuko }
        public static ChallengeType ChallengeMode => ChallengeType.Story_Yuyuko;
    }
}
namespace MetaMystia
{
    public static partial class PrepSceneManager
    {
        public static void BeginPrep() { }
        static class Log { public static void Info(string message) { } }
    }
    static class PlayerProfile
    {
        public static readonly List<bool> PrepStates = new();
        public static void SendProfile() => PrepStates.Add(PrepSceneManager.IsYuyukoPrepActive);
    }
}
namespace MetaMystia.Multiplayer.Messages
{
    static class PrepAllReadyMessage { public static void Send() { } }
}
namespace MetaMystia.Patch
{
    static class IzakayaConfigPannelPatch
    {
        public static void PrepOver() => PrepSceneManager.EndYuyukoPrep();
    }
}
