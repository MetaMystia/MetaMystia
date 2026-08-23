using System.Collections.Generic;

namespace MetaMystia.ResourceEx.Models;

public class RecipeConfig
{
    public int id { get; set; }
    public int foodId { get; set; }
    public GameData.Core.Collections.Cooker.CookerType cookerType { get; set; }
    public float cookTime { get; set; }
    public List<int> ingredients { get; set; }
}
