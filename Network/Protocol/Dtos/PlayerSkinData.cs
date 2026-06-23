using MemoryPack;

using MetaMystia.Network;

namespace MetaMystia;

// 数据半边（协议层）：序列化字段，零游戏依赖（游戏枚举以 WireSkinType 镜像）。
// 行为半边（mod，皮肤解析/立绘/应用到 unit 等）见 Players/PlayerSkin.cs。

[MemoryPackable]
public partial class PlayerSkinData
{
    public int CharacterId = -1; // -1 means Mystia
    public WireSkinType SelectedType = WireSkinType.Default;
    public int SkinIndex = 0;

    /// <summary>
    /// 在线皮肤名（皮肤站标识）。非空时优先使用，由 NetSkinManager 负责异步拉取与解析；
    /// 未就绪时返回 Fallback 占位，下载完成后会自动刷新。为空则回落到原有 CharacterId/Type/Index 流程。
    /// </summary>
    public string NetSkinName = null;

    /// <summary>
    /// 旋转覆盖。null = 使用皮肤默认值；true = 强制开启旋转；false = 强制关闭旋转。
    /// </summary>
    public bool? RotateOverride = null;

    /// <summary>设定在线皮肤名。非空时由 NetSkinManager 异步拉取与解析。</summary>
    public void SetNetSkin(string name)
    {
        NetSkinName = string.IsNullOrEmpty(name) ? null : name;
    }

    /// <summary>设置旋转覆盖</summary>
    public void SetRotate(bool? value)
    {
        RotateOverride = value;
    }
}
