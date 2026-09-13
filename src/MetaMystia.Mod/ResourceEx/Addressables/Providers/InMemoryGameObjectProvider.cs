using System;
using System.Collections.Generic;

using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace MetaMystia.ResourceEx.Addressables.Providers;

public class InMemoryGameObjectProvider : ResourceProviderBase
{
    public const string ProviderIdConst = "MetaMystia.ResourceEx.Addressables.Providers.InMemoryGameObjectProvider";
    private static readonly Dictionary<string, GameObject> Assets = new();

    public InMemoryGameObjectProvider(IntPtr ptr) : base(ptr) { }
    public InMemoryGameObjectProvider() : base(ClassInjector.DerivedConstructorPointer<InMemoryGameObjectProvider>())
    {
        ClassInjector.DerivedConstructorBody(this);
        m_ProviderId = ProviderIdConst;
    }

    public override string ProviderId => ProviderIdConst;
    public override Il2CppSystem.Type GetDefaultType(IResourceLocation location) => Il2CppType.Of<GameObject>();
    public override bool CanProvide(Il2CppSystem.Type type, IResourceLocation location)
        => location != null && location.ProviderId == ProviderIdConst && Assets.ContainsKey(location.InternalId);

    public override void Provide(ProvideHandle handle)
    {
        var key = handle.Location.InternalId;
        if (Assets.TryGetValue(key, out var asset))
            handle.Complete<GameObject>(asset, true, (Il2CppSystem.Exception)null);
        else
            handle.Complete<GameObject>(null, false, new Il2CppSystem.Exception($"Map template missing: {key}"));
    }

    // 模板由地图注册表持有，原游戏负责销毁每次实例化的地图。
    public override void Release(IResourceLocation location, Il2CppSystem.Object asset) { }
    internal static void AddAsset(string guid, GameObject asset) => Assets[guid] = asset;
    internal static GameObject GetAsset(string guid) => Assets.TryGetValue(guid, out var asset) ? asset : null;
    internal static bool RemoveAsset(string guid) => Assets.Remove(guid);
    internal static bool HasAsset(string guid) => Assets.ContainsKey(guid);
}
