using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace MetaMystia.ResourceEx.Addressables.Providers;

/// <summary>
/// Built-in <see cref="IResourceProvider"/> serving in-memory <see cref="AudioClip"/> assets
/// through the Addressables pipeline. Registered automatically by
/// <see cref="RuntimeAddressables.Initialize"/>.
/// </summary>
public class InMemoryAudioClipProvider : ResourceProviderBase
{
    public const string ProviderIdConst = "MetaMystia.ResourceEx.Addressables.Providers.InMemoryAudioClipProvider";

    private static readonly Dictionary<string, AudioClip> _assets = new();

    public InMemoryAudioClipProvider(IntPtr ptr) : base(ptr) { }

    public InMemoryAudioClipProvider()
        : base(ClassInjector.DerivedConstructorPointer<InMemoryAudioClipProvider>())
    {
        ClassInjector.DerivedConstructorBody(this);
        m_ProviderId = ProviderIdConst;
    }

    public override string ProviderId => ProviderIdConst;

    public override Il2CppSystem.Type GetDefaultType(IResourceLocation location)
        => Il2CppType.Of<AudioClip>();

    public override bool CanProvide(Il2CppSystem.Type type, IResourceLocation location)
    {
        if (location == null) return false;
        if (location.ProviderId != ProviderIdConst) return false;
        return _assets.ContainsKey(location.InternalId);
    }

    public override void Provide(ProvideHandle provideHandle)
    {
        var key = provideHandle.Location.InternalId;
        if (_assets.TryGetValue(key, out var clip))
            provideHandle.Complete<AudioClip>(clip, true, (Il2CppSystem.Exception)null);
        else
            provideHandle.Complete<AudioClip>(null, false,
                new Il2CppSystem.Exception($"{ProviderIdConst}: clip not found for key '{key}'"));
    }

    public override void Release(IResourceLocation location, Il2CppSystem.Object asset) { }

    internal static void AddAsset(string guid, AudioClip clip) => _assets[guid] = clip;
    internal static AudioClip GetAsset(string guid) => _assets.TryGetValue(guid, out var clip) ? clip : null;
    internal static bool RemoveAsset(string guid) => _assets.Remove(guid);
    internal static bool HasAsset(string guid) => _assets.ContainsKey(guid);
}
