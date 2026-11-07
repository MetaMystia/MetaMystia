using Common.UI;

using MetaMystia;
using MetaMystia.Network;

static partial class Checks
{
    static async Task PlayerLocations()
    {
        await using var server = new Server(new() { MaxPlayers = 4 });
        await server.StartAsync();
        var walker = await Connect(server, "walker");
        var viewer = await Connect(server, "viewer");
        await Until(() => viewer.State.World.Length == 2);
        walker.SendMotion(new() { X = 17, Map = MapLabel.Home });
        await Until(() => viewer.State.World.Single(p => p.Uid == walker.Uid).HasMotion);

        // 已经进入接收队列的运动不因本地切场景而丢失。
        clients.Remove(viewer);
        walker.SendMotion(new() { X = 19, Map = MapLabel.Home });
        await Until(() => viewer.PendingFrames > 0);
        viewer.SetProfile("viewer", new(), Scene.MainScene, GameStage.MainMenu);
        viewer.DispatchPending();
        clients.Add(viewer);
        Assert(viewer.State.World.Single(p => p.Uid == walker.Uid).Motion.X == 19,
            "切回主菜单仍接收已到达的白天运动");
        await Pump(viewer.CreateRoomAsync());
        Assert(viewer.State.World.Single(p => p.Uid == walker.Uid).Motion.X == 19,
            "主菜单管理快照保留最近收到的位置");

        int updates = 0;
        viewer.StateChanged += () => updates++;
        for (int i = 0; i < 20; i++) walker.SendMotion(new() { X = 20 + i, Map = MapLabel.Home });
        // 同连接有序资料作为处理完成标记。
        walker.SetProfile("walker", new() { SkinIndex = 1 }, Scene.DayScene, GameStage.Day);
        await Until(() => viewer.State.World.Single(p => p.Uid == walker.Uid).Skin.SkinIndex == 1);
        Assert(updates == 1 && viewer.State.World.Single(p => p.Uid == walker.Uid).Motion.X == 19,
            "同地图连续移动不向主菜单增发运动或地图资料");

        walker.SendMotion(new() { X = 50, Map = MapLabel.HumanVillage });
        await Until(() => viewer.State.World.Single(p => p.Uid == walker.Uid).Map == MapLabel.HumanVillage);
        var moved = viewer.State.World.Single(p => p.Uid == walker.Uid);
        Assert(moved.Scene == Scene.DayScene && !moved.HasMotion && moved.Motion.X == 0,
            "主菜单收到换图资料并清除旧地图坐标，不接收新运动");

        var late = new Client(); clients.Add(late);
        await Pump(late.ConnectAsync(server.Endpoint, Player("late-menu") with { Scene = Scene.MainScene, Stage = GameStage.MainMenu }));
        var initial = late.State.World.Single(p => p.Uid == walker.Uid);
        Assert(initial.Map == MapLabel.HumanVillage && !initial.HasMotion,
            "主菜单新连接从初始快照得到地图，不补发受限坐标");

        viewer.SetProfile("viewer", new(), Scene.DayScene, GameStage.Day);
        await Until(() => viewer.State.World.Single(p => p.Uid == walker.Uid).HasMotion);
        Assert(viewer.State.World.Single(p => p.Uid == walker.Uid).Motion.X == 50,
            "返回白天从快照获取最新位置");

        foreach (var (scene, stage) in new[] { (Scene.IzakayaPrepScene, GameStage.Preparation),
            (Scene.WorkScene, GameStage.Work), (Scene.ResultScene, GameStage.Result), (Scene.MainScene, GameStage.MainMenu) })
        {
            walker.SetProfile("walker", new(), scene, stage);
            await Until(() => late.State.World.Single(p => p.Uid == walker.Uid).Scene == scene);
            var player = late.State.World.Single(p => p.Uid == walker.Uid);
            Assert(player.Stage == stage && player.Map == MapLabel.Unknown && !player.HasMotion,
                "全服同步场景并清除旧地图与坐标：" + scene);
        }
        int walkerUid = walker.Uid;
        walker.Disconnect();
        await Until(() => viewer.State.World.All(p => p.Uid != walkerUid));
        Assert(viewer.State.World.Length == 2, "玩家离线清除位置缓存");
        viewer.Disconnect(); late.Disconnect();
    }
}
