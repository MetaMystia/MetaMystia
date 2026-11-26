using GameData.Core.Collections;

namespace MetaMystia.ResourceEx.Models;

/// <summary>
/// 装饰的显示数据。implementation 指定代码中的实现；效果由代码实现，名称、说明、图标由资源包提供。
/// </summary>
public class DecorationConfig : ItemConfig
{
    public string implementation { get; set; }
    public Decoration.DecorationType decorationType { get; set; } = Decoration.DecorationType.Outdoor;
    public int[] conflictDecorationIds { get; set; } = [];
}
