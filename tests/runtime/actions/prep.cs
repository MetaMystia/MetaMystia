// 备菜场景：MODE=info 看当日配置；MODE=go 提交原版「前往营业」（联机时等全员就绪）。
public static class Program {
  public static object Execute() {
    var cfg = GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance;
    var panel = MetaMystia.Patch.IzakayaConfigPannelPatch.instanceRef;
    string info = "panel=" + (panel != null)
      + " recipes=" + (cfg == null ? -1 : cfg.DailyRecipes.Count)
      + " beverages=" + (cfg == null ? -1 : cfg.DailyBeverages.Count)
      + " prepOver=" + MetaMystia.PlayerManager.Local.IsPrepOver;
    if ("@@MODE@@" != "go") return info;
    if (panel == null) return info + " (panel not open)";
    panel._SolveDailyCompletion_b__64_7();
    return info + " submitted";
  }
}
