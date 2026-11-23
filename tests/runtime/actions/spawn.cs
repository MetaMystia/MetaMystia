// 房主生成顾客，客机调用被模组拦截。COUNT=组数。坐满后排队，队满后离开。返回新登记的编号。
// MODE=normal  原版自动刷客入口（经模组重抽与广播）
// MODE=broke   同 normal，生成后把资金设为 0（首单点不起）
// MODE=group   两人一组的普通客人（随机抽两位当前可用的普通客人）
// MODE=special 从当日稀客池生成稀客（模组会换掉房间内有人不可用的）
public static class Program {
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.WorkScene) return "not in night scene: " + MetaMystia.GameFlow.LocalScene;
    var gm = NightScene.GuestManagementUtility.GuestsManager.Instance;
    var reg = MetaMystia.Multiplayer.Guests.GuestRegistry.Current;
    string mode = "@@MODE@@"; int count = @@COUNT@@;
    int before = 0;
    foreach (var g in reg.All) if (g.RuntimeId > before) before = g.RuntimeId;
    for (int i = 0; i < count; i++) {
      switch (mode) {
        case "normal":
        case "broke":
          gm.SpawnNormalGuestGroup();
          break;
        case "group": {
          var cook = NightScene.CookingUtility.CookSystemManager.Instance;
          var list = new Il2CppSystem.Collections.Generic.List<GameData.Core.Collections.NightSceneUtility.NormalGuest>();
          for (int t = 0; t < 20 && list.Count < 2; t++)
            foreach (var x in Il2CppSystem.Linq.Enumerable.ToArray(cook.GetRandomNormalGuestGroups())) if (list.Count < 2) list.Add(x);
          gm.SpawnNormalGuestGroup(list.Cast<Il2CppSystem.Collections.Generic.IEnumerable<GameData.Core.Collections.NightSceneUtility.NormalGuest>>(),
            new Il2CppSystem.Nullable<UnityEngine.Vector3>(), NightScene.GuestManagementUtility.GuestGroupController.LeaveType.Move, -1, true);
          break;
        }
        case "special": {
          var pool = GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance.SpecialGuestPoolIdentityData;
          if (pool == null || pool.Length == 0) return "empty special pool";
          gm.SpawnSpecialGuestGroup(pool[UnityEngine.Random.Range(0, pool.Length)], NightScene.GuestManagementUtility.SpecialGuestsController.GuestSpawnType.Normal,
            new Il2CppSystem.Nullable<UnityEngine.Vector3>(), null, NightScene.GuestManagementUtility.GuestGroupController.LeaveType.Move, true, -1, false, null, true);
          break;
        }
        default: return "unknown mode " + mode;
      }
    }
    var sb = new System.Text.StringBuilder("spawned");
    foreach (var g in reg.All) {
      if (g.RuntimeId <= before) continue;
      if (mode == "broke" && g.Controller != null) g.Controller.GetFund = 0;
      sb.Append(" #" + g.RuntimeId + (g.Controller == null ? "" : " " + g.Controller.ControllType + "x" + Il2CppSystem.Linq.Enumerable.ToArray(g.Controller.GetAllGuests()).Length + " fund=" + g.Controller.GetFund));
    }
    return sb.Append(" occ=" + gm.occupiedDesks.Count + " queue=" + NightScene.GuestManagementUtility.GuestGroupController.QueuedGuestControllers.Count).ToString();
  }
}
