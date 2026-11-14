using System.Net.Http.Headers;
using System.Net.Http.Json;

using MetaMystia.Discovery;
using MetaMystia.Network;

namespace MetaMystia.Gateway;

public sealed class RouteCache : IDisposable
{
    private sealed record Snapshot(ServerListing[] Servers, long Updated);
    private readonly HttpClient http;
    private Snapshot? snapshot;
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromSeconds(5);

    public RouteCache(string directoryUrl, string token)
    {
        var address = new Uri(directoryUrl.TrimEnd('/') + "/");
        if (address.Scheme != "https" && !(address.Scheme == "http" && address.IsLoopback))
            throw new ArgumentException("Directory requires HTTPS or loopback HTTP");
        http = new(new HttpClientHandler { AllowAutoRedirect = false })
        { BaseAddress = address, Timeout = TimeSpan.FromSeconds(2) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task RefreshAsync(CancellationToken token)
    {
        var catalog = await http.GetFromJsonAsync<ServerCatalog>("internal/routes", token)
            ?? throw new InvalidDataException("Empty routes");
        if (catalog.Servers == null) throw new InvalidDataException("Missing routes");
        Volatile.Write(ref snapshot, new(catalog.Servers, Environment.TickCount64));
    }

    public ServerListing Resolve(Versions versions)
    {
        var current = Volatile.Read(ref snapshot);
        if (current == null || Environment.TickCount64 - current.Updated > Lifetime.TotalMilliseconds)
            throw new NetworkException(NetworkErrorCode.DirectoryUnavailable);
        var matches = current.Servers.Where(s => s.GameVersion == versions.Game && s.ModVersion == versions.Mod).ToArray();
        var ready = matches.Where(s => s.State == "ready").ToArray();
        if (ready.Length == 1 && !string.IsNullOrWhiteSpace(ready[0].Host) && ready[0].Port is > 0 and <= 65535)
            return ready[0];
        if (ready.Length == 0 && matches.Length > 0 && matches.All(s => s.State is "retiring" or "retired"))
            throw new NetworkException(new NetworkError { Code = NetworkErrorCode.ServerRetiring, ModVersion = matches[^1].TargetModVersion });
        throw new NetworkException(NetworkErrorCode.VersionUnavailable);
    }

    public void Dispose() => http.Dispose();
}
