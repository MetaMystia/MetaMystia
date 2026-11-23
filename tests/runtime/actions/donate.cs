// 白天在博丽神社给赛钱箱纳贡 AMOUNT 円（先 shrine MODE=travel）。调用原版 DonateBecomeBuff：扣钱、累计纳贡、灵梦好感、安排夜间 buff；跳过对话、按钮和每天一次的检查。
// 500 円以上安排灵梦保护（营业开始前生成灵梦，施放护盾后离场），1000 円以上另加「实惠」料理加速。累计达到最终金额会触发剧情，用前看 shrine 的 donated=。
public static class Program {
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.DayScene) return "not in day scene: " + MetaMystia.GameFlow.LocalScene;
    int amount = @@AMOUNT@@;
    var boxes = UnityEngine.Object.FindObjectsOfType<DayScene.Interactables.Collections.BehaviourComponents.HakureiMoneyBoxBehaviourComponent>();
    if (boxes.Length == 0) return "money box not loaded";
    long fund = GameData.RunTime.Common.RunTimePlayerData.GetFund();
    int pending = NightScene.SceneManager.AdditiveBuff.Count;
    boxes[0].DonateBecomeBuff(amount);
    return "donated " + amount + " fund " + fund + "->" + GameData.RunTime.Common.RunTimePlayerData.GetFund()
      + " total=" + GameData.RunTime.Common.RunTimePlayerData.GetHakureiMoneyBoxDonateNum
      + " pending " + pending + "->" + NightScene.SceneManager.AdditiveBuff.Count;
  }
}
