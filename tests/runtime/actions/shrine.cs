// 博丽神社与灵梦保护。
// MODE=travel 白天切到博丽神社（原版 SwapMap，不扣行动点，不检查出口条件），切图完成后 info 显示 box=1
// MODE=info   白天：所在地图、资金、累计纳贡、赛钱箱是否已加载、待执行的夜间 buff 数；夜间：灵梦护盾层数与待执行的夜间 buff 数
public static class Program {
  public static object Execute() {
    string mode = "@@MODE@@";
    var local = MetaMystia.GameFlow.LocalScene;
    int pending = NightScene.SceneManager.AdditiveBuff.Count;
    if (local == Common.UI.Scene.WorkScene) {
      if (mode != "info") return "night: MODE must be info";
      var em = NightScene.EventUtility.EventManager.Instance;
      return "night shield=" + em.TryGetCountedBuffValue(NightScene.EventUtility.EventManager.BuffType.Skill_Reimu_Shield) + " pending=" + pending;
    }
    if (local != Common.UI.Scene.DayScene) return "not in day or night scene: " + local;
    var sm = DayScene.SceneManager.Instance;
    const string shrine = "HakureiShrine";
    if (mode == "travel") {
      if (sm.IsMapSwapping) return "map swapping";
      if (sm.CurrentActiveMapLabel == shrine) return "already at " + shrine;
      Il2CppSystem.Collections.Generic.HashSet<string> markers;
      if (!GameData.Core.Collections.DaySceneUtility.DataBaseDay.allSpawnMarkerLabels.TryGetValue(shrine, out markers)) return "no spawn markers for " + shrine;
      string marker = null;
      foreach (var m in markers) { marker = m; break; }
      if (marker == null) return "no spawn markers for " + shrine;
      sm.SwapMap(shrine, marker, 0, true, true, false, null);
      return "travel requested " + shrine + " marker=" + marker;
    }
    if (mode != "info") return "MODE must be travel|info";
    var boxes = UnityEngine.Object.FindObjectsOfType<DayScene.Interactables.Collections.BehaviourComponents.HakureiMoneyBoxBehaviourComponent>();
    return "day map=" + sm.CurrentActiveMapLabel + " swapping=" + sm.IsMapSwapping
      + " fund=" + GameData.RunTime.Common.RunTimePlayerData.GetFund()
      + " donated=" + GameData.RunTime.Common.RunTimePlayerData.GetHakureiMoneyBoxDonateNum
      + " box=" + boxes.Length + " pending=" + pending;
  }
}
