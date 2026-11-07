using static GameData.Core.Collections.DaySceneUtility.Collections.Product;

namespace MetaMystia.ResourceEx.Models;

public class GiftConfig
{
    public ProductType? productType { get; set; }
    public int? productId { get; set; }
    public int? productAmount { get; set; }

    /// <summary>
    /// 旧字段，等价于 productType: Item 加 productId；与 productId 并存时以 productId 为准
    /// </summary>
    public int? itemId { get; set; }
    public bool allowRepeat { get; set; }
    public string title { get; set; }
    public string dialogPackageName { get; set; }
}
