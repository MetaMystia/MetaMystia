// MystiaLib 存档数据。NAME 为数据名（记录 ID 为 RuntimeTest/<NAME>），VERSION 为注册的版本，同一进程内同名只能注册一次。
// MODE=get 读取；MODE=set 经 Update 把 n 设为 VALUE；MODE=inject 不经 Update 把整条记录换成 VALUE（模拟其他版本或损坏的记录，其中的引号写两个）；
// MODE=raw 看载体中的记录；MODE=save 调原版存档写入槽位 VALUE。
using System;
using System.Collections.Generic;
using GameData.RunTime.Common;
using MetaMystia.MystiaLib.Saving;
using Carrier = GameData.RunTime.Common.PlayerSaveFile.DLCSchedulerSaveData;
using CarrierDictionary = Il2CppSystem.Collections.Generic.Dictionary<string, GameData.RunTime.Common.PlayerSaveFile.DLCSchedulerSaveData>;
public static class Program {
  const string Name = "@@NAME@@", Value = @"@@VALUE@@";
  static SaveEntry<Dictionary<string, int>> Entry() {
    string slot = "MystiaLib.RuntimeTest." + Name;
    if (AppDomain.CurrentDomain.GetData(slot) is SaveEntry<Dictionary<string, int>> entry) return entry;
    entry = Saves.Register<Dictionary<string, int>>("RuntimeTest", Name, @@VERSION@@);
    AppDomain.CurrentDomain.SetData(slot, entry);
    return entry;
  }
  static string Show() => Entry().Value.TryGetValue("n", out int n) ? "n=" + n : "n=default";
  public static object Execute() {
    switch ("@@MODE@@") {
      case "get": return Show();
      case "set": return "written=" + Entry().Update(d => d["n"] = int.Parse(Value)) + " " + Show();
      case "inject": Inject(); return "injected " + Raw();
      case "raw": return Raw();
      case "save": GameData.Utils.SaveManagement.WriteCurrentPlayerDataToSlotAsync(int.Parse(Value)); return "saving to slot " + Value;
      default: return "unknown mode";
    }
  }
  static string[] Records() {
    var all = RunTimePlayerData.NotLoadedDLCSchedulerSaveData;
    if (all == null) return null;
    string[] records = null;
    if (all.ContainsKey("MystiaLib")) records = all["MystiaLib"].finishedEvents;
    return records ?? new string[0];
  }
  static string Raw() { var r = Records(); return r == null ? "not loaded" : "records=" + r.Length + " " + string.Join(" | ", r); }
  static void Inject() {
    var records = new List<string>(Records());
    int index = records.FindIndex(r => r.Contains("\"RuntimeTest/" + Name + "\""));
    if (index < 0) records.Add(Value); else records[index] = Value;
    var carrier = new Carrier { scheduledEvents = new(), scheduledNews = new(), scheduledNewsReplaceContents = new(), allTrackingMissions = new(),
      finishedEvents = records.ToArray(), finishedMissions = new string[0] };
    var copy = new CarrierDictionary(RunTimePlayerData.NotLoadedDLCSchedulerSaveData.Cast<Il2CppSystem.Collections.Generic.IDictionary<string, Carrier>>());
    copy.Cast<Il2CppSystem.Collections.IDictionary>()["MystiaLib"] = carrier;
    RunTimePlayerData.NotLoadedDLCSchedulerSaveData = copy;
  }
}
