using System.Collections.Generic;

using static GameData.Profile.SchedulerNode;
using static GameData.Profile.SchedulerNode.Trigger;

namespace MetaMystia.ResourceEx.Models;

public class EventNodeConfig
{
    public string label { get; set; }
    public string debugLabel { get; set; }
    public ScheduledEventConfig scheduledEvent { get; set; }
    public List<MissionRewardConfig> rewards { get; set; }
    public List<MissionRewardConfig> postRewards { get; set; }
    public List<string> postMissionsAfterPerformance { get; set; }
    public List<string> postEvents { get; set; }
}

public class DayConfig
{
    public Day.DayType dayType { get; set; }
    public Day.CalculateType dayCalcType { get; set; }
    public int day { get; set; }
    public int dayRangeMin { get; set; }
    public int dayRangeMax { get; set; }
}

public class TriggerConfig
{
    public TriggerType triggerType { get; set; }
    public string triggerId { get; set; }
    public DayConfig time { get; set; }
}

public class ScheduledEventConfig
{
    public EventDataConfig eventData { get; set; }
    public TriggerConfig trigger { get; set; }
}

public class EventDataConfig
{
    public Event.EventType eventType { get; set; }
    public string dialogPackageName { get; set; } // -> SchedulerNode.Event.runtimeDialogPackage: DialogPackage
}
