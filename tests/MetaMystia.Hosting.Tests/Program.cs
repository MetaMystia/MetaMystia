using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using Common.UI;

using MetaMystia;
using MetaMystia.Gateway;
using MetaMystia.Hosting;
using MetaMystia.Network;

Environment.SetEnvironmentVariable("Logging__LogLevel__Default", "Warning");
var clients = new List<Client>();
int count = 0;
void Check(bool value, string message)
{ if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); count++; }
async Task Until(Func<Task<bool>> condition)
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
    while (!await condition())
    { foreach (var client in clients) client.DispatchPending(); await Task.Delay(10, deadline.Token); }
}
async Task Pump(Task task) { await Until(() => Task.FromResult(task.IsCompleted)); await task; }
async Task<Client> Connect(IPEndPoint endpoint, Versions version, string name)
{
    var client = new Client(version); clients.Add(client);
    await Pump(client.ConnectAsync(endpoint, new Player
    { Name = name, Scene = Scene.DayScene, Stage = GameStage.Day, Resources = new() { DlcFlags = DlcPack.Core, PackIds = [] } }));
    return client;
}
string path = Path.GetFullPath(Path.Combine(".tmp", "hosting-tests", Guid.NewGuid().ToString("N"), "routes.json"));
await using var api = GatewayApi.Create("http://127.0.0.1:0", path, "admin-test", "instance-test");
await api.StartAsync();
var registry = api.Services.GetRequiredService<InstanceRegistry>();
await using var gateway = new GatewayServer(new(IPAddress.Loopback, 0), registry, stats: api.Services.GetRequiredService<GatewayStats>());
gateway.Start();
using var http = new HttpClient { BaseAddress = new(api.Urls.Single()) };
Check((await http.GetAsync("admin/instances")).StatusCode == HttpStatusCode.Unauthorized, "网关拒绝无令牌请求");
Check((await http.GetAsync("admin/gateway")).StatusCode == HttpStatusCode.Unauthorized, "网关统计同样需要令牌");
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "wrong");
Check((await http.GetAsync("admin/instances")).StatusCode == HttpStatusCode.Unauthorized, "网关拒绝错误令牌");
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-test");
Check((await http.GetAsync("v1/servers")).StatusCode == HttpStatusCode.NotFound, "旧订阅接口不存在");
var created = await (await http.PostAsJsonAsync("admin/tokens", new { name = "bot" })).Content.ReadFromJsonAsync<JsonElement>();
var readToken = created.GetProperty("token").GetString()!;
using var bot = new HttpClient { BaseAddress = http.BaseAddress };
bot.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", readToken);
Check((await bot.GetAsync("admin/instances")).StatusCode == HttpStatusCode.Forbidden &&
    (await bot.PostAsJsonAsync("admin/tokens", new { name = "other" })).StatusCode == HttpStatusCode.Forbidden, "只读令牌不能访问管理接口");
Check((await http.PostAsJsonAsync("admin/tokens", new { name = " " })).StatusCode == HttpStatusCode.BadRequest, "令牌名称不能为空");
var tokensPath = Path.Combine(Path.GetDirectoryName(path)!, "tokens.json");
var tokenList = await http.GetStringAsync("admin/tokens");
Check(tokenList.Contains("\"bot\"") && !tokenList.Contains(readToken) && !File.ReadAllText(tokensPath).Contains(readToken), "令牌列表与文件不含明文");
Check((await bot.GetAsync("api/v1/events")).StatusCode == HttpStatusCode.BadRequest, "事件接口要求 after");
var registeredEvent = bot.GetFromJsonAsync<EventPage>("api/v1/events?after=0&wait=10");
var v1 = Versions.Current with { Mod = "old-test" };
var v2 = Versions.Current with { Mod = "new-test" };
await using var old = new Server(new() { Versions = v1 });
await using var newer = new Server(new() { Versions = v2 });
await old.StartAsync(); await newer.StartAsync();
var configDirectory = Path.GetDirectoryName(path)!;
Directory.CreateDirectory(configDirectory);
await using var oldApi = ServerApi.Create(old, new("old", "http://127.0.0.1:0", "instance-test"), v1, configDirectory);
await using var newApi = ServerApi.Create(newer, new("new", "http://127.0.0.1:0", "instance-test"), v2);
await oldApi.StartAsync(); await newApi.StartAsync();
using var instanceHttp = new HttpClient { BaseAddress = new(oldApi.Urls.Single()) };
Check((await instanceHttp.GetAsync("admin/status")).StatusCode == HttpStatusCode.Unauthorized, "实例独立校验管理令牌");
var registration = new InstanceRegistration(v1.Game, v1.Mod, "127.0.0.1", old.Endpoint.Port, oldApi.Urls.Single());
await Task.Delay(200);
Check(!registeredEvent.IsCompleted, "没有事件时长轮询保持等待");
(await http.PutAsJsonAsync("admin/instances/old", registration)).EnsureSuccessStatusCode();
var registeredPage = await registeredEvent.WaitAsync(TimeSpan.FromSeconds(5));
Check(registeredPage!.Next == 1 && registeredPage.Events.Single() is { Type: "registered", InstanceId: "old" } e && e.ModVersion == v1.Mod,
    "登记后长轮询立即返回事件");
(await http.PutAsJsonAsync("admin/instances/old", registration)).EnsureSuccessStatusCode();
var waits = Enumerable.Range(0, 4).Select(_ => bot.GetFromJsonAsync<EventPage>("api/v1/events?after=1&wait=60")).ToArray();
await Task.Delay(200);
Check((await bot.GetAsync("api/v1/events?after=1&wait=0")).StatusCode == HttpStatusCode.TooManyRequests &&
    (await http.GetAsync("api/v1/events?after=1&wait=0")).StatusCode == HttpStatusCode.OK, "限制单个令牌同时等待的请求数");
Check((await http.PutAsJsonAsync("admin/instances/wrong", registration with { Host = "8.8.8.8" })).StatusCode == HttpStatusCode.BadRequest, "管理转发拒绝公网目标");
Check((await http.PutAsJsonAsync("admin/instances/duplicate", registration)).StatusCode == HttpStatusCode.Conflict, "拒绝重复版本或地址");
var first = await Connect(gateway.Endpoint, v1, "first");
var second = await Connect(gateway.Endpoint, v1, "second");
await Pump(first.CreateRoomAsync()); await Pump(first.SetJoinableAsync(true));
await Pump(second.JoinRoomAsync(first.State.Room!.Id));
Check(second.State.Room?.Id == first.State.Room.Id, "真实握手、建房与入房通过同一入口");
Check((await http.GetFromJsonAsync<InstanceStatus>("admin/instances/old/status"))?.Players == 2, "网关转发状态查询得到真实人数");
var publicStatus = await bot.GetFromJsonAsync<PublicStatus>("api/v1/status");
Check(publicStatus is { Seq: 1, Connections: 2, Players: 2 } && publicStatus.Instances.Single() is
    { Id: "old", State: "active", Reachable: true, Players: 2, Rooms: 1, MaxPlayers: > 0 }, "只读状态汇总人数、房间与路由");
Check((await bot.GetFromJsonAsync<PublicStatus>("api/v1/status"))!.Time == publicStatus!.Time, "只读状态短时间内复用查询结果");
var publicJson = await bot.GetStringAsync("api/v1/status");
Check(!publicJson.Contains("127.0.0.1") && !publicJson.Contains("second"), "只读状态不含 IP 与玩家昵称");
var details = await http.GetFromJsonAsync<JsonElement>("admin/instances/old/api/details");
Check(details.GetProperty("players").GetArrayLength() == 2 && details.GetProperty("rooms").GetArrayLength() == 1 &&
    details.GetProperty("players").EnumerateArray().Any(p => p.GetProperty("name").GetString() == "second" && p.GetProperty("room").GetString() != null) &&
    details.GetProperty("process").GetProperty("workingSetBytes").GetInt64() > 0, "通用转发得到实例玩家、房间与进程信息");
var connections = await http.GetFromJsonAsync<JsonElement>("admin/connections");
Check(connections.EnumerateArray().Count(c => c.GetProperty("modVersion").GetString() == v1.Mod && c.GetProperty("state").GetString() == "forwarding"
    && c.GetProperty("instanceId").GetString() == "old" && c.GetProperty("ip").GetString() == "127.0.0.1") == 2, "连接记录保存 IP、版本与目标实例");
var gatewayInfo = await http.GetFromJsonAsync<JsonElement>("admin/gateway");
Check(gatewayInfo.GetProperty("connections").GetInt32() == 2 && gatewayInfo.GetProperty("totals").GetProperty("forwarded").GetInt64() == 2, "网关统计当前连接与转发总数");
Check((await http.PostAsJsonAsync("admin/instances/old/api/say", new { message = "hello" })).StatusCode == HttpStatusCode.NoContent, "转发服务器广播");
Check((await http.PutAsJsonAsync("admin/instances/old/api/max-players", new { value = 8 })).StatusCode == HttpStatusCode.NoContent &&
    (await old.GetStatusAsync()).MaxPlayers == 8, "转发人数上限调整");
var missing = await http.PostAsync("admin/instances/old/api/players/99999/kick", null);
var configUrl = "admin/instances/old/api/config/server";
var config = await http.GetFromJsonAsync<JsonElement>(configUrl);
var originalConfig = config.GetProperty("content").GetString();
var editedConfig = "{\"logChat\":true,\"welcomeMessages\":[\"欢迎\"]}";
Check((await http.PutAsJsonAsync(configUrl, new { content = editedConfig, previousContent = originalConfig })).StatusCode == HttpStatusCode.NoContent &&
    (await old.GetStatusAsync()).LogChat && (await old.GetStatusAsync()).WelcomeMessages == 1 &&
    File.ReadAllText(Path.Combine(configDirectory, "server.json")) == editedConfig, "面板配置保存到文件并立即应用");
Check((await http.PutAsJsonAsync(configUrl, new { content = "{}", previousContent = originalConfig })).StatusCode == HttpStatusCode.Conflict,
    "过期编辑不能覆盖新配置");
foreach (var invalid in new[] { "{", "null", "{\"welcomeMessages\":[\"\"]}" })
    Check((await http.PutAsJsonAsync(configUrl, new { content = invalid, previousContent = editedConfig })).StatusCode == HttpStatusCode.BadRequest &&
        File.ReadAllText(Path.Combine(configDirectory, "server.json")) == editedConfig && (await old.GetStatusAsync()).LogChat,
        "无效配置保留文件与运行状态：" + invalid);
Check((await http.GetAsync("admin/instances/old/api/config/other")).StatusCode == HttpStatusCode.NotImplemented,
    "配置接口不允许任意文件名");
var filterUrl = "admin/instances/old/api/config/chat-filter";
var filterConfig = await http.GetFromJsonAsync<JsonElement>(filterUrl);
Check((await http.PutAsJsonAsync(filterUrl, new { content = "{\"enabled\":true,\"words\":[\"testword\"]}", previousContent = filterConfig.GetProperty("content").GetString() })).StatusCode == HttpStatusCode.NoContent &&
    (await old.GetStatusAsync()).ChatFilterWords == 1 && (await old.GetStatusAsync()).LogChat,
    "单独保存敏感词不覆盖公告与聊天开关");
Check(missing.StatusCode == HttpStatusCode.Conflict && (await missing.Content.ReadAsStringAsync()).Contains("PlayerMissing"), "实例错误码原样返回");
Check((await http.PostAsJsonAsync("admin/instances/old/api/drain", new DrainRequest("x", "y"))).StatusCode == HttpStatusCode.BadRequest &&
    (await http.GetAsync("admin/instances/old/api/status")).StatusCode == HttpStatusCode.BadRequest &&
    (await http.GetAsync("admin/instances/old/api/Details")).StatusCode == HttpStatusCode.BadRequest, "通用转发拒绝 Gateway 专属接口与非法路径");
Check((await http.GetAsync("admin/instances/old/api/no-such-thing")).StatusCode == HttpStatusCode.NotImplemented, "实例不支持的接口返回 501");
var instanceLogs = await http.GetFromJsonAsync<JsonElement>("admin/instances/old/api/logs");
Check(instanceLogs.GetProperty("entries").EnumerateArray().Any(e => e.GetProperty("message").GetString()!.Contains("广播")) &&
    (await http.GetFromJsonAsync<JsonElement>("admin/instances/old/api/logs?after=" + instanceLogs.GetProperty("next").GetInt64()))
        .GetProperty("entries").GetArrayLength() == 0, "实例日志可增量读取");
Check((await http.GetFromJsonAsync<JsonElement>("admin/instances/old/api/history")).GetProperty("names").GetArrayLength() == 4 &&
    (await http.GetFromJsonAsync<JsonElement>("admin/history")).GetProperty("names").GetArrayLength() == 3, "实例与网关提供趋势数据");
(await http.PutAsJsonAsync("admin/instances/new", new InstanceRegistration(v2.Game, v2.Mod, "127.0.0.1", newer.Endpoint.Port, newApi.Urls.Single()))).EnsureSuccessStatusCode();
Check((await Task.WhenAll(waits).WaitAsync(TimeSpan.FromSeconds(5))).All(page => page!.Events.Single() is { Seq: 2, Type: "registered", InstanceId: "new" }),
    "同一事件唤醒全部等待请求");
var third = await Connect(gateway.Endpoint, v2, "third");
Check(first.IsConnected && third.State.World.All(p => p.Name != "first"), "新增版本不重启网关，版本之间隔离");
using (var restored = new InstanceRegistry(path, "instance-test")) Check(restored.Resolve(v2).Id == "new", "重启后可从文件恢复路由");
first.Disconnect();
await Until(async () => (await old.GetStatusAsync()).Players.Length == 1);
Check(true, "玩家断开会关闭后端连接并释放人数");
bool notice = false; second.NoticeReceived += _ => notice = true;
(await http.PostAsJsonAsync("admin/instances/old/drain", new DrainRequest("release", v2.Mod))).EnsureSuccessStatusCode();
await Until(() => Task.FromResult(notice));
(await http.PostAsJsonAsync("admin/instances/old/drain", new DrainRequest("release", v2.Mod))).EnsureSuccessStatusCode();
Check((await bot.GetFromJsonAsync<EventPage>("api/v1/events?after=2&wait=0"))!.Events.Single() is
    { Type: "draining", InstanceId: "old" } drained && drained.TargetModVersion == v2.Mod, "退役产生一次事件，重发不重复");
Check(second.IsConnected, "退役转发通知，保留已有玩家连接");
try { await Connect(gateway.Endpoint, v1, "late"); throw new Exception("Expected rejection"); }
catch (NetworkException error) { Check(error.Code == NetworkErrorCode.ServerRetiring, "退役路由拒绝新玩家"); }
Check((await http.GetFromJsonAsync<JsonElement>("admin/connections")).EnumerateArray()
    .Any(c => c.GetProperty("state").GetString() == "rejected" && c.GetProperty("reason").GetString() == "ServerRetiring"), "连接记录保存拒绝原因");
Check((await http.GetFromJsonAsync<JsonElement>("admin/logs")).GetProperty("entries").EnumerateArray()
    .Any(e => e.GetProperty("message").GetString()!.Contains("请求退役实例 old")), "网关日志记录管理操作");
second.Disconnect(); await old.Drained.WaitAsync(TimeSpan.FromSeconds(5));
Check(true, "最后一人断开后实例完成退役");
(await http.DeleteAsync("admin/instances/old")).EnsureSuccessStatusCode();
Check((await bot.GetFromJsonAsync<EventPage>("api/v1/events?after=3&wait=0"))!.Events.Single() is { Seq: 4, Type: "removed", InstanceId: "old" },
    "删除登记产生事件");
Check((await bot.GetFromJsonAsync<EventPage>("api/v1/events?after=99&wait=0")) is { Reset: true, Next: 4 }, "序号超出范围时要求重新同步");
Check(new EventLog(Path.Combine(Path.GetDirectoryName(path)!, "events.json")).Seq == 4 &&
    new AccessTokens(tokensPath, "admin-test").Authenticate("Bearer " + readToken) != null, "事件序号与令牌重启后保留");
(await http.DeleteAsync("admin/tokens/" + created.GetProperty("id").GetString())).EnsureSuccessStatusCode();
Check((await bot.GetAsync("api/v1/status")).StatusCode == HttpStatusCode.Unauthorized, "吊销后令牌立即失效");
Check(registry.List().Length == 1 && third.IsConnected, "删除旧路由不影响新版本连接");
await newer.StopAsync(); await Until(() => Task.FromResult(!third.IsConnected));
Check(true, "实例断开会同步关闭玩家连接");
(await http.DeleteAsync("admin/instances/new")).EnsureSuccessStatusCode();
await using var last = new Server(new() { Versions = v2 }); await last.StartAsync();
await using var lastApi = ServerApi.Create(last, new("last", "http://127.0.0.1:0", "instance-test"), v2);
await lastApi.StartAsync();
(await http.PutAsJsonAsync("admin/instances/last", new InstanceRegistration(v2.Game, v2.Mod, "127.0.0.1", last.Endpoint.Port, lastApi.Urls.Single()))).EnsureSuccessStatusCode();
await using (var temporary = new GatewayServer(new(IPAddress.Loopback, 0), registry))
{ temporary.Start(); await Connect(temporary.Endpoint, v2, "shutdown"); }
await Until(async () => (await last.GetStatusAsync()).Players.Length == 0);
Check(true, "停止网关释放全部后端连接");
foreach (var client in clients) client.Dispose();
await api.StopAsync(); await oldApi.StopAsync(); await newApi.StopAsync(); await lastApi.StopAsync();
Console.WriteLine($"ALL PASS ({count} hosting assertions)");
