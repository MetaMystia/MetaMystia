using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using MetaMystia;
using MetaMystia.UI;

namespace SgrYuki;

[AutoLog]
public static partial class MetricsReporter
{
    private const string TrackingSiteId = "13";
    private const string TrackingServiceEndpoint = "https://track.izakaya.cc/api.php";
    private const string MetaMystiaVersionApiUrl = "https://api.izakaya.cc/version/meta-mystia";
    public const string UserAgent = "MetaMystia/1.0 (+https://github.com/MetaMikuAI/MetaMystia)";

    private static string BuildTrackingUrl(string userId, Dictionary<string, string> parameters)
    {
        var baseParams = new Dictionary<string, string>
        {
            ["idsite"] = TrackingSiteId,
            ["rec"] = "1",
            ["_id"] = userId,
            ["uid"] = userId
        };

        foreach (var param in parameters)
        {
            baseParams[param.Key] = param.Value;
        }

        var queryString = string.Join("&", baseParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{TrackingServiceEndpoint}?{queryString}";
    }

    private static readonly System.Net.Http.HttpClient _githubApiClient = new()
    {
        BaseAddress = new Uri("https://api.github.com/"),
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static readonly System.Net.Http.HttpClient _metricsClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static string ReadMachineGuidFromRegistry()
    {
        try
        {
#pragma warning disable CA1416 // 验证平台兼容性
            var value = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null) as string;
#pragma warning restore CA1416 // 验证平台兼容性
            if (!string.IsNullOrEmpty(value)) return value.Trim();
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to read MachineGuid from registry: {ex.Message}");
        }
        return null;
    }

    private static string GetMachineId()
    {
        try
        {
            var computerName = Environment.MachineName;
            var userName = Environment.UserName;

            var combined = $"{computerName}|{userName}";
            return combined;
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to get machine ID: {ex.Message}");
            return null;
        }
    }

    private static string MD5(string input) => Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    private static readonly Lazy<string> _cachedUserId = new(() =>
    {
        var machineGuid = ReadMachineGuidFromRegistry();
        if (!string.IsNullOrEmpty(machineGuid))
        {
            var id = MD5(machineGuid);
            Log.Message($"User ID generated from MachineGuid: {id}");
            return id;
        }

        var machineId = GetMachineId();
        if (!string.IsNullOrEmpty(machineId))
        {
            var id = MD5(machineId);
            Log.Message($"User ID generated from MachineId: {id}");
            return id;
        }

        var fallbackId = Guid.NewGuid().ToString("N");
        Log.Warning($"No stable identifiers found, using random GUID: {fallbackId}");
        return fallbackId;
    });

    private static string GetUserId() => _cachedUserId.Value;

    static MetricsReporter()
    {
        _githubApiClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _githubApiClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _metricsClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _metricsClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    private static async Task<string> GetLatestReleaseTagUsingGithubAsync(
        string owner,
        string repo)
    {
        try
        {
            using var response = await _githubApiClient.GetAsync($"repos/{owner}/{repo}/releases/latest").ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            // 200: {"tag_name":"v0.11.0"}
            if (dict != null && dict.TryGetValue("tag_name", out var tag))
            {
                return tag.ToString().TrimStart('v');
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to get latest release from GitHub: {ex.Message}");
        }
        return null;
    }

    private static async Task<string> GetLatestReleaseTagAsync()
    {
        try
        {
            using var response = await _metricsClient.GetAsync(MetaMystiaVersionApiUrl).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            // 200: {"dll":"0.11.0","zip":"0.4.5"}
            if (dict != null && dict.TryGetValue("dll", out var ver))
            {
                return ver.ToString();
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to get latest release: {ex.Message}");
        }
        return null;
    }

    public static Task<string> GetPluginLatestTagAsync(bool useGithub = false)
    {
        if (useGithub)
        {
            return GetLatestReleaseTagUsingGithubAsync("MetaMikuAI", "MetaMystia");
        }
        return GetLatestReleaseTagAsync();
    }

    private static Task ReportAsync(Func<Task> reportAction, string actionName)
    {
        return Task.Run(async () =>
        {
            try
            {
                await reportAction().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning($"[{actionName}] Failed: {ex.Message}");
            }
        });
    }

    private static async Task<bool> SendTrackingRequestAsync(string url, string actionName)
    {
        try
        {
            using var response = await _metricsClient.GetAsync(url).ConfigureAwait(false);
            var success = response.IsSuccessStatusCode;

            if (success)
            {
                Log.Info($"[{actionName}] Success");
            }
            else
            {
                Log.Warning($"[{actionName}] Failed with status: {response.StatusCode}");
            }

            return success;
        }
        catch (Exception ex)
        {
            Log.Warning($"[{actionName}] Exception: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 上报自定义事件
    /// </summary>
    /// <param name="category">事件类别</param>
    /// <param name="action">事件动作</param>
    /// <param name="name">事件名称（可选）</param>
    public static Task ReportEvent(string category, string action, string name = null)
    {
        return ReportAsync(async () =>
        {
            var userId = GetUserId();
            var parameters = new Dictionary<string, string>
            {
                ["ca"] = "1",
                ["e_c"] = category,
                ["e_a"] = action
            };

            if (!string.IsNullOrEmpty(name))
                parameters["e_n"] = name;

            var url = BuildTrackingUrl(userId, parameters);
            await SendTrackingRequestAsync(url, $"ReportEvent({category}/{action}{(name == null ? "" : $"/{name}")}").ConfigureAwait(false);
        }, "ReportEvent");
    }

    public static Task SendHeartbeat()
    {
        return ReportAsync(async () =>
        {
            var userId = GetUserId();
            var url = BuildTrackingUrl(userId, new Dictionary<string, string>
            {
                ["ping"] = "1"
            });

            await SendTrackingRequestAsync(url, "Heartbeat").ConfigureAwait(false);
        }, "SendHeartbeat");
    }

    public static void OnEnterMainScene()
    {
        CommandScheduler.RunInBackGround(async () =>
            {
                try
                {
                    var currentVer = Plugin.ModVersion;
                    var latestVer = await GetPluginLatestTagAsync().ConfigureAwait(false);

                    Log.Message($"当前 Mod 版本为 {currentVer}，最新版为 {latestVer}");

                    if (string.IsNullOrEmpty(latestVer))
                    {
                        InGameConsole.ShowPassiveFromAnyThread(TextId.ModVersionUnavailable.Get(currentVer));
                    }
                    else if (currentVer.Equals(latestVer, StringComparison.Ordinal))
                    {
                        InGameConsole.ShowPassiveFromAnyThread(TextId.ModVersionLatest.Get(currentVer));
                    }
                    else
                    {
                        InGameConsole.ShowPassiveFromAnyThread(TextId.ModVersionOutdated.Get(currentVer, latestVer));
                    }

                    if (!PluginManager.DEBUG)
                    {
                        _ = ReportEvent("Client", "Run", currentVer);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Cannot get plugin version, {ex.Message}, {ex.StackTrace}");
                }
            });
    }
}
