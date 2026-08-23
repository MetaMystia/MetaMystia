using System.Collections.Generic;

namespace MetaMystia.ResourceEx.Models;

public class IngredientConfig
{
    public int id { get; set; }
    public string name { get; set; }
    public string description { get; set; }
    public int level { get; set; }
    public int prefix { get; set; }
    public bool isFish { get; set; }
    public bool isMeat { get; set; }
    public bool isVeg { get; set; }
    public int baseValue { get; set; }
    public List<int> tags { get; set; }

    public string spritePath { get; set; }
}
