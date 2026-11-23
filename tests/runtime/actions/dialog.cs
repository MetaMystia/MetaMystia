// 原版对话：MODE=info 看是否有对话在播；MODE=skip 让正在播的对话一直快进（等同长按跳过），直到结束。
public static class Program {
  public static object Execute() {
    bool open = Common.UI.UniversalGameManager.s_IsDialogPanelOpened;
    if ("@@MODE@@" != "skip" || !open) return "dialog=" + open;
    foreach (var p in UnityEngine.Object.FindObjectsOfType<Common.DialogUtility.DialogPannel>())
      if (p.gameObject.activeInHierarchy) p.shouldKeepSkipping = true;
    return "dialog=True skipping";
  }
}
