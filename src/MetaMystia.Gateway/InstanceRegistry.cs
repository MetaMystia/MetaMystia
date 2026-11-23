using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using MetaMystia.Hosting;
using MetaMystia.Network;

namespace MetaMystia.Gateway;

public sealed record InstanceRegistration(string GameVersion, string ModVersion, string Host, int Port, string AdminUrl);
public sealed record ForwardResult(int StatusCode, byte[] Body, string? ContentType);
public sealed record InstanceRecord(string Id, InstanceRegistration Registration, string State = "active", string? TargetModVersion = null);

public sealed class InstanceRegistry : IDisposable
{
    private readonly SemaphoreSlim gate = new(1);
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(3) };
    private readonly string path;
    private readonly EventLog? events;
    private Dictionary<string, InstanceRecord> records;

    public InstanceRegistry(string path, string instanceToken, EventLog? events = null)
    {
        this.path = Path.GetFullPath(path);
        this.events = events;
        records = JsonFile.Read<InstanceRecord>(this.path).ToDictionary(r => r.Id);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", instanceToken);
    }

    public InstanceRecord[] List() => Volatile.Read(ref records).Values.ToArray();
    private InstanceRecord Get(string id) => Volatile.Read(ref records).GetValueOrDefault(id) ?? throw new KeyNotFoundException();
    public InstanceRecord Resolve(Versions versions)
    {
        var matching = List().Where(r => r.Registration.GameVersion == versions.Game && r.Registration.ModVersion == versions.Mod).ToArray();
        var active = matching.SingleOrDefault(r => r.State == "active");
        if (active != null) return active;
        if (matching.Length > 0)
            throw new NetworkException(new NetworkError { Code = NetworkErrorCode.ServerRetiring, ModVersion = matching[^1].TargetModVersion });
        throw new NetworkException(NetworkErrorCode.VersionUnavailable);
    }

    public async Task RegisterAsync(string id, InstanceRegistration registration, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !char.IsLetterOrDigit(id[0]) ||
            id.Any(c => c is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or '.')) ||
            string.IsNullOrWhiteSpace(registration.GameVersion) || registration.GameVersion.Length > 64 ||
            string.IsNullOrWhiteSpace(registration.ModVersion) || registration.ModVersion.Length > 64 ||
            !IPAddress.TryParse(registration.Host, out var address) || !IPAddress.IsLoopback(address) || registration.Port is < 1 or > 65535 ||
            !Uri.TryCreate(registration.AdminUrl, UriKind.Absolute, out var admin) || admin.Scheme != "http" ||
            !IPAddress.TryParse(admin.Host.Trim('[', ']'), out var adminAddress) || !IPAddress.IsLoopback(adminAddress) ||
            admin.AbsolutePath != "/" || admin.Query.Length != 0 || admin.UserInfo.Length != 0 || admin.Fragment.Length != 0)
            throw new ArgumentException("Invalid instance registration");
        await gate.WaitAsync(token);
        try
        {
            var existing = records.GetValueOrDefault(id);
            if (existing != null && (existing.Registration != registration || existing.State != "active"))
                throw new InvalidOperationException("Instance ID cannot be reused");
            if (List().Any(r => r.Id != id && (r.Registration.AdminUrl.TrimEnd('/') == registration.AdminUrl.TrimEnd('/') ||
                (r.Registration.Host == registration.Host && r.Registration.Port == registration.Port) ||
                (r.State == "active" && r.Registration.GameVersion == registration.GameVersion && r.Registration.ModVersion == registration.ModVersion))))
                throw new InvalidOperationException("Endpoint or version already registered");
            var record = new InstanceRecord(id, registration);
            var status = await StatusAsync(record, token);
            if (status.State != "running") throw new InvalidOperationException("Instance is not running");
            Save(new(records) { [id] = record });
            if (existing == null) events?.Add("registered", record);
        }
        finally { gate.Release(); }
    }

    public Task<InstanceStatus> StatusAsync(string id, CancellationToken token) => StatusAsync(Get(id), token);
    public async Task<InstanceStatus> StatusAsync(InstanceRecord record, CancellationToken token)
    {
        var status = await http.GetFromJsonAsync<InstanceStatus>(record.Registration.AdminUrl.TrimEnd('/') + "/admin/status", token)
            ?? throw new InvalidDataException("Empty instance status");
        if (status.InstanceId != record.Id || status.GameVersion != record.Registration.GameVersion ||
            status.ModVersion != record.Registration.ModVersion || status.Port != record.Registration.Port)
            throw new InvalidDataException("Instance identity mismatch");
        return status;
    }

    /// <summary>先确认实例身份，再把白名单路径原样转发；旧版实例没有该接口时返回 501。</summary>
    public async Task<ForwardResult> ForwardAsync(string id, HttpMethod method, string path, byte[]? body, CancellationToken token)
    {
        var record = Get(id);
        await StatusAsync(record, token);
        using var request = new HttpRequestMessage(method, record.Registration.AdminUrl.TrimEnd('/') + path);
        if (body != null) request.Content = new ByteArrayContent(body) { Headers = { ContentType = new("application/json") } };
        using var response = await http.SendAsync(request, token);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new HttpRequestException("Instance rejected the token");
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
            return new(501, [], null);
        return new((int)response.StatusCode, await response.Content.ReadAsByteArrayAsync(token), response.Content.Headers.ContentType?.ToString());
    }

    public async Task DrainAsync(string id, DrainRequest request, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.NoticeId) || request.NoticeId.Length > 128 ||
            string.IsNullOrWhiteSpace(request.TargetModVersion) || request.TargetModVersion.Length > 64)
            throw new ArgumentException("Invalid retirement request");
        await gate.WaitAsync(token);
        try
        {
            var record = Get(id);
            var draining = record with { State = "draining", TargetModVersion = request.TargetModVersion };
            Save(new(records) { [id] = draining });
            if (draining != record) events?.Add("draining", draining);
            using var response = await http.PostAsJsonAsync(record.Registration.AdminUrl.TrimEnd('/') + "/admin/drain", request, token);
            response.EnsureSuccessStatusCode();
        }
        finally { gate.Release(); }
    }

    public async Task DeleteAsync(string id, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var next = new Dictionary<string, InstanceRecord>(records);
            if (!next.Remove(id, out var record)) throw new KeyNotFoundException();
            Save(next);
            events?.Add("removed", record);
        }
        finally { gate.Release(); }
    }

    private void Save(Dictionary<string, InstanceRecord> next)
    {
        JsonFile.Write(path, next.Values.ToArray());
        Volatile.Write(ref records, next);
    }

    public void Dispose() { http.Dispose(); gate.Dispose(); }
}
