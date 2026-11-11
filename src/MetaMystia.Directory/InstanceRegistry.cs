using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using MetaMystia.Discovery;

namespace MetaMystia.Directory;

public sealed class InstanceRegistry : IDisposable
{
    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim gate = new(1);
    private readonly HttpClient http;
    private readonly string path;
    private Dictionary<string, InstanceRecord> records;

    public InstanceRegistry(string path, string instanceToken)
    {
        this.path = Path.GetFullPath(path);
        records = File.Exists(path)
            ? (JsonSerializer.Deserialize<InstanceRecord[]>(File.ReadAllText(path), json) ?? throw new InvalidDataException("Invalid registry"))
                .ToDictionary(r => r.Id, r => r with { Observed = null, LastSeen = null })
            : [];
        http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(3) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", instanceToken);
    }

    public InstanceRecord? Get(string id) => Volatile.Read(ref records).GetValueOrDefault(id);
    public InstanceRecord[] List() => Volatile.Read(ref records).Values.ToArray();
    public ServerCatalog Catalog() => new(List().Where(r => r.State != "staged").Select(r =>
    {
        bool ready = r.State == "active" && r.Observed?.State == "running" &&
            r.LastSeen > DateTimeOffset.UtcNow.AddSeconds(-15);
        string state = r.State switch { "draining" => "retiring", "stopped" => "retired", _ => ready ? "ready" : "unavailable" };
        return new ServerListing(r.Id, r.Registration.GameVersion, r.Registration.ModVersion, state,
            ready ? r.Registration.Host : null, ready ? r.Registration.Port : null, r.TargetModVersion);
    }).ToArray());

    public async Task RegisterAsync(string id, InstanceRegistration registration, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !char.IsLetterOrDigit(id[0]) ||
            id.Any(c => c is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or '.')))
            throw new ArgumentException("Invalid instance ID");
        if (string.IsNullOrWhiteSpace(registration.GameVersion) || registration.GameVersion.Length > 64 ||
            string.IsNullOrWhiteSpace(registration.ModVersion) || registration.ModVersion.Length > 64 ||
            Uri.CheckHostName(registration.Host) == UriHostNameType.Unknown || registration.Port is < 1 or > 65535 ||
            !Uri.TryCreate(registration.AdminUrl, UriKind.Absolute, out var admin) ||
            (admin.Scheme != "https" && !(admin.Scheme == "http" && admin.IsLoopback)) ||
            admin.AbsolutePath != "/" || admin.Query.Length != 0 || admin.UserInfo.Length != 0 || admin.Fragment.Length != 0)
            throw new ArgumentException("Invalid registration");
        await gate.WaitAsync(token);
        try
        {
            var existing = Get(id);
            if (existing != null && existing.Registration != registration) throw new InvalidOperationException("Instance ID is immutable");
            if (List().Any(r => r.Id != id && r.State != "stopped" &&
                (r.Registration.AdminUrl.TrimEnd('/') == registration.AdminUrl.TrimEnd('/') ||
                 (r.Registration.Host == registration.Host && r.Registration.Port == registration.Port))))
                throw new InvalidOperationException("Endpoint already registered");
            var record = existing ?? new InstanceRecord(id, registration);
            var status = await StatusAsync(record, token);
            await SaveAsync(new(records) { [id] = record with { Observed = status, LastSeen = DateTimeOffset.UtcNow } }, token);
        }
        finally { gate.Release(); }
    }

    public async Task ActivateAsync(string id, ActivationRequest request, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var current = Get(id) ?? throw new ArgumentException("Unknown instance");
            if (current.State is not ("staged" or "active")) throw new InvalidOperationException("Instance is retired");
            if (request.Replaces == null || request.Replaces.Contains(id) || request.Replaces.Length != request.Replaces.Distinct().Count())
                throw new ArgumentException("Invalid replacement list");
            var status = await StatusAsync(current, token);
            if (status.State != "running") throw new InvalidOperationException("Instance is not ready");
            var next = new Dictionary<string, InstanceRecord>(records);
            foreach (var oldId in request.Replaces)
            {
                var old = Get(oldId) ?? throw new ArgumentException("Unknown replaced instance");
                if (old.State == "staged" || (old.State == "draining" && old.NoticeId != id))
                    throw new InvalidOperationException("Replacement conflicts with another deployment");
                if (old.Registration.GameVersion == current.Registration.GameVersion && old.Registration.ModVersion == current.Registration.ModVersion && old.State != "stopped")
                    throw new InvalidOperationException("Publish a new mod version before replacing an active instance");
                if (old.State != "stopped") next[oldId] = old with { State = "draining", NoticeId = id, TargetModVersion = current.Registration.ModVersion };
            }
            if (next.Values.Any(r => r.Id != id && r.State == "active" &&
                r.Registration.GameVersion == current.Registration.GameVersion && r.Registration.ModVersion == current.Registration.ModVersion))
                throw new InvalidOperationException("Version already active");
            next[id] = current with { State = "active", Observed = status, LastSeen = DateTimeOffset.UtcNow };
            await SaveAsync(next, token);
        }
        finally { gate.Release(); }
    }

    public async Task MarkStoppedAsync(string id, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var record = Get(id) ?? throw new ArgumentException("Unknown instance");
            if (record.State is not ("draining" or "stopped")) throw new InvalidOperationException("Instance is not retiring");
            await SaveAsync(new(records) { [id] = record with { State = "stopped", Observed = null } }, token);
        }
        finally { gate.Release(); }
    }

    public async Task ReconcileAsync(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var next = new Dictionary<string, InstanceRecord>(records);
            foreach (var record in records.Values.Where(r => r.State != "stopped"))
            {
                try
                {
                    var status = await StatusAsync(record, token);
                    var updated = record with { Observed = status, LastSeen = DateTimeOffset.UtcNow };
                    if (record.State == "draining")
                    {
                        using var response = await http.PostAsJsonAsync(Endpoint(record, "admin/drain"),
                            new DrainRequest(record.NoticeId!, record.TargetModVersion!), token);
                        response.EnsureSuccessStatusCode();
                        updated = updated with { DrainConfirmed = true };
                    }
                    next[record.Id] = updated;
                }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException)
                {
                    token.ThrowIfCancellationRequested();
                    next[record.Id] = record with { Observed = null };
                }
            }
            await SaveAsync(next, token);
        }
        finally { gate.Release(); }
    }

    private async Task<InstanceStatus> StatusAsync(InstanceRecord record, CancellationToken token)
    {
        var status = await http.GetFromJsonAsync<InstanceStatus>(Endpoint(record, "admin/status"), token)
            ?? throw new InvalidDataException("Empty instance status");
        if (status.InstanceId != record.Id || status.GameVersion != record.Registration.GameVersion ||
            status.ModVersion != record.Registration.ModVersion || status.Port != record.Registration.Port)
            throw new InvalidDataException("Instance identity mismatch");
        return status;
    }

    private static Uri Endpoint(InstanceRecord record, string route) => new(new Uri(record.Registration.AdminUrl.TrimEnd('/') + "/"), route);

    private async Task SaveAsync(Dictionary<string, InstanceRecord> next, CancellationToken token)
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(next.Values.ToArray(), json), token);
        File.Move(path + ".tmp", path, true);
        Volatile.Write(ref records, next);
    }

    public void Dispose() { http.Dispose(); gate.Dispose(); }
}
