// 单个登记顾客。ID=编号。
// MODE=info   阶段、心情、资金、剩余点单、耐心/上限、订单需求与已上菜（料理/酒水编号）、待上菜槽位、本端放入（claim）与已确认（conf）、待应用事件数
// MODE=forget 客机：本地强制清理并移出登记表，模拟漏收该顾客；之后主机发来该顾客的事件时请求快照重建
// MODE=unslot 清空本端记录的待上菜槽位和面板显示，不动托盘，模拟尚未收到对方的放入；房主执行即改变裁定依据
public static class Program {
  static string Id(GameData.Core.Collections.Sellable s) { return s == null ? "-" : s.Id.ToString(); }
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.WorkScene) return "not in night scene: " + MetaMystia.GameFlow.LocalScene;
    int id = @@ID@@; string mode = "@@MODE@@";
    var reg = MetaMystia.Multiplayer.Guests.GuestRegistry.Current;
    var g = reg.Find(id);
    if (g == null) return "no guest #" + id;
    var c = g.Controller;
    switch (mode) {
      case "info":
        var inbox = typeof(MetaMystia.Multiplayer.Guests.GuestRecord).GetField("Inbox", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var o = g.CurrentOrder;
        return "#" + id + " " + g.State + " desk=" + g.DeskCode + " seq=" + g.OrderSeq
          + (c == null ? " no-controller" : " mood=" + c.Mood + " fund=" + c.GetFund + " remain=" + c.remainOrderCount + " pat=" + c.CurrentPatient + "/" + c.MaxPatient)
          + " order=" + (o == null ? "-" : o.foodRequest + "/" + o.beverageRequest + " served=" + Id(o.ServFood) + "/" + Id(o.ServBeverage))
          + " slot=" + Id(g.WillServeFood) + "/" + Id(g.WillServeBeverage)
          + " claim=" + Id(g.ClaimFood) + "/" + Id(g.ClaimBeverage)
          + " conf=" + Id(g.ConfirmedFood) + "/" + Id(g.ConfirmedBeverage)
          + " inbox=" + ((System.Collections.IList)inbox.GetValue(g)).Count;
      case "forget":
        if (!MetaMystia.Multiplayer.GameSession.IsRoomClient) return "client only";
        reg.Remove(g);
        if (c != null) MetaMystia.Multiplayer.Guests.GuestReplay.ForceCleanup(c);
        return "forgot #" + id;
      case "unslot":
        g.WillServeFood = null;
        g.WillServeBeverage = null;
        MetaMystia.Multiplayer.Guests.ServeSlots.UpdatePanel(g.DeskCode, null, GameData.Core.Collections.Sellable.SellableType.Food, true);
        MetaMystia.Multiplayer.Guests.ServeSlots.UpdatePanel(g.DeskCode, null, GameData.Core.Collections.Sellable.SellableType.Beverage, true);
        return "unslotted #" + id;
    }
    return "unknown mode " + mode;
  }
}
