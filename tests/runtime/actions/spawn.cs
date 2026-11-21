// 房主按原版自动刷客入口生成 COUNT 组普通客人（经模组重抽与广播）。坐满后排队，队满后离开。客机调用被模组拦截。
public static class Program {
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.WorkScene) return "not in night scene: " + MetaMystia.GameFlow.LocalScene;
    var gm = NightScene.GuestManagementUtility.GuestsManager.Instance;
    int count = @@COUNT@@;
    for (int i = 0; i < count; i++) gm.SpawnNormalGuestGroup();
    return "spawned " + count + " occ=" + gm.occupiedDesks.Count
      + " queue=" + NightScene.GuestManagementUtility.GuestGroupController.QueuedGuestControllers.Count;
  }
}
