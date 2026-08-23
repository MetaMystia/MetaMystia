using System.Collections.Generic;

using GameData.Profile;
using GameData.Profile.SchedulerNodeCollection;

using static GameData.Core.Collections.DaySceneUtility.Collections.Product;
using static GameData.Core.Collections.Sellable;
using static GameData.Profile.SchedulerNode;
using static GameData.Profile.SchedulerNodeCollection.MissionNode.FinishCondition;

namespace MetaMystia.ResourceEx.Models;

public class MissionNodeConfig
{
    public string title { get; set; }
    public string description { get; set; }
    public string label { get; set; }
    public string debugLabel { get; set; }
    public SchedulerNode.SchedulerType missionType { get; set; }
    public string sender { get; set; }
    public string reciever { get; set; } // ignore typo
    public List<MissionRewardConfig> rewards { get; set; }
    public List<MissionRewardConfig> postRewards { get; set; }
    public List<MissionFinishConditionConfig> finishConditions { get; set; }
    public EventDataConfig missionFinishEvent { get; set; }
    public EventDataConfig missionFailedEvent { get; set; }
    public List<string> postMissionsAfterPerformance { get; set; }
    public List<string> postEvents { get; set; }
    public bool isTimedMission { get; set; } = false;
    public MissionNode.MissionFailedAction missionFailedAction { get; set; } = MissionNode.MissionFailedAction.None;
    public TriggerConfig missionTimeLimit { get; set; }
}

public class MissionRewardConfig
{
    public Reward.RewardType rewardType { get; set; }
    public string rewardId { get; set; }
    public Reward.ObjectType? objectType { get; set; }
    public List<int> rewardIntArray { get; set; }
}

public class MissionFinishConditionConfig
{
    public ConditionType conditionType { get; set; }
    public int? amount { get; set; }
    public int? tag { get; set; }
    public int[] tags { get; set; }
    public SellableType? sellableType { get; set; }
    public string label { get; set; }
    public ProductType? productType { get; set; }
    public int? productId { get; set; }
    public int? productAmount { get; set; }
}
