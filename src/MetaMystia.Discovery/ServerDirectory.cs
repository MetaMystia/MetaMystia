using System.Net.Http.Json;

namespace MetaMystia.Discovery;

public static class ServerDirectory
{
    private static readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 256 * 1024 };

    public static async Task<ServerListing?> ResolveAsync(string url, string gameVersion, string modVersion, CancellationToken token = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
            throw new ArgumentException("Directory URL requires HTTPS (HTTP is allowed on loopback)");
        using var response = await http.GetAsync(uri, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var catalog = await response.Content.ReadFromJsonAsync<ServerCatalog>(cancellationToken: token).ConfigureAwait(false)
            ?? throw new InvalidDataException("Empty server directory");
        if (catalog.Servers == null || catalog.Servers.Length > 1024) throw new InvalidDataException("Invalid server directory");
        var matches = catalog.Servers.Where(s => s.GameVersion == gameVersion && s.ModVersion == modVersion).ToArray();
        var ready = matches.Where(s => s.State == "ready").ToArray();
        if (ready.Length > 1) throw new InvalidDataException("Ambiguous server directory");
        var result = ready.SingleOrDefault() ?? matches.FirstOrDefault();
        if (result?.State == "ready" && (string.IsNullOrWhiteSpace(result.Host) ||
            Uri.CheckHostName(result.Host) == UriHostNameType.Unknown || result.Port is not (>= 1 and <= 65535)))
            throw new InvalidDataException("Invalid server endpoint");
        return result;
    }
}
