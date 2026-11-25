// 符卡。
// MODE=spawn  房主生成标签为 NAME 的稀客（如 Tenshi），返回编号
// MODE=red / black / leave  房主让 NAME 号登记顾客放红卡、黑卡、离场卡（原版 Trigger 入口，不经评价）
// MODE=state  锁定的桌子与厨具、符卡队列长度、执行中的符卡、护盾、延长营业计数（NAME 填 0）
public static class Program {
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.WorkScene) return "not in night scene: " + MetaMystia.GameFlow.LocalScene;
    string mode = "@@MODE@@", name = "@@NAME@@";
    var events = NightScene.EventUtility.EventManager.Instance;
    var reg = MetaMystia.Multiplayer.Guests.GuestRegistry.Current;
    if (mode == "spawn") {
      int before = 0;
      foreach (var g in reg.All) if (g.RuntimeId > before) before = g.RuntimeId;
      var guest = GameData.Core.Collections.CharacterUtility.DataBaseCharacter.RefSGuest(name);
      if (guest == null) return "no special guest " + name;
      NightScene.GuestManagementUtility.GuestsManager.Instance.SpawnSpecialGuestGroup(guest.Id,
        NightScene.GuestManagementUtility.SpecialGuestsController.GuestSpawnType.Normal, new Il2CppSystem.Nullable<UnityEngine.Vector3>(), null,
        NightScene.GuestManagementUtility.GuestGroupController.LeaveType.Move, true, -1, false, null, true);
      foreach (var g in reg.All) if (g.RuntimeId > before) return "#" + g.RuntimeId + " id=" + guest.Id + " desk=" + g.DeskCode;
      return "not spawned id=" + guest.Id;
    }
    if (mode == "state") {
      var tables = new System.Collections.Generic.List<int>();
      foreach (var t in events.LockedTables) tables.Add(t);
      tables.Sort();
      var cookers = new System.Collections.Generic.List<string>();
      foreach (var c in events.LockedCookers) cookers.Add(c.x + "," + c.y);
      cookers.Sort();
      var runs = (System.Collections.IDictionary)typeof(MetaMystia.Multiplayer.Spells.SpellRegistry)
        .GetField("runs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(MetaMystia.Multiplayer.Spells.SpellRegistry.Current);
      var running = new System.Collections.Generic.List<string>();
      foreach (var r in runs.Values) running.Add(r.ToString());
      return "tables=[" + string.Join(",", tables) + "] cookers=[" + string.Join(" ", cookers) + "] queue=" + events.spellExecutionQueue.Count
        + " runs=[" + string.Join("; ", running) + "] shield=" + events.TryGetCountedBuffValue(NightScene.EventUtility.EventManager.BuffType.Skill_Reimu_Shield)
        + " hold=" + events.RegisteredDoNotCloseIzakayaStatus + " close=" + NightScene.GuestManagementUtility.GuestsManager.Instance.isIzakayaClosing;
    }
    var record = reg.Find(int.Parse(name));
    var special = record == null || record.Controller == null ? null : record.Controller.TryCast<NightScene.GuestManagementUtility.SpecialGuestsController>();
    if (special == null) return "no special guest #" + name;
    switch (mode) {
      case "red": special.TriggerPositiveBuff(null, true); break;
      case "black": special.TriggerNegativeBuff(null, true); break;
      case "leave": special.TriggerLeaveBuff(null); break;
      default: return "unknown mode " + mode;
    }
    return mode + " " + special.SpecialGuest.StringId + " queue=" + events.spellExecutionQueue.Count;
  }
}
