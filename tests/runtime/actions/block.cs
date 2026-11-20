// 阻塞本实例主线程 SECONDS 秒（上限 50），模拟卡顿或晚到。期间该实例不处理网络、不推进画面。
public static class Program {
  public static object Execute() {
    int s = @@SECONDS@@; if (s > 50) s = 50;
    System.Threading.Thread.Sleep(s * 1000);
    return "blocked " + s + "s";
  }
}
