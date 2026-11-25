using System;
using System.Collections;
using System.Threading.Tasks;

using UnityEngine;

using MetaMystia.Services;
using MetaMystia.UI;
using SgrYuki;

namespace MetaMystia;

public static class IzakayaReporter
{
    private const int HeartbeatIntervalSeconds = 2 * 60;
    private const string LogScope = "[Izakaya]";
    private static readonly bool isHeartbeatEnabled = true;
    private static readonly bool isRunEventEnabled = true;
    private static readonly bool isOtherEventEnabled = false;
    private static readonly IzakayaService izakayaService =
        new(Plugin.ModVersion, warning => Log.Warning($"{LogScope}[Identity] {warning}"));
    private static bool heartbeatStarted;

    public static void OnEnterMainScene()
    {
        if (isHeartbeatEnabled) StartHeartbeat();
        CommandScheduler.RunInBackGround(async () =>
        {
            try
            {
                var currentVer = Plugin.ModVersion;
                string latestVer = null;
                try
                {
                    latestVer = await izakayaService.GetLatestVersionAsync().ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    Log.Warning($"{LogScope}[Version] request=failed error={error.Message}");
                }

                Log.Message($"{LogScope}[Version] current={currentVer} latest={latestVer ?? "unavailable"}");

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

                await ReportRunEventAsync(currentVer).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                Log.Error($"{LogScope}[Version] request=unhandled error={error}");
            }
        });
    }

    private static void StartHeartbeat()
    {
        if (heartbeatStarted || PluginManager.DEBUG) return;
        if (PluginHost.Instance == null)
        {
            PluginManager.RunOnMainThread(StartHeartbeat);
            return;
        }
        heartbeatStarted = true;
        PluginHost.Instance.StartManagedCoroutine(SendHeartbeats());
    }

    private static IEnumerator SendHeartbeats()
    {
        var interval = new WaitForSecondsRealtime(HeartbeatIntervalSeconds);
        while (true)
        {
            _ = SendHeartbeatAsync();
            yield return interval;
        }
    }

    public static async Task ReportOtherEventAsync(string category, string action, string name = null)
    {
        if (PluginManager.DEBUG || !isOtherEventEnabled) return;
        await ReportEventAsync(category, action, name).ConfigureAwait(false);
    }

    private static Task ReportRunEventAsync(string version) =>
        PluginManager.DEBUG || !isRunEventEnabled
            ? Task.CompletedTask
            : ReportEventAsync("Client", "Run", version);

    private static async Task ReportEventAsync(string category, string action, string name)
    {
        var details = string.IsNullOrEmpty(name)
            ? $"category={category} action={action}"
            : $"category={category} action={action} name={name}";
        try
        {
            await izakayaService.ReportEventAsync(category, action, name).ConfigureAwait(false);
            Log.Info($"{LogScope}[Event] {details} result=success");
        }
        catch (Exception error)
        {
            Log.Warning($"{LogScope}[Event] {details} result=failed error={error.Message}");
        }
    }

    private static async Task SendHeartbeatAsync()
    {
        try
        {
            await izakayaService.SendHeartbeatAsync().ConfigureAwait(false);
            Log.Info($"{LogScope}[Heartbeat] result=success");
        }
        catch (Exception error)
        {
            Log.Warning($"{LogScope}[Heartbeat] result=failed error={error.Message}");
        }
    }
}
