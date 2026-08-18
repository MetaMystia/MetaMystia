using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using BepInEx;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

using MetaMystia.UI;

namespace MetaMystia;

public static class Il2CppInteropPatcher
{
    const string ResourceName = "MetaMystia.Patches.Native.Runtime.Il2CppInterop.HarmonySupport.dll";
    const string TargetFileName = "Il2CppInterop.HarmonySupport.dll";

    public static bool Patched { get; private set; }

    public static void TryPatch()
    {
        var targetPath = Path.Combine(Paths.BepInExRootPath, "core", TargetFileName);
        var backupPath = targetPath + ".bak";

        try
        {
            // Clean up previous backup
            if (File.Exists(backupPath))
            {
                try { File.Delete(backupPath); }
                catch { /* still locked from a previous run, ignore */ }
            }

            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream == null)
            {
                Plugin.Instance?.Log.LogWarning($"Embedded {TargetFileName} not found.");
                return;
            }

            using var md5 = MD5.Create();

            var embeddedHash = md5.ComputeHash(stream);
            stream.Position = 0;

            if (File.Exists(targetPath))
            {
                using var fileStream = File.OpenRead(targetPath);
                var existingHash = md5.ComputeHash(fileStream);
                if (Convert.ToHexString(existingHash) == Convert.ToHexString(embeddedHash))
                    return; // already patched
            }

            var embeddedBytes = new byte[stream.Length];
            stream.Read(embeddedBytes, 0, embeddedBytes.Length);

            // Try direct write first; if locked, rename-then-write
            try
            {
                File.WriteAllBytes(targetPath, embeddedBytes);
            }
            catch (IOException)
            {
                File.Move(targetPath, backupPath);
                File.WriteAllBytes(targetPath, embeddedBytes);
            }

            Patched = true;
            Plugin.Instance?.Log.LogInfo($"Patched {TargetFileName} → BepInEx/core (effective on next launch)");
        }
        catch (Exception ex)
        {
            Plugin.Instance?.Log.LogWarning($"Failed to patch {TargetFileName}: {ex.Message}");
        }
    }

    public static void NotifyIfPatched()
    {
        if (!Patched)
            return;

        var message = TextId.Il2CppInteropPatchedRestartRequired.Get();
        if (PluginHost.Instance != null)
            PluginHost.Instance.StartManagedCoroutine(InteropRestartReminderLoop(message));
        else
            InGameConsole.LogAlert(message);
    }

    [HideFromIl2Cpp]
    private static IEnumerator InteropRestartReminderLoop(string message)
    {
        var wait = new WaitForSeconds(3f);
        while (true)
        {
            InGameConsole.LogAlert(message);
            yield return wait;
        }
    }
}
