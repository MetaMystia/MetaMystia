// 把 ID 号登记顾客的当前耐心设为 VALUE（原版 SetPatient）。房主设为 0 时下一次耐心更新即判定耗尽；客机的设置会被主机下一次数值覆盖。
public static class Program {
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.WorkScene) return "not in night scene: " + MetaMystia.GameFlow.LocalScene;
    var g = MetaMystia.Multiplayer.Guests.GuestRegistry.Current.Find(@@ID@@);
    if (g == null || g.Controller == null) return "no guest #@@ID@@";
    g.Controller.SetPatient(@@VALUE@@);
    return "#@@ID@@ pat=" + g.Controller.CurrentPatient + "/" + g.Controller.MaxPatient;
  }
}
