// 房主打烊：停止刷客并走原版关店（联机时广播给客机）。客机调用会被模组拦截。
public static class Program {
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.WorkScene) return "not in night scene: " + MetaMystia.GameFlow.LocalScene;
    NightScene.EventUtility.EventManager.Instance.StopInstantiationLoopAndCloseIzakaya();
    return "close requested";
  }
}
