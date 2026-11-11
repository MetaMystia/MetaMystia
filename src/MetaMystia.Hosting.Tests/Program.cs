using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Common.UI;

using MetaMystia;
using MetaMystia.Directory;
using MetaMystia.Discovery;
using MetaMystia.Hosting;
using MetaMystia.Network;

Environment.SetEnvironmentVariable("Logging__LogLevel__Default", "Warning");
await HostingChecks.Run();

static class HostingChecks
{
    private static int count;
    private static readonly List<Client> clients = [];
    private static void Assert(bool condition, string message)
    { if (!condition) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }

    private static async Task Until(Func<bool> predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!predicate())
        {
            foreach (var client in clients) client.DispatchPending();
            await Task.Delay(10, deadline.Token);
        }
    }

    private static async Task<Client> Connect(Server server, Versions version, string name)
    {
        var client = new Client(version); clients.Add(client);
        var connect = client.ConnectAsync(server.Endpoint, new Player
        {
            Name = name, Scene = Scene.DayScene, Stage = GameStage.Day,
            Resources = new() { DlcFlags = DlcPack.Core, PackIds = [] }
        });
        await Until(() => connect.IsCompleted); await connect;
        return client;
    }

    private static HttpClient Http(string address, string? token = null)
    {
        var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(10) };
        if (token != null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    internal static async Task Run()
    {
        string root = Path.GetFullPath(Path.Combine(".tmp", "hosting-tests", Guid.NewGuid().ToString("N")));
        System.IO.Directory.CreateDirectory(root);
        var adminToken = Guid.NewGuid().ToString("N");
        var instanceToken = Guid.NewGuid().ToString("N");
        var options = new DirectoryOptions("http://127.0.0.1:0", Path.Combine(root, "instances.json"), adminToken, instanceToken);
        await using var directory = DirectoryApi.Create(options);
        await directory.StartAsync();
        string directoryUrl = directory.Urls.Single();
        var registry = directory.Services.GetRequiredService<InstanceRegistry>();
        using var publicHttp = Http(directoryUrl);
        using var deployHttp = Http(directoryUrl, adminToken);
        Assert((await publicHttp.GetAsync("admin/instances")).StatusCode == HttpStatusCode.Unauthorized, "目录管理接口拒绝未认证请求");

        var oldVersion = Versions.Current with { Mod = "old-test" };
        var newVersion = Versions.Current with { Mod = "new-test" };
        await using var old = new Server(new() { Versions = oldVersion, MaxPlayers = 4 });
        await using var newer = new Server(new() { Versions = newVersion, MaxPlayers = 4 });
        await old.StartAsync(); await newer.StartAsync();
        var oldOptions = new ServerAdminOptions("old", "http://127.0.0.1:0", instanceToken, directoryUrl);
        await using var oldAdmin = ServerAdministration.Create(old, oldOptions, oldVersion);
        await using var newAdmin = ServerAdministration.Create(newer, new("new", "http://127.0.0.1:0", instanceToken, directoryUrl), newVersion);
        await oldAdmin.StartAsync(); await newAdmin.StartAsync();
        using var unauthenticated = Http(oldAdmin.Urls.Single());
        Assert((await unauthenticated.PostAsJsonAsync("admin/drain", new DrainRequest("forged", "bad"))).StatusCode == HttpStatusCode.Unauthorized,
            "实例管理接口拒绝未认证退役请求");

        InstanceRegistration Registration(Server server, Versions version, WebApplication admin) =>
            new(version.Game, version.Mod, "127.0.0.1", server.Endpoint.Port, admin.Urls.Single());
        async Task Register(string id, InstanceRegistration body)
        {
            using var response = await deployHttp.PutAsJsonAsync("admin/instances/" + id, body);
            response.EnsureSuccessStatusCode();
        }
        async Task Activate(string id, params string[] replaces)
        {
            using var response = await deployHttp.PostAsJsonAsync($"admin/instances/{id}/activate", new ActivationRequest(replaces));
            response.EnsureSuccessStatusCode();
        }
        var oldRegistration = Registration(old, oldVersion, oldAdmin);
        await Register("old", oldRegistration);
        await Activate("old");
        var listing = await ServerDirectory.ResolveAsync(directoryUrl + "/v1/servers", oldVersion.Game, oldVersion.Mod);
        Assert(listing?.State == "ready" && listing.Port == old.Endpoint.Port, "客户端按游戏和模组版本精确解析地址");
        Assert(await ServerDirectory.ResolveAsync(directoryUrl + "/v1/servers", "wrong-game", oldVersion.Mod) == null, "目录不跨游戏版本选择实例");

        var first = await Connect(old, oldVersion, "first");
        var second = await Connect(old, oldVersion, "second");
        var notices = new List<ServerNotice>(); first.NoticeReceived += notices.Add;
        await Register("new", Registration(newer, newVersion, newAdmin));
        Assert(await ServerDirectory.ResolveAsync(directoryUrl + "/v1/servers", newVersion.Game, newVersion.Mod) == null, "登记但未激活的实例不对玩家开放");
        using (var bad = await deployHttp.PutAsJsonAsync("admin/instances/wrong", Registration(newer, oldVersion, newAdmin)))
            Assert(!bad.IsSuccessStatusCode, "登记校验实际实例身份及版本");
        using (var bad = await deployHttp.PostAsJsonAsync("admin/instances/missing/activate", new ActivationRequest(["old"])))
            Assert(!bad.IsSuccessStatusCode && registry.Get("old")?.State == "active", "无效部署不改变旧实例状态");
        await Activate("new", "old");
        await Until(() => notices.Count == 1);
        Assert(first.IsConnected && second.IsConnected && notices[0].TargetModVersion == newVersion.Mod, "HTTP 激活后旧服广播结构化事件，在线玩家不断线");
        Assert((await ServerDirectory.ResolveAsync(directoryUrl + "/v1/servers", oldVersion.Game, oldVersion.Mod))?.State == "retiring", "目录不再提供退役实例连接地址");
        Assert((await ServerDirectory.ResolveAsync(directoryUrl + "/v1/servers", newVersion.Game, newVersion.Mod))?.Port == newer.Endpoint.Port, "新版目录指向新版端口");
        Assert(!await ServerAdministration.CanStartAsync(oldOptions), "实例重启前识别持久退役状态，不重新接客");
        await Activate("new", "old");
        await registry.ReconcileAsync(CancellationToken.None);
        first.DispatchPending();
        Assert(notices.Count == 1, "激活与通知重试不会重复广播");
        using (var bad = await deployHttp.PutAsJsonAsync("admin/instances/old", oldRegistration with { Port = 1 }))
            Assert(bad.StatusCode == HttpStatusCode.Conflict, "实例 ID 不能被另一次部署覆盖");

        var rejected = new Client(oldVersion); clients.Add(rejected);
        var connectLate = rejected.ConnectAsync(old.Endpoint, new Player { Name = "late", Resources = new() { DlcFlags = DlcPack.Core, PackIds = [] } });
        await Until(() => connectLate.IsCompleted);
        NetworkErrorCode lateError = NetworkErrorCode.None;
        try { await connectLate; } catch (NetworkException error) { lateError = error.Code; }
        Assert(lateError == NetworkErrorCode.ServerRetiring, "缓存旧地址直接连接仍被服务端拒绝");
        first.Disconnect();
        await Until(() => second.State.World.Length == 1);
        Assert(!old.Drained.IsCompleted, "一人离线不会关闭仍有玩家的实例");
        second.Disconnect();
        await old.Drained.WaitAsync(TimeSpan.FromSeconds(3));
        await oldAdmin.StopAsync(); await old.StopAsync();
        await ServerAdministration.ReportStoppedAsync(oldOptions);
        Assert(registry.Get("old")?.State == "stopped", "空服退出后通过 HTTP 完成退役登记");
        var newClient = await Connect(newer, newVersion, "newPlayer");
        Assert(newClient.IsConnected, "旧服退出不影响新版实际 TCP 连接");

        await directory.StopAsync();
        await using var restarted = DirectoryApi.Create(options);
        await restarted.StartAsync();
        var recovered = restarted.Services.GetRequiredService<InstanceRegistry>();
        await recovered.ReconcileAsync(CancellationToken.None);
        Assert(recovered.Get("old")?.State == "stopped" && recovered.Get("new")?.State == "active", "目录服务重启保留发布与退役状态");
        Assert(newClient.IsConnected, "目录服务重启不会中断游戏连接");
        await ProcessRetirement(restarted.Urls.Single(), adminToken, instanceToken);
        foreach (var client in clients) client.Dispose();
        await restarted.StopAsync();
        await newAdmin.StopAsync();
        Console.WriteLine($"ALL PASS ({count} hosting assertions)");
    }

    private static async Task ProcessRetirement(string directoryUrl, string adminToken, string instanceToken)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        string adminUrl = $"http://127.0.0.1:{port}";
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "MetaMystia.Server.exe" : "MetaMystia.Server"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("0");
        start.Environment["MM_INSTANCE_ID"] = "process-test";
        start.Environment["MM_ADMIN_URL"] = adminUrl;
        start.Environment["MM_INSTANCE_TOKEN"] = instanceToken;
        start.Environment["MM_DIRECTORY_URL"] = directoryUrl;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            using var http = Http(adminUrl, instanceToken);
            InstanceStatus? status = null;
            for (int i = 0; i < 100 && !process.HasExited; i++)
            {
                try { status = await http.GetFromJsonAsync<InstanceStatus>("admin/status"); break; }
                catch (HttpRequestException) { await Task.Delay(100); }
            }
            Assert(status != null, "真实服务端子进程启动管理 HTTP");
            using var deploy = Http(directoryUrl, adminToken);
            using (var response = await deploy.PutAsJsonAsync("admin/instances/process-test",
                new InstanceRegistration(status!.GameVersion, status.ModVersion, "127.0.0.1", status.Port, adminUrl)))
                response.EnsureSuccessStatusCode();
            using (var response = await deploy.PostAsJsonAsync("admin/instances/process-test/activate", new ActivationRequest([])))
                response.EnsureSuccessStatusCode();
            var player = new Client(); clients.Add(player);
            var connection = player.ConnectAsync(new IPEndPoint(IPAddress.Loopback, status.Port), new Player
            { Name = "processPlayer", Resources = new() { DlcFlags = DlcPack.Core, PackIds = [] } });
            await Until(() => connection.IsCompleted); await connection;
            using (var response = await deploy.PostAsJsonAsync("admin/instances/new/activate", new ActivationRequest(["process-test"])))
                response.EnsureSuccessStatusCode();
            bool notified = false;
            player.NoticeReceived += _ => notified = true;
            await Until(() => notified);
            Assert(!process.HasExited && player.IsConnected, "真实服务端进入退役后保留在线玩家");
            player.Disconnect();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            Assert(process.ExitCode == 0, "最后一人离线后真实服务端进程正常退出");
            using var internalHttp = Http(directoryUrl, instanceToken);
            var record = await internalHttp.GetFromJsonAsync<InstanceRecord>("internal/instances/process-test");
            Assert(record?.State == "stopped", "真实服务端退出时完成 HTTP 登记");
        }
        finally
        {
            if (!process.HasExited) process.Kill(true);
            await process.WaitForExitAsync();
            string stderr = await errors;
            if (process.ExitCode != 0) Console.Error.WriteLine((await output) + stderr);
        }
    }
}
