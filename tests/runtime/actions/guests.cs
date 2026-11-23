// 顾客同步一览：连击、断连击次数、本晚收入、排队组数，以及登记表中未离场的顾客 #编号:阶段@桌号/订单序号（+n 为待应用事件数）。
// 两端对照同一编号的阶段和订单序号；入座、吃饭途中可能暂时不同。单机没有登记的顾客。
public static class Program {
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.WorkScene) return "not in night scene: " + MetaMystia.GameFlow.LocalScene;
    var em = NightScene.EventUtility.EventManager.Instance;
    var inbox = typeof(MetaMystia.Multiplayer.Guests.GuestRecord).GetField("Inbox", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var sb = new System.Text.StringBuilder("combo=" + em.CurrentCombo + " lose=" + em.LoseComboTimes + " earned=" + em.EarnedFund
      + " queue=" + NightScene.GuestManagementUtility.GuestGroupController.QueuedGuestControllers.Count + " guests=");
    foreach (var g in MetaMystia.Multiplayer.Guests.GuestRegistry.Current.All) {
      if (g.IsLeft) continue;
      int n = ((System.Collections.IList)inbox.GetValue(g)).Count;
      sb.Append("#" + g.RuntimeId + ":" + g.State + "@" + g.DeskCode + "/" + g.OrderSeq + (n > 0 ? "+" + n : "") + " ");
    }
    return sb.ToString().TrimEnd();
  }
}
