// 跳过开场图片和 OP 视频（等同按任意键）。
public static class Program {
  public static object Execute() {
    var m = UnityEngine.Object.FindObjectOfType<SplashScene.SceneManager>();
    if (m == null) return "not in splash";
    m.cancellationTokenSource?.Cancel();
    return "splash skipped";
  }
}
