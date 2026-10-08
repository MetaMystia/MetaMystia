using System.Collections.Generic;

namespace MetaMystia.ResourceEx.Models;

public class TagConfig
{
    public int id { get; set; }
    public string name { get; set; }
}

public class TagRuleConfig
{
    /// <summary>同组至多保留一个 Tag，靠前者优先</summary>
    public List<int> tags { get; set; }
}
