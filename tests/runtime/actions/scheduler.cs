// 原生 Scheduler 的读取与操作，只引用游戏类型，未安装模组时也能执行。FILTER 为标签子串，只显示匹配的条目。
// MODE=show      今天、资金，运行时新闻、事件、任务与已完成列表中匹配的条目，未加载分区的键与 dlcSaveDate（替换内容看存档）
// MODE=news      经 ScheduleNews 奖励把 LABEL（逗号分隔为多个候选）排到绝对日 VALUE
// MODE=event     ScheduleEvent(LABEL)，日期按节点配置
// MODE=mission   StartMission(LABEL)，日期按节点配置
// MODE=finish    FinishNodeExtern(LABEL)：直接完成节点并发放其奖励
// MODE=addday    日期加 VALUE（可为负），不经过日终流程
// MODE=save      原版存档写入槽位 VALUE
// MODE=apply     直接执行原生 ApplyNews（发布今天日期的新闻，正常只在进入白天时调用）
// MODE=title     回标题（原版 DisposeGameStatusAndBackToMainMenu）
// MODE=newgame   在标题载入新游戏的数据（LoadNewPlayerData），不切场景
// MODE=scene     当前场景、白天就绪、timeScale
using System.Collections.Generic;
using GameData.Profile;
using GameData.RunTime.Common;
public static class Program {
  const string Filter = "@@FILTER@@", Label = "@@LABEL@@";
  static bool Match(string s) => s != null && s.Contains(Filter);
  public static object Execute() {
    switch ("@@MODE@@") {
      case "show": return Show();
      case "news": {
        // 直接调用 ScheduleNews 会因 ReplaceContent 的 interop 类型抛 TypeLoadException，经原生 ProcessReward（与资源包奖励相同）。
        var day = new SchedulerNode.Day { dayType = SchedulerNode.Day.DayType.Absolute, dayCalcType = SchedulerNode.Day.CalculateType.Constant, day = @@VALUE@@ };
        RunTimeScheduler.ProcessReward(new SchedulerNode.Reward { rewardType = SchedulerNode.Reward.RewardType.ScheduleNews, rewardIdArray = Label.Split(','), time = day });
        return "scheduled news " + Label + " | " + Show();
      }
      case "event": RunTimeScheduler.ScheduleEvent(Label); return "schedule event " + Label + " | " + Show();
      case "mission": RunTimeScheduler.StartMission(Label); return "start mission " + Label + " | " + Show();
      case "finish": RunTimeScheduler.FinishNodeExtern(Label); return "finished " + Label + " | " + Show();
      case "newgame": GameData.Utils.SaveManagement.LoadNewPlayerData(); return "new game data | " + Show();
      case "addday": RunTimePlayerData.AddDay(@@VALUE@@); return "day now " + RunTimePlayerData.GetDay().CorrectedDay;
      case "save": GameData.Utils.SaveManagement.WriteCurrentPlayerDataToSlotAsync(@@VALUE@@); return "saving to slot @@VALUE@@ at day " + RunTimePlayerData.GetDay().CorrectedDay;
      case "apply": RunTimeScheduler.ApplyNews(); return "applied | " + Show();
      case "title": GameData.Utils.SaveManagement.DisposeGameStatusAndBackToMainMenu(); return "back to title";
      case "scene": return "scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        + " day=" + (DayScene.SceneManager.hasInstance) + " title=" + MainScene.SceneManager.hasInstance
        + " timeScale=" + UnityEngine.Time.timeScale;
      default: return "unknown mode";
    }
  }
  static string Show() {
    var parts = new List<string>();
    parts.Add("today=" + RunTimePlayerData.GetDay().CorrectedDay + " fund=" + RunTimePlayerData.Fund);
    parts.Add("news{" + Dict(RunTimeScheduler.scheduledNews) + "}");
    parts.Add("events{" + Dict(RunTimeScheduler.scheduledEvents) + "}");
    var missions = new List<string>();
    foreach (var kv in RunTimeScheduler.trackingMissions)
      foreach (var m in kv.Value) if (Match(m.missionLabel)) missions.Add(kv.Key + ":" + Short(m.missionLabel));
    parts.Add("missions{" + string.Join(",", missions) + "}");
    var done = new List<string>();
    foreach (var e in RunTimeScheduler.finishedEvents) if (Match(e)) done.Add("E:" + Short(e));
    foreach (var e in RunTimeScheduler.finishedMissions) if (Match(e)) done.Add("M:" + Short(e));
    foreach (var n in RunTimeScheduler.newsData) if (Match(n.newsLabel)) done.Add("N:" + Short(n.newsLabel) + "@" + n.newsDate.CorrectedDay);
    parts.Add("done{" + string.Join(",", done) + "}");
    var notLoaded = RunTimePlayerData.NotLoadedDLCSchedulerSaveData;
    var keys = new List<string>();
    if (notLoaded != null) foreach (var kv in notLoaded) keys.Add(kv.Key + "@" + kv.Value.dlcSaveDate);
    parts.Add("notLoaded{" + (notLoaded == null ? "null" : string.Join(",", keys)) + "}");
    return string.Join(" ", parts);
  }
  static string Dict(Il2CppSystem.Collections.Generic.Dictionary<int, Il2CppSystem.Collections.Generic.List<string>> d) {
    var items = new List<string>();
    foreach (var kv in d) foreach (var s in kv.Value) if (Match(s)) items.Add(kv.Key + ":" + Short(s));
    return string.Join(",", items);
  }
  static string Short(string s) => s.StartsWith(Filter) ? s.Substring(Filter.Length) : s;
}
