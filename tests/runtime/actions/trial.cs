// 白天：提交「重修最终试炼」意向。等同和幽幽子对话选挑战并确认（原版确认回调），联机时被模组拦截为意向，全员都提交后房主确认，各端进入挑战。
public static class Program {
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.DayScene) return "not in day scene: " + MetaMystia.GameFlow.LocalScene;
    var data = GameData.Core.Collections.DaySceneUtility.DataBaseDay.DaySceneGetSpecialGuestExtraDialogData(40);
    var yuyuko = data == null ? null : data.TryCast<GameData.Core.Collections.DaySceneUtility.Collections.YuyukoExtraDialogData>();
    if (yuyuko == null) return "Yuyuko dialog data not loaded";
    var confirm = new GameData.Core.Collections.DaySceneUtility.Collections.YuyukoExtraDialogData.__c__DisplayClass4_0();
    confirm.__4__this = yuyuko;
    confirm._Yuyuko_Challenge_b__2(true);
    return "final trial retake intent submitted (event " + yuyuko.challengeStartEvent + ")";
  }
}
