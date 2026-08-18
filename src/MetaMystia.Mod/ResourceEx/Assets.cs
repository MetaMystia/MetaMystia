using UnityEngine;
using MetaMystia.ResourceEx.Addressables;
using MetaMystia.ResourceEx.AssetManagement;
using UnityEngine.AddressableAssets;

namespace MetaMystia;

public static partial class ResourceExManager
{
    public static string ResolveAssetUri(string path, string packageLabel)
    {
        return RexAssetRegistry.TryResolveUri(path, packageLabel, out var uri) ? uri : null;
    }

    public static bool TryGetSprite(string uri, out Sprite sprite)
    {
        sprite = null;
        return RexUri.IsRexUri(uri) && RuntimeAddressables.TryGetAsset(uri, out sprite);
    }

    public static bool TryGetSpriteReference(string uri, out AssetReferenceSprite reference)
    {
        reference = null;
        return RexUri.IsRexUri(uri) && RuntimeAddressables.TryGetSpriteReference(uri, out reference);
    }

    public static bool TryGetAudioReference(string uri, out AssetReferenceT<AudioClip> reference)
    {
        reference = null;
        return RexUri.IsRexUri(uri) && RuntimeAddressables.TryGetReference(uri, out reference);
    }

}
