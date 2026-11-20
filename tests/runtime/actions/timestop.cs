// 本端请求或解除冻结（原版面板打开时走的同一入口）。MODE=Freeze|Resume。联机营业中由房间汇总后才真正冻结。
public static class Program {
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.WorkScene) return "not in night scene: " + MetaMystia.GameFlow.LocalScene;
    Common.TimelineExtestion.GameTimeManager.Instance.SetGameTimeMode(Common.TimelineExtestion.GameTimeManager.TimeMode.@@MODE@@);
    return "@@MODE@@ requested; timeScale=" + UnityEngine.Time.timeScale;
  }
}
