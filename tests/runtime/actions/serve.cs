// 上菜面板。DESK=桌号。
// MODE=open   打开该桌上菜面板（原版桌位互动入口）；面板在之后的帧才就绪，用 night.cs 的 panel= 确认
// MODE=send   当日第一道菜和第一种酒水放进托盘，送入已打开的面板，再正常关闭（ClosePanel，走确认上菜）
// MODE=stage  同 send 放入托盘并送入面板，但不关闭面板（留着已选物品）
// MODE=stage2 同 stage，但用当日第二道菜和第二种酒水（与 stage 的物品内容不同）
// MODE=good   按订单上对的菜酒并关闭：普通客人用所点料理和酒水；稀客用带所需标签的当日料理和酒水（没有时用第一种）
// MODE=bad    上不符合订单的菜酒并关闭：普通客人用不同于所点的当日料理和酒水；稀客用不带所需标签的
// MODE=goodfood / goodbev  只放 good 中的料理或酒水并关闭（分次上菜）
// MODE=close  正常关闭面板（ClosePanel）
// MODE=cancel 取消关闭（CloseExternPanel）
// MODE=repell 驱赶该桌客人（PlayerRepell）
public static class Program {
  static void Stage(NightScene.UI.GuestManagementUtility.WorkSceneServePannel panel, int fid, int bid) {
    var tray = GameData.RunTime.NightSceneUtility.IzakayaTray.Instance;
    if (fid >= 0) { var food = GameData.Core.Collections.DataBaseCore.AsNewFood(fid); tray.Receive(food); panel.Send(food); }
    if (bid >= 0) { var bev = GameData.Core.Collections.DataBaseCore.AsNewBeverage(bid); tray.Receive(bev); panel.Send(bev); }
  }
  static bool Has(int[] tags, int tag) { foreach (var t in tags) if (t == tag) return true; return false; }
  // 普通订单点名料理和酒水；稀客订单按标签。
  static void Pick(NightScene.GuestManagementUtility.GuestsManager.OrderBase order, bool good, out int fid, out int bid) {
    var cfg = GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance;
    var recipes = cfg.DailyRecipes; var bevs = cfg.DailyBeverages;
    fid = recipes[0].foodID; bid = bevs[0].Id;
    var special = order.TryCast<NightScene.GuestManagementUtility.GuestsManager.SpecialOrder>();
    if (special == null) {
      if (good) { fid = order.foodRequest; bid = order.beverageRequest; return; }
      for (int i = 0; i < recipes.Count; i++) if (recipes[i].foodID != order.foodRequest) { fid = recipes[i].foodID; break; }
      for (int i = 0; i < bevs.Count; i++) if (bevs[i].Id != order.beverageRequest) { bid = bevs[i].Id; break; }
      return;
    }
    for (int i = 0; i < recipes.Count; i++) {
      var tags = (int[])GameData.Core.Collections.DataBaseCore.AsNewFood(recipes[i].foodID).Tags;
      if (Has(tags, special.RequestFoodTag) == good) { fid = recipes[i].foodID; break; }
    }
    for (int i = 0; i < bevs.Count; i++) {
      if (Has((int[])bevs[i].Tags, special.RequestBeverageTag) == good) { bid = bevs[i].Id; break; }
    }
  }
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.WorkScene) return "not in night scene: " + MetaMystia.GameFlow.LocalScene;
    int desk = @@DESK@@; string mode = "@@MODE@@";
    var gm = NightScene.GuestManagementUtility.GuestsManager.Instance;
    var panel = MetaMystia.Patch.WorkSceneServePannelPatch.instanceRef;
    switch (mode) {
      case "open": gm.ExcuteEventAtCorodinate(desk); return "open requested desk " + desk;
      case "repell": gm.PlayerRepell(desk); return "repell requested desk " + desk;
      case "close": if (panel == null) return "no panel"; panel.ClosePanel(); return "closed panel desk " + MetaMystia.Patch.WorkSceneServePannelPatch.PanelDeskCode;
      case "cancel": if (panel == null) return "no panel"; panel.CloseExternPanel(); return "cancelled panel";
      case "stage":
      case "stage2":
      case "send": {
        if (panel == null) return "no panel";
        var cfg = GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance;
        int pick = mode == "stage2" ? 1 : 0;
        int fid = cfg.DailyRecipes[pick].foodID, bid = cfg.DailyBeverages[pick].Id;
        Stage(panel, fid, bid);
        if (mode != "send") return "staged food " + fid + " bev " + bid + " in panel";
        panel.ClosePanel();
        return "served food " + fid + " bev " + bid + " at desk " + desk;
      }
      case "good":
      case "bad":
      case "goodfood":
      case "goodbev": {
        if (panel == null) return "no panel";
        var order = panel.operatingOrder;
        if (order == null) return "no order";
        int fid, bid;
        Pick(order, mode != "bad", out fid, out bid);
        if (mode == "goodfood") bid = -1;
        if (mode == "goodbev") fid = -1;
        Stage(panel, fid, bid);
        panel.ClosePanel();
        return mode + " food " + fid + " bev " + bid + " at desk " + desk;
      }
    }
    return "unknown mode " + mode;
  }
}
