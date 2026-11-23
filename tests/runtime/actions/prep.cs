// 备菜场景：MODE=info 看当日配置；MODE=go 提交原版「前往营业」（联机时等全员就绪）。
// MODE=fill 只填空着的配置（最终试炼的备菜默认全空）：每个厨具位放一件拥有的厨具（尽量不同类型），再登记这些厨具能做的最高等级料理（最多 20 道）和库存里等级最高的酒水（最多 15 种）。提交时原版按店铺上限截取。
public static class Program {
  public static object Execute() {
    var cfg = GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance;
    var panel = MetaMystia.Patch.IzakayaConfigPannelPatch.instanceRef;
    string mode = "@@MODE@@";
    if (mode == "fill" && cfg != null) Fill(cfg, 20);
    int cookers = 0;
    if (cfg != null) foreach (var c in cfg.CookerConfigure) if (c >= 0) cookers++;
    string info = "panel=" + (panel != null)
      + " recipes=" + (cfg == null ? -1 : cfg.DailyRecipes.Count)
      + " beverages=" + (cfg == null ? -1 : cfg.DailyBeverages.Count)
      + " cookers=" + cookers
      + " prepOver=" + MetaMystia.PlayerManager.Local.IsPrepOver;
    if (mode != "go") return info;
    if (panel == null) return info + " (panel not open)";
    panel._SolveDailyCompletion_b__64_7();
    return info + " submitted";
  }
  static void Fill(GameData.RunTime.NightSceneUtility.IzakayaConfigure cfg, int maxRecipes) {
    var slots = cfg.CookerConfigure;
    var owned = new System.Collections.Generic.List<(int id, int type, int left)>();
    foreach (var kv in GameData.RunTime.Common.RunTimeStorage.GetAllCookers()) owned.Add((kv.Key.Id, (int)kv.Key.Type, kv.Value));
    var types = new System.Collections.Generic.HashSet<int>();
    for (int i = 0; i < slots.Length; i++) {
      if (slots[i] >= 0) continue;
      int pick = -1;
      for (int k = 0; k < owned.Count && pick < 0; k++) if (owned[k].left > 0 && !types.Contains(owned[k].type)) pick = k;
      for (int k = 0; k < owned.Count && pick < 0; k++) if (owned[k].left > 0) pick = k;
      if (pick < 0) break;
      var o = owned[pick]; owned[pick] = (o.id, o.type, o.left - 1); types.Add(o.type);
      cfg.RegisterToCookers(o.id, i, false);
    }
    var cookerTypes = new System.Collections.Generic.HashSet<int>();
    foreach (var c in slots) if (c >= 0) cookerTypes.Add((int)GameData.Core.Collections.DataBaseCore.RefCooker(c).Type);
    if (cfg.DailyRecipes.Count == 0) {
      var recipes = new System.Collections.Generic.List<GameData.Core.Collections.Recipe>();
      foreach (var r in GameData.RunTime.Common.RunTimeStorage.GetAllRecipes()) if (cookerTypes.Contains((int)r.CookerType)) recipes.Add(r);
      recipes.Sort((a, b) => b.Food.Level.CompareTo(a.Food.Level));
      for (int i = 0; i < recipes.Count && i < maxRecipes; i++) cfg.RegisterToDailyRecipes(recipes[i].Id, false);
    }
    if (cfg.DailyBeverages.Count == 0) {
      var bevs = new System.Collections.Generic.List<GameData.Core.Collections.Sellable>();
      foreach (var kv in GameData.RunTime.Common.RunTimeStorage.GetAllBeverages()) if (kv.Value > 0) bevs.Add(kv.Key);
      bevs.Sort((a, b) => b.Level.CompareTo(a.Level));
      for (int i = 0; i < bevs.Count && i < 15; i++) cfg.RegisterToDailyBeverages(bevs[i].Id, false);
    }
    cfg.PreCalculateRecipes();
  }
}
