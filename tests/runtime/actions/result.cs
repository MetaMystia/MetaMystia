// 结算场景：每调用一次前进一步。统计面板 -> 关闭；存档面板 -> 不存档直接关闭（不写存档槽）。关闭存档面板后原版加载次日白天。
// 结算场景不做联机同步，两端各自调用。
public static class Program {
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.ResultScene) return "not in result scene: " + MetaMystia.GameFlow.LocalScene;
    foreach (var p in UnityEngine.Object.FindObjectsOfType<ResultScene.UI.ResultSceneStatusPannel>()) {
      if (p.gameObject.activeInHierarchy) { p.ClosePanel(); return "status panel closed"; }
    }
    foreach (var p in UnityEngine.Object.FindObjectsOfType<ResultScene.UI.ResultSceneSavePannel>()) {
      if (p.gameObject.activeInHierarchy) { p.ClosePanel(); return "save panel closed without saving"; }
    }
    return "no result panel open yet";
  }
}
