// 白天：提交「去营业」意向（原版快进按钮，联机时被模组拦截为意向，房主确认全员一致后才执行）。
public static class Program {
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.DayScene) return "not in day scene: " + MetaMystia.GameFlow.LocalScene;
    DayScene.UI.UIManager.Instance.SustainedPannel.OnFastForwardSubmit();
    return "business intent submitted";
  }
}
