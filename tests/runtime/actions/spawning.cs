// 原版自然来客开关（只在房主上）：MODE=off 停止普通、路人与稀客的自然生成，on 恢复，info 只查看。手动 spawn 不受影响。
public static class Program {
  public static object Execute() {
    var em = NightScene.EventUtility.EventManager.Instance;
    if (em == null) return "no EventManager";
    string mode = "@@MODE@@";
    if (mode == "off") em.ShouldGuestSpawn = false;
    else if (mode == "on") em.ShouldGuestSpawn = true;
    else if (mode != "info") return "MODE must be off|on|info";
    return "natural spawn=" + em.ShouldGuestSpawn;
  }
}
