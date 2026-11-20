// 标题界面读取指定存档槽（文件 Mystia#<slot>.memory）：设 PlayerPrefs 上次存档槽后提交原版「继续」按钮。
// 槽位文件读不出时原版会回退到别的槽，读完用 state 核对。
// 直接复制 PlayerSaveFile 再 LoadPlayerData 会因 Il2Cpp 值类型损坏而卡在 LoadScene，不要用。
public static class Program {
  public static object Execute() {
    int slot = @@SLOT@@;
    if (!MainScene.SceneManager.hasInstance) return "not on title";
    var menu = UnityEngine.Object.FindObjectOfType<MainScene.UI.MainMenuPannel>();
    if (menu == null || menu.ContinueBtn == null || !menu.ContinueBtn.gameObject.activeInHierarchy) return "continue button not ready";
    UnityEngine.PlayerPrefs.SetInt("LAST_SAVED_SAVEFILE", slot);
    UnityEngine.PlayerPrefs.Save();
    menu.ContinueBtn.CallSubmitAction();
    return "loading slot " + slot;
  }
}
