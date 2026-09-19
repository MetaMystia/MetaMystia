using Common.UI;

using MetaMystia.Network;

static partial class Checks
{
    static async Task Stages()
    {
        await using var server = new Server(new() { Messages = GameMessageRules.Create() });
        await server.StartAsync();
        var host = await Connect(server, "stage-host");
        var guest = await Connect(server, "stage-guest");
        host.SetProfile("stage-host", new(), Scene.MainScene, GameStage.MainMenu);
        guest.SetProfile("stage-guest", new(), Scene.MainScene, GameStage.MainMenu);
        await Pump(host.CreateRoomAsync());
        await Pump(host.SetJoinableAsync(true));
        await Pump(guest.JoinRoomAsync(host.State.Room!.Id));
        var room = guest.State.Room!.Id;
        var membership = guest.State.Room.Members.Single(p => p.Uid == guest.Uid).Membership;
        Assert(host.IsConnected && guest.IsConnected, "双方主菜单可以建房与入房");

        var seen = new List<GameStage>();
        host.StateChanged += () => seen.Add(host.State.Room?.Members.SingleOrDefault(p => p.Uid == guest.Uid)?.Stage ?? GameStage.Unavailable);
        foreach (var (scene, stage) in new[]
        {
            (Scene.LoadScene, GameStage.Loading), (Scene.DayScene, GameStage.Day),
            (Scene.LoadScene, GameStage.Loading), (Scene.DayScene, GameStage.Day),
            (Scene.LoadScene, GameStage.Loading), (Scene.MainScene, GameStage.MainMenu),
        }) guest.SetProfile("stage-guest", new(), scene, stage);
        await Until(() => seen.Count >= 6);
        Assert(seen.TakeLast(6).SequenceEqual(new[] { GameStage.Loading, GameStage.Day, GameStage.Loading,
            GameStage.Day, GameStage.Loading, GameStage.MainMenu }), "白天重载与主菜单往返的阶段更新有序到达");
        Assert(guest.State.Room!.Id == room && guest.State.Room.Members.Single(p => p.Uid == guest.Uid).Membership == membership,
            "自由切场景保留连接、房间和入房身份");

        guest.LeaveRoom();
        await Until(() => host.State.Room!.Members.Length == 1);
        guest.SetProfile("stage-guest", new(), Scene.DayScene, GameStage.DayEnd);
        Assert(await Failure(guest.JoinRoomAsync(room)) == "PlayerNotAvailable", "仍在 DayScene 的结束白天阶段不能入房");
        guest.SetProfile("stage-guest", new(), Scene.WorkScene, GameStage.Work);
        Assert(await Failure(guest.CreateRoomAsync()) == "PlayerNotAvailable", "营业中不能通过建房绕过入口协调");
        Assert(guest.IsConnected, "独立服务器拒绝入房后保留世界连接");
        guest.SetProfile("stage-guest", new(), Scene.DayScene, GameStage.Day);
        await Pump(guest.JoinRoomAsync(room));

        var messages = new List<ReceivedMessage>();
        guest.MessageReceived += messages.Add;
        host.SendToRoom((ushort)GameMessageType.BusinessStart, [1]);
        host.SendToRoom((ushort)GameMessageType.GuestSpawn, [2]);
        await Until(() => messages.Count == 2);
        Assert(messages[0].Type == (ushort)GameMessageType.BusinessStart && messages[1].Type == (ushort)GameMessageType.GuestSpawn,
            "营业放行消息先于随后的顾客消息到达");
        var inbound = new List<ReceivedMessage>();
        host.MessageReceived += inbound.Add;
        guest.SendToRoom((ushort)GameMessageType.BusinessStart, [3]);
        guest.SendToRoom((ushort)GameMessageType.DayDestinationConfirm, [3]);
        guest.SendToHost((ushort)GameMessageType.DayDestinationIntent, [4]);
        await Until(() => inbound.Count == 1);
        Assert(inbound[0].Type == (ushort)GameMessageType.DayDestinationIntent, "客人只能提交入口意向，不能广播入口执行或营业放行");
        host.Disconnect();
        guest.Disconnect();
    }
}
