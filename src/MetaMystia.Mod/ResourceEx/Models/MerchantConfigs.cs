using System.Collections.Generic;

using static GameData.Core.Collections.DaySceneUtility.Collections.Product;

namespace MetaMystia.ResourceEx.Models;

public class MerchantConfig
{
    public string key { get; set; }
    public List<string> welcomeDialogPackageNames { get; set; }
    public List<string> nullDialogPackageNames { get; set; }
    public float priceMultiplierMin { get; set; }
    public float priceMultiplierMax { get; set; }
    public int leastSellNum { get; set; }
    public List<MerchandiseConfig> merchandise { get; set; }
}

public class MerchandiseConfig
{
    public ProductConfig item { get; set; }
    public int itemAmountMin { get; set; }
    public int itemAmountMax { get; set; }
    public float sellProbability { get; set; }
}

public class ProductConfig
{
    public ProductType productType { get; set; }
    public int productId { get; set; }
    public int productAmount { get; set; }
    public string productLabel { get; set; }
}
