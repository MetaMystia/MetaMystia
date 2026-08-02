using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

using MetaMystia.ResourceEx.Models;
using MetaMystia.UI;

namespace MetaMystia.ResourceEx.AssetManagement;

/// <summary>
/// Loads resource packages (ZIP files) from disk and parses their configurations
/// </summary>
[AutoLog]
public static partial class ResourcePackageLoader
{
    /// <summary>
    /// Scans the resource directory and loads all valid resource packages
    /// </summary>
    public static List<LoadedResourcePackage> LoadAllPackages(string resourceRoot, out List<(string packageName, string reason)> rejected)
    {
        rejected = new List<(string, string)>();

        if (!Directory.Exists(resourceRoot))
        {
            Directory.CreateDirectory(resourceRoot);
            Log.LogInfo($"Created ResourceEx directory at {resourceRoot}");
            return new List<LoadedResourcePackage>();
        }

        var zipFiles = Directory.GetFiles(resourceRoot, "*.zip");
        var candidates = new List<PackageCandidate>();

        foreach (var zipPath in zipFiles)
        {
            string packageName = Path.GetFileNameWithoutExtension(zipPath);
            Log.LogInfo($"Scanning resource package: {packageName} from {zipPath}");

            try
            {
                var candidate = LoadPackageCandidate(zipPath, packageName);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to load resource package {packageName}: {e.Message}");
                rejected.Add((packageName, e.Message));
            }
        }

        // Validate ID ranges and signatures
        bool enforceSignature = ConfigManager.SignatureCheck.Value;
        if (!enforceSignature)
        {
            Log.LogWarning("[ResourceEx] Signature verification is DISABLED by config.");
            InGameConsole.LogDeferred(() => TextId.SignatureVerificationDisabled.Get());
        }

        var validatedCandidates = new List<PackageCandidate>();
        foreach (var candidate in candidates)
        {
            var validation = IdRangeValidator.Validate(candidate.Config, candidate.PackageName, enforceSignature);

            foreach (var warning in validation.Warnings)
                Log.LogWarning(warning);

            if (!validation.IsValid)
            {
                foreach (var error in validation.Errors)
                    Log.LogError(error);

                Log.LogError($"[{candidate.PackageName}] Rejected: ID range validation failed.");
                InGameConsole.LogDeferred(() => TextId.ResourcePackageValidationFailed.Get(candidate.PackageName));
                rejected.Add((candidate.PackageName, "ID range validation failed"));
                continue;
            }

            validatedCandidates.Add(candidate);
        }

        // Resolve version conflicts
        var resolvedPackages = ResolveVersionConflicts(validatedCandidates);

        // Convert to final loaded packages
        var result = new List<LoadedResourcePackage>();
        foreach (var candidate in resolvedPackages)
        {
            try
            {
                var package = CreateLoadedPackage(candidate);
                result.Add(package);
                LogPackageInfo(candidate);
            }
            catch (Exception ex)
            {
                Log.LogError($"[{candidate.PackageName}] Failed to finalize package: {ex.Message}");
                rejected.Add((candidate.PackageName, ex.Message));
            }
        }

        return result;
    }

    private static PackageCandidate LoadPackageCandidate(string zipPath, string packageName)
    {
        // Read entire ZIP file into memory (one-time IO)
        byte[] zipBytes = File.ReadAllBytes(zipPath);

        using (var memoryStream = new MemoryStream(zipBytes))
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
        {
            // Find ResourceEx.json
            ZipArchiveEntry configEntry = null;
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith("ResourceEx.json", StringComparison.OrdinalIgnoreCase))
                {
                    // Prefer shorter path (root level)
                    if (configEntry == null || entry.FullName.Length < configEntry.FullName.Length)
                    {
                        configEntry = entry;
                    }
                }
            }

            if (configEntry == null)
            {
                Log.LogWarning($"[{packageName}] ResourceEx.json not found in zip.");
                return null;
            }

            // Determine internal prefix
            string entryName = configEntry.FullName;
            string internalPrefix = entryName.EndsWith("ResourceEx.json", StringComparison.OrdinalIgnoreCase)
                ? entryName.Substring(0, entryName.Length - "ResourceEx.json".Length)
                : "";

            // Parse JSON config
            string jsonString;
            using (var stream = configEntry.Open())
            using (var reader = new StreamReader(stream))
            {
                jsonString = reader.ReadToEnd();
            }

            var options = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

            var config = JsonSerializer.Deserialize<ResourceConfig>(jsonString, options);

            return new PackageCandidate
            {
                ZipPath = zipPath,
                PackageName = packageName,
                ZipBytes = zipBytes,
                InternalPrefix = internalPrefix,
                Config = config
            };
        }
    }

    private static List<PackageCandidate> ResolveVersionConflicts(List<PackageCandidate> candidates)
    {
        var finalPacks = candidates
            .Where(c => string.IsNullOrEmpty(c.Config?.packInfo?.label))
            .ToList();

        var resolvedPacks = candidates
            .Where(c => !string.IsNullOrEmpty(c.Config?.packInfo?.label))
            .GroupBy(c => c.Config.packInfo.label)
            .Select(group =>
            {
                var sorted = group
                    .Select(c => new { Candidate = c, Version = ParseVersion(c.Config.packInfo.version) })
                    .OrderByDescending(x => x.Version)
                    .ToList();

                var winner = sorted.First();

                if (sorted.Count > 1)
                {
                    Log.LogWarning($"[ResourceEx] Label Conflict: '{group.Key}'. " +
                        $"Selected '{winner.Candidate.PackageName}' (v{winner.Version}) over others: " +
                        $"{string.Join(", ", sorted.Skip(1).Select(s => s.Candidate.PackageName))}");
                }

                return winner.Candidate;
            });

        finalPacks.AddRange(resolvedPacks);
        return finalPacks;
    }

    private static LoadedResourcePackage CreateLoadedPackage(PackageCandidate candidate)
    {
        var packageLabel = !string.IsNullOrWhiteSpace(candidate.Config?.packInfo?.label)
            ? candidate.Config.packInfo.label
            : candidate.PackageName;
        if (!RexUri.IsValidPackageName(packageLabel))
        {
            Log.LogWarning($"[{candidate.PackageName}] Invalid pack label '{packageLabel}' for rex URI. Falling back to zip file name.");
            packageLabel = candidate.PackageName;
        }

        var resourcePackage = new ResourcePackage(
            candidate.ZipPath,
            candidate.InternalPrefix,
            candidate.ZipBytes);

        return new LoadedResourcePackage
        {
            PackageName = candidate.PackageName,
            PackageLabel = packageLabel,
            Config = candidate.Config,
            AssetPackage = resourcePackage
        };
    }

    private static void LogPackageInfo(PackageCandidate candidate)
    {
        if (candidate.Config?.packInfo != null)
        {
            var info = candidate.Config.packInfo;
            string authors = info.authors != null ? string.Join(", ", info.authors) : "Unknown";
            Log.LogMessage($"Loaded Resource Pack: {info.name} [{info.label}] v{info.version} by {authors}");

            InGameConsole.LogDeferred(() => TextId.ResourcePackageLoaded.Get(info.name, info.label, info.version));
        }
        else
        {
            Log.LogInfo($"Loading resource package: {candidate.PackageName} from {candidate.ZipPath}");
        }

        Log.LogInfo($"[{candidate.PackageName}] ZIP size: {candidate.ZipBytes.Length / 1024} KB");
    }

    private static Version ParseVersion(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return new Version(0, 0, 0);

        return Version.TryParse(versionString, out var result)
            ? result
            : new Version(0, 0, 0);
    }

    private class PackageCandidate
    {
        public string ZipPath { get; set; }
        public string PackageName { get; set; }
        public byte[] ZipBytes { get; set; }
        public string InternalPrefix { get; set; }
        public ResourceConfig Config { get; set; }
    }
}

/// <summary>
/// Represents a fully loaded resource package with parsed config and asset access
/// </summary>
public class LoadedResourcePackage
{
    public string PackageName { get; set; }
    public string PackageLabel { get; set; }
    public ResourceConfig Config { get; set; }
    public ResourcePackage AssetPackage { get; set; }
}
