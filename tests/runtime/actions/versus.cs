// 料理对决：MODE=menu 列出当前模组选项菜单（顶层面板、各项标题与可用性）；
// MODE=pick INDEX=n 点击第 n 项（与玩家点击相同：关闭面板后执行选项）；
// MODE=state 输出队伍表、入口与本局状态；MODE=partner 确认伙伴选择面板的当前项（与长按确认相同）；
// MODE=result 关闭结算面板（与确认按钮相同）。
using System.Linq;
using System.Reflection;

public static class Program {
  const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

  static System.Collections.IList Items() {
    var menu = typeof(MetaMystia.VersusManager).Assembly.GetType("MetaMystia.UI.DaySceneSelectionMenu");
    return (System.Collections.IList)menu.GetField("Items", Any).GetValue(null);
  }

  static object Field(object item, string name) => item.GetType().GetField(name, Any).GetValue(item);

  public static object Execute() {
    string mode = "@@MODE@@";
    string top = SgrYuki.Utils.Panel.TopPanelName ?? "none";
    if (mode == "state") {
      var teams = string.Join(",", MetaMystia.VersusRoster.Teams.Select(p => p.Key + ":" + p.Value));
      var confirmed = string.Join(",", MetaMystia.VersusRoster.Confirmed);
      var missing = string.Join(",", MetaMystia.VersusRoster.MissingDlc3);
      var ev = NightScene.EventUtility.EventManager.Instance;
      return "open=" + MetaMystia.VersusRoster.Open + " invite=" + MetaMystia.VersusRoster.Invite
        + " tempo=" + MetaMystia.VersusRoster.Tempo + " teams=" + teams + " confirmed=" + confirmed + " noDlc3=" + missing
        + " intent=" + MetaMystia.DayDestinationManager.LocalIntent + " dest=" + MetaMystia.GameFlow.Destination
        + " challenge=" + NightScene.NightSceneDirector.ChallengeMode + " difficulty=" + NightScene.NightSceneDirector.DifficultyMode
        + " waiting=" + MetaMystia.BusinessStart.IsWaitingForStart
        + " countdown=" + (ev == null ? "-" : ev.TotalCountDown.ToString())
        + " top=" + top;
    }
    if (mode == "partner") {
      var panel = UnityEngine.Object.FindObjectOfType<DLC3_BossBattleEmployeeSelectorPannel>();
      if (panel == null || !panel.IsPanelOpened) return "partner selector not open";
      var selected = panel.FinalSelected.id;
      panel.ClosePanel();
      return "partner confirmed " + selected;
    }
    if (mode == "result") {
      var panel = UnityEngine.Object.FindObjectOfType<DLC3_BossBattleResultPannel>();
      if (panel == null || !panel.IsPanelOpened) return "result panel not open";
      panel.ClosePanel();
      return "result closed";
    }
    var items = Items();
    if (mode == "menu") {
      var lines = new System.Collections.Generic.List<string> { "top=" + top + " input=" + DEYU.AdpUISystem.Managers.AdpUIPanelManager.CurrentInputMode };
      for (int i = 0; i < items.Count; i++) {
        var title = (System.Func<string>)Field(items[i], "_getTitle");
        var available = (System.Func<bool>)Field(items[i], "_isAvailable");
        var data = Field(items[i], "_data");
        lines.Add(i + ": " + (title == null ? "-" : title()) + " available=" + (available != null && available()) + " shown=" + (data != null));
      }
      return string.Join("\n", lines);
    }
    if (mode == "pick") {
      int index = @@INDEX@@;
      var item = items[index];
      if (Field(item, "_data") == null) return "item " + index + " not on the open panel";
      item.GetType().GetMethod("Interact", Any).Invoke(item, null);
      return "picked " + index + " top=" + (SgrYuki.Utils.Panel.TopPanelName ?? "none");
    }
    return "unknown mode " + mode;
  }
}
