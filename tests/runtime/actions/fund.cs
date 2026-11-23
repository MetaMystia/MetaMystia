// 改 ID 号登记顾客的资金（只在房主执行；客机的值由房主同步覆盖）。VALUE 只用于 set、max。
// MODE=set     当前资金设为 VALUE（普通客人资金不足时首单点单失败、续单不续）
// MODE=max     稀客资金上限设为 VALUE。设为 0 后，下一次续单判定为超出承受（愤怒离场）
// MODE=nomoney 稀客：按本单 serve MODE=good 将上的菜酒价格，把资金上限设到「超出资金但未超出承受」，下一次续单判定为资金不足
public static class Program {
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.WorkScene) return "not in night scene: " + MetaMystia.GameFlow.LocalScene;
    if (!MetaMystia.Multiplayer.GameSession.IsRoomHost) return "host only";
    int id = @@ID@@; string mode = "@@MODE@@"; int value = @@VALUE@@;
    var g = MetaMystia.Multiplayer.Guests.GuestRegistry.Current.Find(id);
    if (g == null || g.Controller == null) return "no guest #" + id;
    var c = g.Controller;
    switch (mode) {
      case "set": c.GetFund = value; break;
      case "max": c.MaxFundCarry = value; break;
      case "nomoney": {
        var order = g.CurrentOrder;
        var special = order == null ? null : order.TryCast<NightScene.GuestManagementUtility.GuestsManager.SpecialOrder>();
        if (special == null) return "#" + id + " has no special order";
        var cfg = GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance;
        int price = -1, bev = -1;
        foreach (var r in cfg.DailyRecipes) {
          var f = GameData.Core.Collections.DataBaseCore.AsNewFood(r.foodID);
          if (System.Array.IndexOf((int[])f.Tags, special.RequestFoodTag) >= 0) { price = f.TrueValue; break; }
        }
        if (price < 0) price = GameData.Core.Collections.DataBaseCore.AsNewFood(cfg.DailyRecipes[0].foodID).TrueValue;
        foreach (var b in cfg.DailyBeverages) if (System.Array.IndexOf((int[])b.Tags, special.RequestBeverageTag) >= 0) { bev = b.TrueValue; break; }
        if (bev < 0) bev = cfg.DailyBeverages[0].TrueValue;
        int spent = price + bev;
        foreach (var o in Il2CppSystem.Linq.Enumerable.ToArray(c.AllOrders)) if (!o.FreeOrder) spent += o.Price;
        float limit = c.TryCast<NightScene.GuestManagementUtility.SpecialGuestsController>().EnduranceLimit;
        int total = (int)System.Math.Ceiling(spent / limit);
        if (total >= spent) return "#" + id + " cannot fit: spent=" + spent + " endurance=" + limit;
        c.MaxFundCarry = total - c.ExtraFundByBuff;
        break;
      }
      default: return "unknown mode " + mode;
    }
    return "#" + id + " fund=" + c.GetFund + " max=" + c.MaxFundCarry + " extra=" + c.ExtraFundByBuff;
  }
}
