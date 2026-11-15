using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Win32;

namespace MetaMystia.Services;

public sealed class IzakayaService
{
    private const string TrackingSiteId = "13";
    private const string TrackingServiceEndpoint = "https://track.izakaya.cc/api.php";
    private const string MetaMystiaVersionApiUrl = "https://api.izakaya.cc/version/meta-mystia";

    private readonly HttpClient http;
    private readonly Lazy<string> userId;

    public IzakayaService(string modVersion, Action<string>? onWarning = null)
    {
        http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"MetaMystia/{modVersion} (+https://github.com/MetaMystia/MetaMystia)");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        userId = new(() => CreateUserId(onWarning));
    }

    public async Task<string?> GetLatestVersionAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(
            MetaMystiaVersionApiUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<VersionResponse>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return payload?.Dll;
    }

    public async Task ReportEventAsync(string category, string action, string? name = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["ca"] = "1",
            ["e_c"] = category,
            ["e_a"] = action
        };
        if (!string.IsNullOrEmpty(name)) parameters["e_n"] = name;
        await SendTrackingRequestAsync(BuildTrackingUrl(parameters), cancellationToken).ConfigureAwait(false);
    }

    public async Task SendHeartbeatAsync(CancellationToken cancellationToken = default) =>
        await SendTrackingRequestAsync(BuildTrackingUrl(new Dictionary<string, string> { ["ping"] = "1" }), cancellationToken)
            .ConfigureAwait(false);

    private Uri BuildTrackingUrl(Dictionary<string, string> parameters)
    {
        var visitorId = userId.Value;
        var query = new Dictionary<string, string>
        {
            ["idsite"] = TrackingSiteId,
            ["rec"] = "1",
            ["_id"] = visitorId,
            ["uid"] = visitorId
        };
        foreach (var parameter in parameters) query[parameter.Key] = parameter.Value;

        var queryString = string.Join("&", query.Select(pair =>
            $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
        return new Uri($"{TrackingServiceEndpoint}?{queryString}");
    }

    private async Task SendTrackingRequestAsync(Uri url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private static string CreateUserId(Action<string>? onWarning)
    {
        try
        {
            if (OperatingSystem.IsWindows()
                && Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null) is string machineGuid
                && !string.IsNullOrWhiteSpace(machineGuid))
                return Hash(machineGuid.Trim());
        }
        catch (Exception error) when (error is PlatformNotSupportedException or SecurityException or UnauthorizedAccessException or IOException)
        {
        }
        onWarning?.Invoke("MachineGuid unavailable, using a random session ID");
        return Guid.NewGuid().ToString("N");
    }

    private static string Hash(string value) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record VersionResponse
    {
        [JsonPropertyName("dll")]
        public string? Dll { get; init; }
    }
}
