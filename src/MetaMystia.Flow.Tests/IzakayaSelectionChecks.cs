using Common.UI;
using Common.UI.GlobalMap;

using MetaMystia;
using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Patch;

static class IzakayaSelectionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var map1 = new MapSpot("BeastForest");
        var map2 = new MapSpot("HumanVillage");
        var panel = new IzakayaSelectorPanel_New();
        panel.m_SpotToExtensions.Add(map1, new() { Level1IzakayaId = 11, Level3IzakayaId = 13 });
        panel.m_SpotToExtensions.Add(map2, new() { Level1IzakayaId = 21, Level3IzakayaId = 23 });
        GameSession.IsInRoom = true;
        GameSession.IsRoomHost = true;
        GameFlow.Destination = DayDestination.Business;
        PlayerManager.Peers.Clear();
        PlayerManager.Peers.Add(2, new() { Uid = 2 });
        IzakayaSelectorPanelPatch.OnGuideMapInitialize_Prefix(panel);
        IzakayaSelectorPanelPatch.cachedSpots.Clear();
        panel.m_CurrentSelectedSpot = map1;
        panel.m_CurrentSelectedIzakayaLevel = IzakayaLevel.Level1;
        ConfirmIzakayaMessage.Sent.Clear();
        IzakayaSelectorPanelPatch._OnGuideMapInitialize_b__21_0_Prefix(ref panel);
        check(ConfirmIzakayaMessage.Sent.Count == 0, "选店提交后等待其他玩家");
        check(IzakayaSelectorPanelPatch.cachedSpots[MapLabel.BeastForest] == map1,
            "提交意向时缓存实际地图对象");

        // A 提交地图 1 后移动焦点；B 的地图 1 意向随后才到达。
        panel.m_CurrentSelectedSpot = map2;
        panel.m_CurrentSelectedIzakayaLevel = IzakayaLevel.Level3;
        IGuideMapSpot focusedSpot = map2;
        IzakayaSelectorPanelPatch.OnGuideMapSpotSelected_Prefix(ref focusedSpot);
        check(IzakayaSelectorPanelPatch.cachedSpots[MapLabel.BeastForest] == map1
            && IzakayaSelectorPanelPatch.cachedSpots[MapLabel.HumanVillage] == map2,
            "移动焦点继续缓存新地图，保留已提交地图");
        PlayerManager.Peers[2].IzakayaMapLabel = MapLabel.BeastForest;
        PlayerManager.Peers[2].IzakayaLevel = 1;
        IzakayaSelectorPanelPatch.TryConfirmSelection();
        check(ConfirmIzakayaMessage.Sent.SequenceEqual(new[] { (MapLabel.BeastForest, 1) }),
            "迟到意向匹配已提交地图，广播地图 1 一级店");
        check(panel.m_CurrentSelectedSpot == map1 && panel.m_CurrentSelectedIzakayaLevel == IzakayaLevel.Level1
            && panel.CurrentIzakayaId == 11, "主机恢复地图和等级，店铺数据同步恢复");
        IzakayaSelectorPanelPatch.TryConfirmSelection();
        check(ConfirmIzakayaMessage.Sent.Count == 1, "匹配消息重复到达不重复广播");

        GameSession.IsRoomHost = false;
        panel.m_CurrentSelectedSpot = map2;
        panel.m_CurrentSelectedIzakayaLevel = IzakayaLevel.Level3;
        IzakayaSelectorPanelPatch.TryProceedWithConfirmedSelection(MapLabel.BeastForest, IzakayaLevel.Level1);
        check(panel.m_CurrentSelectedSpot == map1 && panel.CurrentIzakayaId == 11,
            "客机焦点变化后也按房主确认恢复同一店铺");
        panel.m_CurrentSelectedIzakayaLevel = IzakayaLevel.Level3;
        IzakayaSelectorPanelPatch.TryProceedWithConfirmedSelection(MapLabel.BeastForest, IzakayaLevel.Level1);
        check(panel.m_CurrentSelectedIzakayaLevel == IzakayaLevel.Level1 && panel.CurrentIzakayaId == 11,
            "同地图仅改变等级时恢复确认等级");

    }
}

// 替代游戏面板与 Harmony；测试直接编译选店 Patch，ReversePatch 空体不模拟场景切换。
namespace Common.UI
{
    public class MapSpot(string primaryName) : IGuideMapSpot
    {
        public string PrimaryName { get; } = primaryName;
    }
    public class IzakayaSelectorPanel_New
    {
        public class SpotData
        {
            public int? Level1IzakayaId, Level2IzakayaId, Level3IzakayaId;
        }
        public Dictionary<IGuideMapSpot, SpotData> m_SpotToExtensions = new();
        public IGuideMapSpot m_CurrentSelectedSpot;
        public IzakayaLevel m_CurrentSelectedIzakayaLevel;
        public int CurrentIzakayaId;
        public void OnGuideMapInitialize() { }
        public void OnGuideMapSpotSelected(IGuideMapSpot spot) { }
        public void _OnGuideMapInitialize_b__21_0() { }
        public void UpdateToggleStatus(IzakayaLevel level) { }
        public void UpdateCurrentIzakaya()
        {
            var data = m_SpotToExtensions[m_CurrentSelectedSpot];
            CurrentIzakayaId = (m_CurrentSelectedIzakayaLevel switch
            {
                IzakayaLevel.Level1 => data.Level1IzakayaId,
                IzakayaLevel.Level2 => data.Level2IzakayaId,
                _ => data.Level3IzakayaId,
            }).Value;
        }
    }
}
namespace Common.UI.GlobalMap
{
    public interface IGuideMapSpot
    {
        string PrimaryName { get; }
    }
}
namespace MetaMystia
{
    public static class MapLabelExtensions
    {
        public static MapLabel FromMapKey(string key) => Enum.Parse<MapLabel>(key);
        public static bool TryFromMapKey(string key, out MapLabel map) => Enum.TryParse(key, out map);
        public static string ToMapKey(this MapLabel map) => map.ToString();
        public static bool IsSelected(this MapLabel map) => map != MapLabel.Unknown;
        public static string FormatIzakayaSelection(this MapLabel map, int level) => $"{map}/{level}";
    }
}
namespace MetaMystia.Multiplayer.Messages
{
    static class SelectIzakayaMessage
    {
        public static void Send(MapLabel map, int level) { }
    }
    static class ConfirmIzakayaMessage
    {
        public static readonly List<(MapLabel, int)> Sent = new();
        public static void Send(MapLabel map, int level) => Sent.Add((map, level));
    }
}
namespace MetaMystia.Patch
{
    public partial class IzakayaSelectorPanelPatch
    {
        static class Log
        {
            public static void Info(string message) { }
            public static void Message(string message) { }
            public static void LogInfo(string message) { }
            public static void LogMessage(string message) { }
            public static void Error(string message) { }
        }
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type) { }
        public HarmonyPatch(string method) { }
    }
    sealed class HarmonyPrefix : Attribute { }
    sealed class HarmonyReversePatch : Attribute { }
}
