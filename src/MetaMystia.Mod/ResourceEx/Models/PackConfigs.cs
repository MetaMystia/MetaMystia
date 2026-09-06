using System.Collections.Generic;

namespace MetaMystia.ResourceEx.Models;

public class PackInfoConfig
{
    public string name { get; set; }
    public string label { get; set; }
    public List<string> authors { get; set; }
    public string description { get; set; }
    public string version { get; set; }
    public string license { get; set; }
    public int? idRangeStart { get; set; }
    public int? idRangeEnd { get; set; }
    public string idSignature { get; set; }

    /// <summary>
    /// 依赖的 DLC/包标签（如 "CORE"、"DLC1"、"DLC2"），加载前必须全部处于激活状态
    /// </summary>
    public List<string> dependencies { get; set; }
}

public class ResourceConfig
{
    public PackInfoConfig packInfo { get; set; }
    public List<CharacterConfig> characters { get; set; }
    public List<DialogPackageConfig> dialogPackages { get; set; }
    public List<GiftConfig> gifts { get; set; }
    public List<IngredientConfig> ingredients { get; set; }
    public List<RecipeConfig> recipes { get; set; }
    public List<FoodConfig> foods { get; set; }
    public List<BeverageConfig> beverages { get; set; }
    public List<ClothConfig> clothes { get; set; }
    public List<MissionNodeConfig> missionNodes { get; set; }
    public List<EventNodeConfig> eventNodes { get; set; }
    public List<MerchantConfig> merchants { get; set; }
}
