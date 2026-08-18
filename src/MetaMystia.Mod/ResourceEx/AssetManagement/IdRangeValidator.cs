using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
#pragma warning disable CA1416

using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.AssetManagement;

/// <summary>
/// Validates resource package ID ranges and RSA-2048 (SHA-256) signatures.
///
/// ID range rules:
///   &lt; 9000               — forbidden (reserved for game)
///   9000 ~ 1073741823     — managed range, requires valid RSA signature
///   1073741824 ~ 2147483647 — unmanaged range, free to use without signature
/// </summary>
[AutoLog]
public static partial class IdRangeValidator
{
    public const int GameIdMax = 8999;
    public const int ManagedIdMin = 9000;
    public const int ManagedIdMax = 1073741823;
    public const int UnmanagedIdMin = 1073741824;
    public const int UnmanagedIdMax = 2147483647;

    private const string PublicKeyResourceName = "MetaMystia.ResourceEx.AssetManagement.public.pem";

    private static RSA _publicKey;

    /// <summary>
    /// Gets the lazily-initialized RSA public key instance.
    /// </summary>
    private static RSA GetPublicKey()
    {
        if (_publicKey != null)
            return _publicKey;

        var pem = LoadEmbeddedPublicKey();

        // Use explicit PROV_RSA_AES (24) for SHA256 support within CSP
        var cspParams = new CspParameters();
        cspParams.ProviderType = 24;
        _publicKey = new RSACryptoServiceProvider(2048, cspParams);

        _publicKey.ImportSubjectPublicKeyInfo(
            Convert.FromBase64String(ExtractBase64FromPem(pem)),
            out _);
        return _publicKey;
    }

    /// <summary>
    /// Loads the PEM public key from the embedded resource.
    /// </summary>
    private static string LoadEmbeddedPublicKey()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(PublicKeyResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{PublicKeyResourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Strips PEM headers/footers and whitespace, returning raw Base64 content.
    /// </summary>
    private static string ExtractBase64FromPem(string pem)
    {
        var sb = new StringBuilder();
        bool inside = false;
        foreach (var rawLine in pem.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("-----BEGIN"))
            {
                inside = true;
                continue;
            }
            if (line.StartsWith("-----END"))
            {
                inside = false;
                continue;
            }
            if (inside && line.Length > 0)
                sb.Append(line);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Verifies the RSA-2048 / SHA-256 signature for "&lt;label&gt;:&lt;start&gt;-&lt;end&gt;".
    /// </summary>
    public static bool VerifySignature(string label, int start, int end, string signatureBase64)
    {
        try
        {
            var message = Encoding.UTF8.GetBytes($"{label}:{start}-{end}");
            var signature = Convert.FromBase64String(signatureBase64);
            return GetPublicKey().VerifyData(message, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            Log.LogError($"[IdRange] Signature verification threw: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Result of validating a resource package's ID range.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();

        public void AddError(string msg) { Errors.Add(msg); IsValid = false; }
        public void AddWarning(string msg) { Warnings.Add(msg); }
    }

    /// <summary>
    /// Validates a resource package's ID declarations against the ID range policy.
    /// Returns a <see cref="ValidationResult"/> with any errors/warnings.
    /// </summary>
    public static ValidationResult Validate(ResourceConfig config, string packageName, bool enforceSignature = true)
    {
        var result = new ValidationResult { IsValid = true };
        var packInfo = config?.packInfo;

        // Collect every declared ID with a category label
        var declaredIds = CollectDeclaredIds(config);

        if (declaredIds.Count == 0)
        {
            // No custom IDs declared — nothing to validate
            return result;
        }

        // ── Check for forbidden IDs (< 9000) ──────────────────────────
        var forbiddenIds = declaredIds.Where(x => x.id <= GameIdMax).ToList();
        foreach (var (category, id) in forbiddenIds)
        {
            result.AddError($"[{packageName}] {category} ID {id} is in the reserved game range (<= {GameIdMax}) and cannot be used.");
        }

        // ── Partition IDs into managed / unmanaged ────────────────────
        var managedIds = declaredIds.Where(x => x.id >= ManagedIdMin && x.id <= ManagedIdMax).ToList();
        var unmanagedIds = declaredIds.Where(x => x.id >= UnmanagedIdMin && x.id <= UnmanagedIdMax).ToList();

        // Unmanaged IDs are always allowed — no further checks needed.

        // ── Managed IDs: require valid packInfo range + signature ─────
        if (managedIds.Count > 0)
        {
            if (packInfo == null || !packInfo.idRangeStart.HasValue || !packInfo.idRangeEnd.HasValue)
            {
                result.AddError($"[{packageName}] Package uses managed-range IDs ({ManagedIdMin}–{ManagedIdMax}) but does not declare idRangeStart/idRangeEnd in packInfo.");
                return result;
            }

            int rangeStart = packInfo.idRangeStart.Value;
            int rangeEnd = packInfo.idRangeEnd.Value;

            // Range itself must be within managed bounds
            if (rangeStart < ManagedIdMin || rangeEnd > ManagedIdMax || rangeStart > rangeEnd)
            {
                result.AddError($"[{packageName}] Declared ID range [{rangeStart}, {rangeEnd}] is invalid or outside managed range ({ManagedIdMin}–{ManagedIdMax}).");
            }

            // Signature check
            if (!enforceSignature)
            {
                Log.LogWarning($"[{packageName}] Signature check is DISABLED by config. Skipping signature verification for [{rangeStart}, {rangeEnd}].");
            }
            else if (string.IsNullOrWhiteSpace(packInfo.idSignature))
            {
                result.AddError($"[{packageName}] Package declares managed ID range [{rangeStart}, {rangeEnd}] but has no idSignature.");
            }
            else if (string.IsNullOrWhiteSpace(packInfo.label))
            {
                result.AddError($"[{packageName}] Package has idSignature but no label — cannot verify signature.");
            }
            else
            {
                bool signatureValid = VerifySignature(packInfo.label, rangeStart, rangeEnd, packInfo.idSignature);
                if (!signatureValid)
                {
                    result.AddError($"[{packageName}] ID range signature verification FAILED for \"{packInfo.label}:{rangeStart}-{rangeEnd}\". The package may be tampered or the signature is outdated.");
                }
                else
                {
                    Log.LogInfo($"[{packageName}] ID range signature verified: \"{packInfo.label}:{rangeStart}-{rangeEnd}\"");
                }
            }

            // Check every managed ID falls within the declared range
            foreach (var (category, id) in managedIds)
            {
                if (id < rangeStart || id > rangeEnd)
                {
                    result.AddError($"[{packageName}] {category} ID {id} is outside the declared range [{rangeStart}, {rangeEnd}].");
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Collects all explicitly declared IDs from a resource config, along with a human-friendly category string.
    /// </summary>
    private static List<(string category, int id)> CollectDeclaredIds(ResourceConfig config)
    {
        var ids = new List<(string category, int id)>();
        if (config == null) return ids;

        if (config.characters != null)
        {
            foreach (var c in config.characters)
                ids.Add(("Character", c.id));
        }

        if (config.ingredients != null)
        {
            foreach (var i in config.ingredients)
                ids.Add(("Ingredient", i.id));
        }

        if (config.foods != null)
        {
            foreach (var f in config.foods)
                ids.Add(("Food", f.id));
        }

        if (config.beverages != null)
        {
            foreach (var b in config.beverages)
                ids.Add(("Beverage", b.id));
        }

        if (config.recipes != null)
        {
            foreach (var r in config.recipes)
                ids.Add(("Recipe", r.id));
        }

        return ids;
    }
}
