using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace MetaMystia.ResourceEx.Addressables.Providers;

/// <summary>
/// Built-in <see cref="IResourceProvider"/> serving in-memory <see cref="Sprite"/> assets
/// through the Addressables pipeline. Registered automatically by
/// <see cref="RuntimeAddressables.Initialize"/>.
/// </summary>
/// <remarks>
/// Concrete (non-generic) subclass of <see cref="ResourceProviderBase"/>; Il2CppInterop
/// cannot inject closed generic types, so each asset type gets its own dedicated provider.
/// </remarks>
public class InMemorySpriteProvider : ResourceProviderBase
{
    public const string ProviderIdConst = "MetaMystia.ResourceEx.Addressables.Providers.InMemorySpriteProvider";

    private static readonly Dictionary<string, Sprite> _assets = new();

    public InMemorySpriteProvider(IntPtr ptr) : base(ptr) { }

    public InMemorySpriteProvider()
        : base(ClassInjector.DerivedConstructorPointer<InMemorySpriteProvider>())
    {
        ClassInjector.DerivedConstructorBody(this);
        m_ProviderId = ProviderIdConst;
    }

    public override string ProviderId => ProviderIdConst;

    public override Il2CppSystem.Type GetDefaultType(IResourceLocation location)
        => Il2CppType.Of<Sprite>();

    public override bool CanProvide(Il2CppSystem.Type type, IResourceLocation location)
    {
        if (location == null) return false;
        if (location.ProviderId != ProviderIdConst) return false;
        return _assets.ContainsKey(location.InternalId);
    }

    public override void Provide(ProvideHandle provideHandle)
    {
        var key = provideHandle.Location.InternalId;
        if (_assets.TryGetValue(key, out var sprite))
            provideHandle.Complete<Sprite>(sprite, true, (Il2CppSystem.Exception)null);
        else
            provideHandle.Complete<Sprite>(null, false,
                new Il2CppSystem.Exception($"{ProviderIdConst}: sprite not found for key '{key}'"));
    }

    public override void Release(IResourceLocation location, Il2CppSystem.Object asset)
    {
        // In-memory; consumer owns lifecycle.
    }

    internal static void AddAsset(string guid, Sprite sprite) => _assets[guid] = sprite;
    internal static Sprite GetAsset(string guid) => _assets.TryGetValue(guid, out var sprite) ? sprite : null;
    internal static bool RemoveAsset(string guid) => _assets.Remove(guid);
    internal static bool HasAsset(string guid) => _assets.ContainsKey(guid);
}
