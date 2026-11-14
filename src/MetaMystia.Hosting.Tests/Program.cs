using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

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
await using var gateway = new GatewayServer(new(IPAddress.Loopback, 0), registry);
gateway.Start();
using var http = new HttpClient { BaseAddress = new(api.Urls.Single()) };
Check((await http.GetAsync("admin/instances")).StatusCode == HttpStatusCode.Unauthorized, "网关拒绝无令牌请求");
var panel = await http.GetAsync("admin/");
Check(panel.IsSuccessStatusCode && panel.Content.Headers.ContentType?.MediaType == "text/html" &&
    panel.Headers.TryGetValues("Content-Security-Policy", out _) && (await panel.Content.ReadAsStringAsync()).Contains("panel.js"), "管理面板无需令牌即可加载");
Check((await http.GetAsync("admin/panel.js")).IsSuccessStatusCode && (await http.GetAsync("admin/panel.css")).IsSuccessStatusCode, "管理面板静态文件可访问");
Check((await http.PostAsync("admin/", null)).StatusCode == HttpStatusCode.Unauthorized, "面板路径之外仍需令牌");
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "wrong");
Check((await http.GetAsync("admin/instances")).StatusCode == HttpStatusCode.Unauthorized, "网关拒绝错误令牌");
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-test");
Check((await http.GetAsync("v1/servers")).StatusCode == HttpStatusCode.NotFound, "旧订阅接口不存在");
var v1 = Versions.Current with { Mod = "old-test" };
var v2 = Versions.Current with { Mod = "new-test" };
await using var old = new Server(new() { Versions = v1 });
await using var newer = new Server(new() { Versions = v2 });
await old.StartAsync(); await newer.StartAsync();
await using var oldApi = ServerAdministration.Create(old, new("old", "http://127.0.0.1:0", "instance-test"), v1);
await using var newApi = ServerAdministration.Create(newer, new("new", "http://127.0.0.1:0", "instance-test"), v2);
await oldApi.StartAsync(); await newApi.StartAsync();
using var instanceHttp = new HttpClient { BaseAddress = new(oldApi.Urls.Single()) };
Check((await instanceHttp.GetAsync("admin/status")).StatusCode == HttpStatusCode.Unauthorized, "实例独立校验管理令牌");
var registration = new InstanceRegistration(v1.Game, v1.Mod, "127.0.0.1", old.Endpoint.Port, oldApi.Urls.Single());
(await http.PutAsJsonAsync("admin/instances/old", registration)).EnsureSuccessStatusCode();
Check((await http.PutAsJsonAsync("admin/instances/wrong", registration with { Host = "8.8.8.8" })).StatusCode == HttpStatusCode.BadRequest, "管理转发拒绝公网目标");
Check((await http.PutAsJsonAsync("admin/instances/duplicate", registration)).StatusCode == HttpStatusCode.Conflict, "拒绝重复版本或地址");
var first = await Connect(gateway.Endpoint, v1, "first");
var second = await Connect(gateway.Endpoint, v1, "second");
await Pump(first.CreateRoomAsync()); await Pump(first.SetJoinableAsync(true));
await Pump(second.JoinRoomAsync(first.State.Room!.Id));
Check(second.State.Room?.Id == first.State.Room.Id, "真实握手、建房与入房通过同一入口");
Check((await http.GetFromJsonAsync<InstanceStatus>("admin/instances/old/status"))?.Players == 2, "网关转发状态查询得到真实人数");
(await http.PutAsJsonAsync("admin/instances/new", new InstanceRegistration(v2.Game, v2.Mod, "127.0.0.1", newer.Endpoint.Port, newApi.Urls.Single()))).EnsureSuccessStatusCode();
var third = await Connect(gateway.Endpoint, v2, "third");
Check(first.IsConnected && third.State.World.All(p => p.Name != "first"), "新增版本不重启网关，版本之间隔离");
using (var restored = new InstanceRegistry(path, "instance-test")) Check(restored.Resolve(v2).Id == "new", "重启后可从文件恢复路由");
first.Disconnect();
await Until(async () => (await old.GetStatusAsync()).Players.Length == 1);
Check(true, "玩家断开会关闭后端连接并释放人数");
bool notice = false; second.NoticeReceived += _ => notice = true;
(await http.PostAsJsonAsync("admin/instances/old/drain", new DrainRequest("release", v2.Mod))).EnsureSuccessStatusCode();
await Until(() => Task.FromResult(notice));
Check(second.IsConnected, "退役转发通知，保留已有玩家连接");
try { await Connect(gateway.Endpoint, v1, "late"); throw new Exception("Expected rejection"); }
catch (NetworkException error) { Check(error.Code == NetworkErrorCode.ServerRetiring, "退役路由拒绝新玩家"); }
second.Disconnect(); await old.Drained.WaitAsync(TimeSpan.FromSeconds(5));
Check(true, "最后一人断开后实例完成退役");
(await http.DeleteAsync("admin/instances/old")).EnsureSuccessStatusCode();
Check(registry.List().Length == 1 && third.IsConnected, "删除旧路由不影响新版本连接");
await newer.StopAsync(); await Until(() => Task.FromResult(!third.IsConnected));
Check(true, "实例断开会同步关闭玩家连接");
(await http.DeleteAsync("admin/instances/new")).EnsureSuccessStatusCode();
await using var last = new Server(new() { Versions = v2 }); await last.StartAsync();
await using var lastApi = ServerAdministration.Create(last, new("last", "http://127.0.0.1:0", "instance-test"), v2);
await lastApi.StartAsync();
(await http.PutAsJsonAsync("admin/instances/last", new InstanceRegistration(v2.Game, v2.Mod, "127.0.0.1", last.Endpoint.Port, lastApi.Urls.Single()))).EnsureSuccessStatusCode();
await using (var temporary = new GatewayServer(new(IPAddress.Loopback, 0), registry))
{ temporary.Start(); await Connect(temporary.Endpoint, v2, "shutdown"); }
await Until(async () => (await last.GetStatusAsync()).Players.Length == 0);
Check(true, "停止网关释放全部后端连接");
foreach (var client in clients) client.Dispose();
await api.StopAsync(); await oldApi.StopAsync(); await newApi.StopAsync(); await lastApi.StopAsync();
Console.WriteLine($"ALL PASS ({count} hosting assertions)");
