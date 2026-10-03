using Common.UI;
using Common.UI.GlobalMap;

using GameData.Core.Collections;
using GameData.RunTime.NightSceneUtility;

using Mystia.Listeners;
using Mystia.Scenes;

using MetaMystia;
using MetaMystia.Listeners;
using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using PrepNightScene.UI;

static class IzakayaSelectionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var sync = new PrepSync();
        var prep = new FakePrepServices();
        var day = new FakeDayServices();

        var map1 = new MapSpot("BeastForest");
        var map2 = new MapSpot("HumanVillage");
        var panel = new IzakayaSelectorPanel_New();
        var config = new IzakayaConfigPannel();
        var guideMap = new GuideMapView(panel);
        var configView = new PrepConfigView(config);

        GameSession.IsInRoom = true;
        GameSession.IsRoomHost = true;
        GameFlow.Destination = DayDestination.Business;
        PlayerManager.Peers.Clear();

        // 单人：放行原版地图确认
        sync.Setup(prep);
        check(prep.Map.ConfirmEnabled, "没有同伴时放行原版地图确认");

        // 联机：拦住原版地图确认，由本类的选店共识推进
        PlayerManager.Peers.Add(2, new() { Uid = 2 });
        PrepSceneManager.Reset();
        sync.Setup(prep);
        check(!prep.Map.ConfirmEnabled, "备菜场景拦住原版地图确认");
        check(PrepSceneManager.IsOpeningPanel, "场景开始到面板打开之间不接受同步编辑");

        // 主机提交：广播自己的选择，同伴还没选则不确认也不推进
        panel.m_CurrentSelectedSpot = map1;
        panel.m_CurrentSelectedIzakayaLevel = IzakayaLevel.Level1;
        SelectIzakayaMessage.Sent.Clear();
        ConfirmIzakayaMessage.Sent.Clear();
        sync.OnGuideMapConfirmed(guideMap);
        check(SelectIzakayaMessage.Sent.SequenceEqual([(MapLabel.BeastForest, 1)]),
            "提交后广播自己的选择");
        check(ConfirmIzakayaMessage.Sent.Count == 0, "同伴未选时不广播确认");
        check(prep.Map.Confirmed.Count == 0, "未一致时不推进");

        // 主机一致：广播确认，并在白天循环的 Update 里按提交的地图与等级触发原版确认
        PlayerManager.Peers[2].IzakayaMapLabel = MapLabel.BeastForest;
        PlayerManager.Peers[2].IzakayaLevel = 1;
        PrepSync.TryConfirmSelection();
        check(ConfirmIzakayaMessage.Sent.SequenceEqual([(MapLabel.BeastForest, 1)]),
            "全员一致后广播确认");
        check(prep.Map.Confirmed.Count == 0, "推进不在通知回调里执行");
        sync.Update(day, 0);
        check(prep.Map.Confirmed.SequenceEqual([(map1, IzakayaLevel.Level1)]),
            "白天场景循环里按提交的地图与确认等级推进");
        PrepSync.TryConfirmSelection();
        check(ConfirmIzakayaMessage.Sent.Count == 1, "重复检查不重复广播");

        // 客机：焦点变化后仍按提交的地图恢复同一店铺
        GameSession.IsRoomHost = false;
        panel.m_CurrentSelectedSpot = map2;
        panel.m_CurrentSelectedIzakayaLevel = IzakayaLevel.Level3;
        PrepSync.TryProceedWithConfirmedSelection(MapLabel.BeastForest, IzakayaLevel.Level1);
        sync.Update(day, 0);
        check(prep.Map.Confirmed.Last() == (map1, IzakayaLevel.Level1),
            "客机按提交的地图推进，不受焦点变化影响");

        // 中途退房：原版确认已被拦住，按本机选择直接推进
        PlayerManager.Peers.Clear();
        panel.m_CurrentSelectedSpot = map1;
        panel.m_CurrentSelectedIzakayaLevel = IzakayaLevel.Level1;
        sync.OnGuideMapConfirmed(guideMap);
        sync.Update(day, 0);
        check(prep.Map.Confirmed.Last() == (map1, IzakayaLevel.Level1), "退房后按本机选择直接推进");

        // 面板打开：结束开启窗口并开始备菜；之后的切页只恢复联机配置
        GameSession.IsRoomHost = true;
        PrepSceneManager.Reset();
        sync.Setup(prep);
        check(PrepSceneManager.IsOpeningPanel, "面板打开前处于开启窗口");
        sync.OnConfigTabSelected(configView);
        check(!PrepSceneManager.IsOpeningPanel && PrepSceneManager.BeginPrepCount == 1,
            "面板打开后开始备菜");
        check(PrepSceneManager.UpdateGroupsCount == 0, "打开面板的那次切页不恢复联机配置");
        PrepSceneManager.CanSyncEdits = true;
        sync.OnConfigTabSelected(configView);
        check(PrepSceneManager.UpdateGroupsCount == 1 && PrepSceneManager.UpdateUICount == 1,
            "之后的切页按主机身份恢复联机配置与界面");

        // 面板确认：单人时不改就绪状态
        PlayerManager.Peers.Clear();
        PlayerManager.LocalIsPrepOver = false;
        sync.OnPrepConfirmed(configView);
        check(!PlayerManager.LocalIsPrepOver, "单人时面板确认不改就绪状态");

        // 菜单逐项同步：对端缺 DLC 时不发送并跳过原版注册，否则发送
        PlayerManager.Peers.Add(2, new() { Uid = 2 });
        PrepSceneManager.Reset();
        sync.Setup(prep);
        UpdatePrepMessage.Submitted.Clear();
        check(!PlayerManager.RecipeAvailable(7), "对端缺少该菜谱");
        var cancel = false;
        sync.OnPreRecipeAdded(7, ref cancel);
        check(cancel && UpdatePrepMessage.Submitted.Count == 0, "对端缺 DLC 时跳过原版注册且不发送");
        cancel = false;
        sync.OnPreRecipeAdded(1, ref cancel);
        check(!cancel && UpdatePrepMessage.Submitted.Count == 1, "对端有该菜谱时发送新增");
        sync.OnRecipeRemoved(1);
        check(UpdatePrepMessage.Submitted.Count == 2, "移除菜谱仍通知同步");

        // 存放食物：正常的存放广播，远端重放时不再回广播
        IzakayaConfigure.Stored += sellable => sync.OnFoodStored(sellable);
        StoreFoodMessage.Sent.Clear();
        sync.OnFoodStored(new Sellable());
        check(StoreFoodMessage.Sent.Count == 1, "存放食物广播同步");
        PrepSync.StoreFood(new Sellable());
        check(StoreFoodMessage.Sent.Count == 1, "远端重放的存放不重复广播");

        // 开始营业：单人放行原版；联机拦住按钮回调，收尾时在备菜场景循环里放行同一个原版回调
        PlayerManager.Peers.Clear();
        PrepSceneManager.Reset();
        sync.Setup(prep);
        check(prep.Session.CompleteEnabled, "没有同伴时放行原版「开始营业」");

        PlayerManager.Peers.Add(2, new() { Uid = 2 });
        PrepSceneManager.Reset();
        sync.Setup(prep);
        check(!prep.Session.CompleteEnabled, "联机时拦住原版「开始营业」");
        PrepSync.PrepOver();
        check(prep.Session.Completed == 0, "收尾不在点击回调里推进营业");
        sync.Update(prep, 0);
        check(prep.Session.Completed == 1, "备菜场景循环里放行原版「开始营业」");
        sync.Update(prep, 0);
        check(prep.Session.Completed == 1, "营业推进只执行一次");
    }
}

internal sealed class FakePrepServices : IPrepNightSceneServices
{
    internal FakeMap Map { get; } = new();

    internal FakeSession Session { get; } = new();

    IPrepNightMapServices IPrepNightSceneServices.Map => Map;

    IPrepNightSessionServices IPrepNightSceneServices.Session => Session;
}

internal sealed class FakeSession : IPrepNightSessionServices
{
    internal bool CompleteEnabled = true;

    internal int Completed;

    public void SetCompleteEnabled(bool enabled) => CompleteEnabled = enabled;

    public void Confirm() => Completed++;
}

internal sealed class FakeMap : IPrepNightMapServices
{
    internal readonly List<(IGuideMapSpot Spot, IzakayaLevel Level)> Confirmed = new();

    internal bool ConfirmEnabled = true;

    public void SetConfirmEnabled(bool enabled) => ConfirmEnabled = enabled;

    public void Confirm(IGuideMapSpot spot, IzakayaLevel level) => Confirmed.Add((spot, level));
}

internal sealed class FakeDayServices : IDaySceneServices
{
}

// 契约面：与中间件 SDK 的同名接口保持相同的成员签名。
namespace Mystia.Listeners
{
    public interface IPrepListener
    {
        void OnPreRecipeAdded(int id, ref bool cancelInvocation);
        void OnPreBeverageAdded(int id, ref bool cancelInvocation);
        void OnPreCookerAssigned(int id, int index, ref bool cancelInvocation);
        void OnGuideMapConfirmed(GuideMapView view);
        void OnConfigTabSelected(PrepConfigView view);
        void OnConfigureUpdated(IzakayaConfigure configure);
        void OnRecipeRemoved(int id);
        void OnBeverageRemoved(int id);
        void OnFoodStored(Sellable sellable);
    }

    public interface IPrepNightSceneGameLoop
    {
        void Setup(IPrepNightSceneServices services);
        void Update(IPrepNightSceneServices services, float delta) { }
        void Shutdown(IPrepNightSceneServices services);
    }

    public interface IDaySceneGameLoop
    {
        void Setup(IDaySceneServices services);
        void Update(IDaySceneServices services, float delta);
        void Shutdown(IDaySceneServices services);
    }
}

namespace Mystia.Scenes
{
    public interface IPrepNightSceneServices
    {
        IPrepNightMapServices Map { get; }

        IPrepNightSessionServices Session { get; }
    }

    public interface IPrepNightMapServices
    {
        void SetConfirmEnabled(bool enabled);
        void Confirm(IGuideMapSpot spot, IzakayaLevel level);
    }

    public interface IPrepNightSessionServices
    {
        void SetCompleteEnabled(bool enabled);
        void Confirm();
    }

    public interface IDaySceneServices
    {
    }

    /// <summary>契约面：与中间件 SDK 的同名视图保持相同的成员签名（这里只落 PrepSync 用到的部分）。</summary>
    public sealed class GuideMapView
    {
        private readonly IzakayaSelectorPanel_New _panel;

        public GuideMapView(IzakayaSelectorPanel_New panel) => _panel = panel;

        public IGuideMapSpot SelectedSpot => _panel.m_CurrentSelectedSpot;

        public string SelectedMapLabel => _panel.m_CurrentSelectedSpot?.PrimaryName;

        public int SelectedLevel => (int)_panel.m_CurrentSelectedIzakayaLevel;
    }

    public sealed class PrepConfigView
    {
        private readonly IzakayaConfigPannel _panel;

        public PrepConfigView(IzakayaConfigPannel panel) => _panel = panel;

        public bool IsOpen => _panel.IsPanelOpened;

        public string Name => _panel.name;

        public void Refresh() => _panel.SolveDailyCompletion();

        public void UpdateGroups()
        {
            _panel.m_RecipeGroup?.UpdateGroupRaw();
            _panel.m_BeverageGroup?.UpdateGroupRaw();
            _panel.m_CookerGroup?.UpdateGroupRaw();
        }

        public void UpdateCookers() => _panel.m_CookerGroup?.UpdateGroupRaw();

        public void UpdateUi()
        {
            Refresh();
            UpdateGroups();
        }
    }
}

namespace MetaMystia.Listeners
{
    public sealed partial class PrepSync
    {
        static class Log
        {
            public static void Info(string message) { }
            public static void Message(string message) { }
            public static void LogInfo(string message) { }
            public static void LogMessage(string message) { }
            public static void LogWarning(string message) { }
            public static void Warning(string message) { }
            public static void Error(string message) { }
        }
    }
}

namespace MetaMystia
{
    public static class MapLabelExtensions
    {
        public static MapLabel FromMapKey(string key) => Enum.Parse<MapLabel>(key);
        public static string ToMapKey(this MapLabel map) => map.ToString();
        public static bool IsSelected(this MapLabel map) => map != MapLabel.Unknown;
        public static string FormatIzakayaSelection(this MapLabel map, int level) => $"{map}/{level}";
    }
}

namespace Common.UI
{
    public class MapSpot(string primaryName) : IGuideMapSpot
    {
        public string PrimaryName { get; } = primaryName;
    }

    public class IzakayaSelectorPanel_New
    {
        public IGuideMapSpot m_CurrentSelectedSpot;
        public IzakayaLevel m_CurrentSelectedIzakayaLevel;
    }
}

namespace Common.UI.GlobalMap
{
    public interface IGuideMapSpot
    {
        string PrimaryName { get; }
    }
}

namespace PrepNightScene.UI
{
    public class IzakayaConfigPannel
    {
        public bool IsPanelOpened => true;

        public string name => "IzakayaConfigPannelNew(Clone)";

        public Group m_CookerGroup = new();
        public Group m_BeverageGroup = new();
        public Group m_RecipeGroup = new();

        public void SolveDailyCompletion() { }

        public sealed class Group
        {
            public void UpdateGroupRaw() { }
        }
    }
}

namespace GameData.Core.Collections
{
    public class Sellable
    {
        public Language Text { get; } = new();

        public sealed class Language
        {
            public string Name => "sellable";
        }
    }
}

namespace GameData.RunTime.NightSceneUtility
{
    public class IzakayaConfigure
    {
        public static readonly IzakayaConfigure Instance = new();

        /// <summary>模拟原版 StoreFood 触发的通知，用于验证重入闩。</summary>
        internal static Action<Sellable> Stored;

        public int[] CookerConfigure = new int[4];
        public UnityEngine.Vector2 NormalGuestInterval;
        public float SpecialGuestGachaInterval;

        public void StoreFood(Sellable sellable, int messageSender = -1) => Stored?.Invoke(sellable);

        public void PreCalculateRecipes() { }
    }
}

namespace MetaMystia.Multiplayer.Messages
{
    static class SelectIzakayaMessage
    {
        public static readonly List<(MapLabel, int)> Sent = new();
        public static void Send(MapLabel map, int level) => Sent.Add((map, level));
    }

    static class ConfirmIzakayaMessage
    {
        public static readonly List<(MapLabel, int)> Sent = new();
        public static void Send(MapLabel map, int level) => Sent.Add((map, level));
    }

    static class PrepReadyMessage
    {
        public static void Send() { }
    }

    class UpdatePrepMessage
    {
        public static readonly List<UpdatePrepMessage> Submitted = new();

        public int[] AddedRecipes = [];
        public int[] RemovedRecipes = [];
        public int[] AddedBeverages = [];
        public int[] RemovedBeverages = [];
        public Dictionary<int, int> ChangedCookers = new();

        public void Submit() => Submitted.Add(this);
    }

    static class StoreFoodMessage
    {
        public static readonly List<SellableFood> Sent = new();
        public static void Send(SellableFood food) => Sent.Add(food);
    }

    public partial class SellableFood
    {
        public static SellableFood FromSellable(Sellable sellable) => new();
    }
}
