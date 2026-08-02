using System;

namespace MetaMystia.ResourceEx.Addressables.Providers;

/// <summary>
/// Metadata describing how <see cref="MetaMystia.ResourceEx.Addressables.RuntimeAddressables"/> routes a Unity asset type
/// to its registered <see cref="UnityEngine.ResourceManagement.ResourceProviders.IResourceProvider"/>.
/// </summary>
public sealed class ProviderRegistration
{
    /// <summary>The provider id stamped onto each <c>ResourceLocationBase</c> for this type.</summary>
    public string ProviderId { get; init; }

    /// <summary>The Unity asset type this provider serves (e.g. <c>typeof(Sprite)</c>).</summary>
    public Type AssetType { get; init; }

    /// <summary>The IL2CPP resource type stamped onto each <c>ResourceLocationBase</c>.</summary>
    public Il2CppSystem.Type ResourceType { get; init; }

    /// <summary>Insert / overwrite an asset in the provider's internal store.</summary>
    public Action<string, UnityEngine.Object> AddAsset { get; init; }

    /// <summary>Return an asset from the provider's internal store by GUID, or null when absent.</summary>
    public Func<string, UnityEngine.Object> GetAsset { get; init; }

    /// <summary>Remove an asset from the provider's internal store. Returns true if present.</summary>
    public Func<string, bool> RemoveAsset { get; init; }

    /// <summary>Return true if the provider's internal store contains an entry for the GUID.</summary>
    public Func<string, bool> HasAsset { get; init; }
}
