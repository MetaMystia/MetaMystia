namespace MetaMystia.ResourceEx.Models;

/// <summary>需要在启动时预加载的 AssetBundle；运行时按其 rex:// URI 取用。</summary>
public class AssetBundleConfig
{
    public string path { get; set; }
}
