using System.Collections.Generic;

namespace MetaMystia.ResourceEx.Models;

public class NewsNodeConfig
{
    public string label { get; set; }
    public string debugLabel { get; set; }
    public string title { get; set; }
    public string description { get; set; }
    public List<MissionRewardConfig> rewards { get; set; }
    public List<MissionRewardConfig> postRewards { get; set; }
}
