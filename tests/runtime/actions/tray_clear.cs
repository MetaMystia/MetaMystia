// 清空本端托盘（原版 ClearTray），用于放入类用例前腾出空位；serve 的 stage 会先把新物品放进托盘再送入面板。
public static class Program {
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.WorkScene) return "not in night scene: " + MetaMystia.GameFlow.LocalScene;
    var removed = Il2CppSystem.Linq.Enumerable.ToArray(GameData.RunTime.NightSceneUtility.IzakayaTray.Instance.ClearTray());
    return "cleared " + removed.Length;
  }
}
