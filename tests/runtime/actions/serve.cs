// 上菜面板。DESK=桌号。
// MODE=open   打开该桌上菜面板（原版桌位互动入口）；面板在之后的帧才就绪，用 night.cs 的 panel= 确认
// MODE=send   当日第一道菜和第一种酒水放进托盘，送入已打开的面板，再正常关闭（ClosePanel，走确认上菜）
// MODE=stage  同 send 放入托盘并送入面板，但不关闭面板（留着已选物品）
// MODE=stage2 同 stage，但用当日第二道菜和第二种酒水（与 stage 的物品内容不同）
// MODE=close  正常关闭面板（ClosePanel）
// MODE=cancel 取消关闭（CloseExternPanel）
// MODE=repell 驱赶该桌客人（PlayerRepell）
public static class Program {
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
      case "send":
        if (panel == null) return "no panel";
        var cfg = GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance;
        int pick = mode == "stage2" ? 1 : 0;
        int fid = cfg.DailyRecipes[pick].foodID, bid = cfg.DailyBeverages[pick].Id;
        var tray = GameData.RunTime.NightSceneUtility.IzakayaTray.Instance;
        var food = GameData.Core.Collections.DataBaseCore.AsNewFood(fid);
        var bev = GameData.Core.Collections.DataBaseCore.AsNewBeverage(bid);
        tray.Receive(food); tray.Receive(bev);
        panel.Send(food); panel.Send(bev);
        if (mode != "send") return "staged food " + fid + " bev " + bid + " in panel";
        panel.ClosePanel();
        return "served food " + fid + " bev " + bid + " at desk " + desk;
    }
    return "unknown mode " + mode;
  }
}
