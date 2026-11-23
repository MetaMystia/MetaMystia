// 托盘内容：每格 类型:编号，空格为 -。
public static class Program {
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.WorkScene) return "not in night scene: " + MetaMystia.GameFlow.LocalScene;
    var tray = GameData.RunTime.NightSceneUtility.IzakayaTray.Instance.Tray;
    var sb = new System.Text.StringBuilder("tray=");
    var els = tray.Elements;
    for (int i = 0; i < els.Length; i++) { var s = els[i]; sb.Append(s == null ? "- " : s.Type + ":" + s.Id + " "); }
    return sb.ToString().TrimEnd();
  }
}
