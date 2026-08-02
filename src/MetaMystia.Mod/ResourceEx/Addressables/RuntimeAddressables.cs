using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MetaMystia.ResourceEx.Addressables.Providers;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace MetaMystia.ResourceEx.Addressables;

/// <summary>
/// Serves arbitrary in-memory Unity assets through the standard Unity Addressables pipeline.
/// Game / engine code calling <see cref="AssetReference.LoadAssetAsync{T}()"/> or
/// <see cref="Addressables.LoadAssetAsync{T}(object)"/> can resolve registered assets
/// without any patching of the consumer side.
///
/// <para><b>Built-in support:</b> <see cref="Sprite"/>, <see cref="AudioClip"/>.</para>
///
/// <para><b>Adding new asset types:</b></para>
/// <list type="number">
/// <item>Write a concrete subclass of <see cref="ResourceProviderBase"/>
/// (use <see cref="InMemorySpriteProvider"/> as a ~30-line template).
/// Do not use generics — Il2CppInterop cannot inject closed generic types.</item>
/// <item>Call <see cref="ClassInjector.RegisterTypeInIl2Cpp{T}()"/> on it once.</item>
/// <item>Call <see cref="RegisterProviderType{T}"/> with a fresh provider instance and
/// delegates wrapping the provider's static asset-store methods.</item>
/// </list>
///
/// <para><b>Why GUID keys:</b> <c>AssetReference.RuntimeKeyIsValid()</c> internally calls
/// <c>Guid.TryParse</c>; non-GUID keys cause game code paths like <c>LoadAssetAllowNull</c>
/// to silently skip the load. This class MD5-hashes any human-readable key into a
/// deterministic GUID so the check passes.</para>
/// </summary>
public static class RuntimeAddressables
{
    private static readonly object _initLock = new();
    private static bool _initialized;
    private static ManualLogSource _log;
    private static ResourceLocationMap _locator;

    /// <summary>Type → provider routing table.</summary>
    private static readonly Dictionary<Type, ProviderRegistration> _registrations = new();

    /// <summary>Track keys (in GUID form) we've already added to the locator, for dedupe.</summary>
    private static readonly HashSet<string> _knownGuids = new();

    /// <summary>Whether the registry has been initialized successfully.</summary>
    public static bool IsInitialized => _initialized;

    /// <summary>
    /// Initialize the registry: create a shared <see cref="ResourceLocationMap"/>, register it
    /// with Addressables, and inject the built-in providers (Sprite, AudioClip).
    /// Idempotent and thread-safe.
    /// </summary>
    /// <param name="log">Optional log source. A private one is created if null.</param>
    public static void Initialize(ManualLogSource log = null)
    {
        if (_initialized) return;
        lock (_initLock)
        {
            if (_initialized) return;

            _log = log ?? BepInEx.Logging.Logger.CreateLogSource("RuntimeAddressables");

            try
            {
                _locator = new ResourceLocationMap("MetaMystia.ResourceEx.Addressables.RuntimeAddressables.Locator", 32);

                UnityEngine.AddressableAssets.Addressables.AddResourceLocator(
                    _locator.Cast<IResourceLocator>(),
                    (string)null,
                    (IResourceLocation)null);

                RegisterBuiltInSpriteProvider();
                RegisterBuiltInAudioClipProvider();

                _initialized = true;
                _log.LogInfo("Initialized.");
            }
            catch (Exception ex)
            {
                _log.LogError($"Initialization failed: {ex}");
                throw;
            }
        }
    }

    // ── Public registration API ──

    /// <summary>
    /// Register an in-memory asset of a supported type under <paramref name="key"/>,
    /// returning an <see cref="AssetReferenceT{T}"/> that resolves to it via Addressables.
    /// </summary>
    /// <typeparam name="T">A Unity asset type with a registered provider (built-in: Sprite, AudioClip;
    /// custom: anything passed to <see cref="RegisterProviderType{T}"/>).</typeparam>
    /// <exception cref="NotSupportedException">No provider has been registered for type <typeparamref name="T"/>.</exception>
    public static AssetReferenceT<T> Register<T>(string key, T asset)
        where T : UnityEngine.Object
    {
        if (!_initialized) Initialize();
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key must not be empty", nameof(key));

        if (!_registrations.TryGetValue(typeof(T), out var reg))
        {
            throw new NotSupportedException(
                $"No provider registered for type {typeof(T).FullName}. " +
                $"Built-in support: Sprite, AudioClip. " +
                $"For other types, write a ResourceProviderBase subclass and call " +
                $"RuntimeAddressables.RegisterProviderType<{typeof(T).Name}>(...).");
        }

        var guid = KeyToGuid(key);

        // Defensive: prevent unintended unload by Addressables / scene change.
        asset.hideFlags |= HideFlags.HideAndDontSave;

        reg.AddAsset(guid, asset);
        AddLocation(guid, reg);

        return new AssetReferenceT<T>(guid);
    }

    /// <summary>
    /// Convenience overload returning the more specific <see cref="AssetReferenceSprite"/>
    /// (which game code often type-checks against directly).
    /// </summary>
    public static AssetReferenceSprite RegisterSprite(string key, Sprite sprite)
    {
        Register(key, sprite);
        return new AssetReferenceSprite(KeyToGuid(key));
    }

    /// <summary>
    /// Resolve a previously registered in-memory asset through RuntimeAddressables' central store.
    /// This is the synchronous counterpart for game code that still needs a direct Unity object.
    /// </summary>
    public static bool TryGetAsset<T>(string key, out T asset)
        where T : UnityEngine.Object
    {
        asset = null;
        if (string.IsNullOrEmpty(key)) return false;
        if (!_initialized) Initialize();

        if (!_registrations.TryGetValue(typeof(T), out var reg))
            return false;

        var guid = KeyToGuid(key);
        var obj = reg.GetAsset?.Invoke(guid);
        if (obj is not T typed)
            return false;

        asset = typed;
        return true;
    }

    /// <summary>Return an Addressables reference for an asset already registered under <paramref name="key"/>.</summary>
    public static bool TryGetReference<T>(string key, out AssetReferenceT<T> reference)
        where T : UnityEngine.Object
    {
        reference = null;
        if (string.IsNullOrEmpty(key)) return false;
        if (!_initialized) Initialize();

        if (!_registrations.TryGetValue(typeof(T), out var reg))
            return false;

        var guid = KeyToGuid(key);
        if (!reg.HasAsset(guid))
            return false;

        reference = new AssetReferenceT<T>(guid);
        return true;
    }

    /// <summary>Return an <see cref="AssetReferenceSprite"/> for a Sprite already registered under <paramref name="key"/>.</summary>
    public static bool TryGetSpriteReference(string key, out AssetReferenceSprite reference)
    {
        reference = null;
        if (string.IsNullOrEmpty(key)) return false;
        if (!_initialized) Initialize();

        if (!_registrations.TryGetValue(typeof(Sprite), out var reg))
            return false;

        var guid = KeyToGuid(key);
        if (!reg.HasAsset(guid))
            return false;

        reference = new AssetReferenceSprite(guid);
        return true;
    }

    /// <summary>
    /// Register a custom provider for asset type <typeparamref name="T"/>.
    /// Call once after constructing your provider and registering its il2cpp type.
    /// </summary>
    public static void RegisterProviderType<T>(
        IResourceProvider provider,
        string providerId,
        Action<string, T> addAsset,
        Func<string, bool> removeAsset = null,
        Func<string, bool> hasAsset = null,
        Func<string, T> getAsset = null)
        where T : UnityEngine.Object
    {
        if (!_initialized) Initialize();
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        if (string.IsNullOrEmpty(providerId)) throw new ArgumentException("providerId required", nameof(providerId));
        if (addAsset == null) throw new ArgumentNullException(nameof(addAsset));

        if (_registrations.ContainsKey(typeof(T)))
            _log.LogWarning($"Provider for {typeof(T).FullName} already registered; overwriting.");

        AttachProviderToResourceManager(provider);

        _registrations[typeof(T)] = new ProviderRegistration
        {
            ProviderId = providerId,
            AssetType = typeof(T),
            ResourceType = Il2CppType.From(typeof(T)),
            AddAsset = (guid, obj) => addAsset(guid, (T)obj),
            GetAsset = getAsset == null ? (_ => null) : (guid => getAsset(guid)),
            RemoveAsset = removeAsset ?? (_ => false),
            HasAsset = hasAsset ?? (_ => false),
        };

        _log.LogInfo($"Registered provider for {typeof(T).FullName} (id={providerId})");
    }

    /// <summary>
    /// Unregister a previously registered asset of type <typeparamref name="T"/>.
    /// Removes both the provider entry and the locator entry, leaving a clean miss.
    /// </summary>
    public static bool Unregister<T>(string key) where T : UnityEngine.Object
    {
        if (!_initialized) return false;
        if (!_registrations.TryGetValue(typeof(T), out var reg)) return false;

        var guid = KeyToGuid(key);
        var removed = reg.RemoveAsset(guid);

        if (_knownGuids.Remove(guid))
        {
            // ResourceLocationMap.Locations is an il2cpp Dictionary<Il2CppSystem.Object, IList<...>>.
            // We registered the key as a boxed Il2Cpp string; remove using the same boxed form.
            var dict = _locator.Locations;
            var keyBoxed = (Il2CppSystem.Object)(Il2CppSystem.String)guid;
            dict.Remove(keyBoxed);
        }

        if (removed) _log.LogDebug($"Unregistered {typeof(T).Name}: {key} → {guid}");
        return removed;
    }

    /// <summary>Whether <paramref name="key"/> is currently registered for type <typeparamref name="T"/>.</summary>
    public static bool IsRegistered<T>(string key) where T : UnityEngine.Object
    {
        if (!_initialized) return false;
        if (!_registrations.TryGetValue(typeof(T), out var reg)) return false;
        return reg.HasAsset(KeyToGuid(key));
    }

    /// <summary>
    /// Convert a human-readable key to its deterministic GUID-format string.
    /// AssetReference.RuntimeKeyIsValid() requires Guid.TryParse() to succeed; this
    /// MD5-hashes the input so any string maps to a valid GUID.
    /// </summary>
    public static string KeyToGuid(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key must not be null or empty", nameof(key));

        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
        return new Guid(hash).ToString("D");
    }

    // ── Internals ──

    private static void RegisterBuiltInSpriteProvider()
    {
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<InMemorySpriteProvider>())
            ClassInjector.RegisterTypeInIl2Cpp<InMemorySpriteProvider>();

        var provider = new InMemorySpriteProvider();
        AttachProviderToResourceManager(provider.Cast<IResourceProvider>());

        _registrations[typeof(Sprite)] = new ProviderRegistration
        {
            ProviderId = InMemorySpriteProvider.ProviderIdConst,
            AssetType = typeof(Sprite),
            ResourceType = Il2CppType.Of<Sprite>(),
            AddAsset = (guid, obj) => InMemorySpriteProvider.AddAsset(guid, (Sprite)obj),
            GetAsset = guid => InMemorySpriteProvider.GetAsset(guid),
            RemoveAsset = InMemorySpriteProvider.RemoveAsset,
            HasAsset = InMemorySpriteProvider.HasAsset,
        };
    }

    private static void RegisterBuiltInAudioClipProvider()
    {
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<InMemoryAudioClipProvider>())
            ClassInjector.RegisterTypeInIl2Cpp<InMemoryAudioClipProvider>();

        var provider = new InMemoryAudioClipProvider();
        AttachProviderToResourceManager(provider.Cast<IResourceProvider>());

        _registrations[typeof(AudioClip)] = new ProviderRegistration
        {
            ProviderId = InMemoryAudioClipProvider.ProviderIdConst,
            AssetType = typeof(AudioClip),
            ResourceType = Il2CppType.Of<AudioClip>(),
            AddAsset = (guid, obj) => InMemoryAudioClipProvider.AddAsset(guid, (AudioClip)obj),
            GetAsset = guid => InMemoryAudioClipProvider.GetAsset(guid),
            RemoveAsset = InMemoryAudioClipProvider.RemoveAsset,
            HasAsset = InMemoryAudioClipProvider.HasAsset,
        };
    }

    private static void AttachProviderToResourceManager(IResourceProvider provider)
    {
        var providers = UnityEngine.AddressableAssets.Addressables.ResourceManager.ResourceProviders;
        // Actual runtime type is ListWithEvents<IResourceProvider>, not List<>.
        var providerList = providers.Cast<ListWithEvents<IResourceProvider>>();
        providerList.Add(provider);
    }

    private static void AddLocation(string guid, ProviderRegistration reg)
    {
        if (!_knownGuids.Add(guid))
        {
            _log.LogDebug($"Updated existing entry for GUID {guid}");
            return;
        }

        var location = new ResourceLocationBase(
            guid,
            guid,
            reg.ProviderId,
            reg.ResourceType,
            new Il2CppReferenceArray<IResourceLocation>(0)
        );

        _locator.Add(
            (Il2CppSystem.Object)(Il2CppSystem.String)guid,
            location.Cast<IResourceLocation>()
        );

        _log.LogDebug($"Registered location: {guid} (type={reg.AssetType.Name})");
    }
}
