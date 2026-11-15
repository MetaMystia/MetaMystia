using MetaMystia;
using MetaMystia.Multiplayer;
using MetaMystia.Network;
using MetaMystia.Patch;

using NightScene.CookingUtility;

static class CookerChecks
{
    public static void Run(Action<bool, string> check)
    {
        var a = new CookerOperations.Operation(Guid.Parse("10000000-0000-0000-0000-000000000000"), 0, 1);
        var b = new CookerOperations.Operation(Guid.Parse("20000000-0000-0000-0000-000000000000"), 0, 2);
        var c = new CookerOperations.Operation(Guid.Parse("30000000-0000-0000-0000-000000000000"), 0, 3);
        CookerOperations.Operation[][] orders = [[a, b, c], [a, c, b], [b, a, c], [b, c, a], [c, a, b], [c, b, a]];
        foreach (var order in orders)
        {
            var replica = new CookerOperations();
            foreach (var operation in order)
            {
                replica.Accept(0, operation);
                replica.Start(0, operation.Id, operation.Owner);
            }
            check(replica.Get(0)?.Id == a.Id && replica.Get(0).Started, "三人交错提交和 QTE 的不同到达顺序选中同一份料理");
            check(!replica.Start(0, a.Id, a.Owner), "同一份料理不重复启动");
            check(!replica.Complete(0, b.Id, 0), "输掉竞争的取餐不能清空当前料理");
            check(replica.Complete(0, a.Id, 0), "当前料理可以正常取走");
            check(!replica.Accept(0, b) && !replica.Start(0, a.Id, a.Owner), "取餐后的旧提交和旧 QTE 都失效");
            var next = b with { Revision = 1, Id = Guid.NewGuid() };
            check(replica.Accept(0, next) && !replica.Complete(0, a.Id, 0), "迟到的上一轮取餐不能清空下一份菜");
        }

        void Setup()
        {
            CookerSync.Reset();
            PluginHost.Instance.Clear();
            GameSession.IsInRoom = true;
            GameSession.Room = new() { Host = 1, Members = [new() { Uid = 1 }, new() { Uid = 2 }] };
            PlayerManager.Local.Uid = 1;
            GameFlow.LocalScene = Scene.WorkScene;
            CookSystemManager.Instance.AllCookerControllers.Clear();
            GameData.RunTime.Common.RunTimeStorage.Returned.Clear();
        }
        CookController Loaded()
        {
            var cooker = new CookController { GridIndex = 0, Phase = CookController.CookPhase.Loaded, Result = new() };
            CookSystemManager.Instance.AllCookerControllers.Add(cooker);
            return cooker;
        }

        Setup();
        var stove = Loaded();
        var local = CookerSync.Commit(stove, false);
        check(CookerSync.StartLocal(stove, out var id) && id == local.Id, "本地提交不等待网络即可开始 QTE 后的烹饪");
        CookControllerPatch.StartCookCountDown_ReversePatch(stove, 1);
        check(CookerSync.Complete(stove, id, local.Revision) && stove.onCookingCoroutine == null, "取餐先停止旧计时");
        stove.Phase = CookController.CookPhase.Idle;
        stove.Result = null;
        check(!CookerSync.StartRemote(stove, id, 1) && stove.Starts == 1, "取餐后到达的 QTE 不会把空厨具重新置为 Cooking");

        Setup();
        stove = Loaded();
        check(CookerSync.ReceiveCook(stove, b.Id, 0, 2), "收到远端装料");
        GameSession.Room = GameSession.Room with { Members = [new() { Uid = 1 }] };
        PluginHost.Instance.Tick();
        PluginHost.Instance.Tick();
        check(stove.Starts == 1 && stove.LastScore == -1 && stove.Phase == CookController.CookPhase.Cooking,
            "装料者掉线后按普通评分接管一次，不停留在 Loaded");
        check(!CookerSync.StartRemote(stove, b.Id, 2), "掉线接管后的迟到 QTE 不重复启动");

        Setup();
        stove = Loaded();
        CookerSync.ReceiveCook(stove, b.Id, 0, 2);
        check(CookerSync.StartRemote(stove, b.Id, 2), "远端 QTE 正常启动");
        CookControllerPatch.StartCookCountDown_ReversePatch(stove, 0.75f);
        GameSession.Room = GameSession.Room with { Members = [new() { Uid = 1 }] };
        PluginHost.Instance.Tick();
        check(stove.Starts == 1 && stove.LastScore == 0.75f, "已经烹饪时掉线不重启计时或覆盖评分");

        Setup();
        stove = Loaded();
        local = CookerSync.Commit(stove, false);
        GameSession.IsInRoom = false;
        CookerSync.Reset(resume: true);
        check(stove.Starts == 1 && !CookerSync.StartLocal(stove, out _), "整房断线也能续做，原本地 QTE 返回时不能再次启动");

        Setup();
        stove = Loaded();
        CookerSync.ReceiveCook(stove, b.Id, 0, 2);
        CookerSync.Reset();
        GameSession.IsInRoom = false;
        PluginHost.Instance.Tick();
        check(stove.Starts == 0, "场景重置使旧掉线协程失效，不再操作旧厨具");

        Setup();
        PlayerManager.Peers = new() { [2] = new() { Uid = 2 } };
        stove = Loaded();
        var panel = new NightScene.UI.CookingUtility.WorkSceneCookingSelectionPannel { CookerParent = stove, ExtraCostIng = 1 };
        panel.selectedIngredients.AddRange([11, 12]);
        var submit = new NightScene.UI.CookingUtility.WorkSceneCookingSelectionPannel.__c__DisplayClass79_0 { __4__this = panel };
        check(!WorkSceneCookingSelectionPannel__c__DisplayClass79_0Patch.Method_Internal_Void_PDM_0_Prefix(submit)
            && panel.Closed && panel.selectedIngredients.Count == 0,
            "真实提交 Prefix 在原装料和 QTE 之前关闭已占用的旧面板并返还食材");
        check(GameData.RunTime.Common.RunTimeStorage.Returned.SequenceEqual([11, 12])
            && stove.Starts == 0 && stove.Phase == CookController.CookPhase.Loaded,
            "旧面板取消不改动另一人的料理，也不启动 QTE 倒计时");
        CookerSync.Reset();

        Setup();
        stove = Loaded();
        panel = new() { CookerParent = stove, ExtraCostIng = 2 };
        panel.selectedIngredients.AddRange([11, 12]);
        CookerSync.CaptureIngredients(panel);
        local = CookerSync.Commit(stove, true);
        CookerSync.StartLocal(stove, out _);
        CookControllerPatch.StartCookCountDown_ReversePatch(stove, 1);
        var winner = Guid.Parse("00000000-0000-0000-0000-000000000001");
        check(CookerSync.ReceiveCook(stove, winner, 0, 2) && stove.Stops == 1,
            "同轮冲突替换前先取消原计时");
        check(GameData.RunTime.Common.RunTimeStorage.Returned.SequenceEqual([11, 12, 11, 12]),
            "冲突按实际选料及额外消耗次数退回食材");
        check(!CookerSync.ReceiveCook(stove, winner, 0, 2)
            && GameData.RunTime.Common.RunTimeStorage.Returned.Count == 4, "重复装料不会重复退款");
        CookerSync.Reset();
    }
}

namespace Il2CppSystem
{
    public delegate void Action();
}
namespace Il2CppSystem.Collections.Generic
{
    public interface IEnumerable<T> : System.Collections.Generic.IEnumerable<T> { }
    public class List<T> : System.Collections.Generic.List<T>, IEnumerable<T>
    {
        public TCast Cast<TCast>() => (TCast)(object)this;
    }
}
namespace Il2CppSystem.IO { }
namespace DEYU.Utils { }
namespace GameData.RunTime.Common
{
    public static class RunTimeStorage
    {
        public static readonly List<int> Returned = new();
        public static void IngredientInRange(Il2CppSystem.Collections.Generic.IEnumerable<int> values, bool _)
            => Returned.AddRange(values);
    }
}
namespace NightScene.CookingUtility
{
    public class CookController
    {
        public enum CookPhase { Idle, Loaded, Cooking, Finished }
        public int GridIndex;
        public CookPhase Phase;
        public object Result;
        public object onCookingCoroutine;
        public int Starts, Stops;
        public float LastScore;
        public void StopCoroutine(object _) => Stops++;
    }
    public class CookSystemManager
    {
        public static readonly CookSystemManager Instance = new();
        public List<CookController> AllCookerControllers = new();
    }
}
namespace NightScene.UI.CookingUtility
{
    public class WorkSceneCookingSelectionPannel
    {
        public class Recipe { public int Id; }
        public class Combo { public Recipe Recipe; public int[] Modifiers; }
        public class __c__DisplayClass79_0
        {
            public WorkSceneCookingSelectionPannel __4__this;
            public Combo solved;
            public void Method_Internal_Void_PDM_0() { }
        }
        public CookController CookerParent;
        public bool Closed;
        public int ExtraCostIng;
        public List<int> selectedIngredients = new();
        public void ClosePanel()
        {
            Closed = true;
            GameData.RunTime.Common.RunTimeStorage.Returned.AddRange(selectedIngredients);
            selectedIngredients.Clear();
        }
    }
}
namespace GameData.CoreLanguage.Collections
{
    public static class DataBaseLanguage
    {
        public sealed class Ingredient { public string Name; }
        public static Dictionary<int, Ingredient> Ingredients = new();
    }
}
namespace MetaMystia
{
    public static class YuyukoGuestSync
    {
        public static bool IsSwallowedCooker(int _) => false;
    }
}
namespace MetaMystia.Patch
{
    public partial class WorkSceneCookingSelectionPannel__c__DisplayClass79_0Patch
    {
        static class Log { public static void Warning(string _) { } }
    }
    public static class CookControllerPatch
    {
        public static void StartCookCountDown_ReversePatch(CookController cooker, float score, bool _ = false)
        {
            cooker.Starts++;
            cooker.LastScore = score;
            cooker.Phase = CookController.CookPhase.Cooking;
            cooker.onCookingCoroutine = new();
        }
    }
}
