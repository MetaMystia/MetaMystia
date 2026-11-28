// 料理对决：MODE=menu 列出当前模组选项菜单（顶层面板、各项标题与可用性）；
// MODE=pick INDEX=n 点击第 n 项（与玩家点击相同：关闭面板后执行选项）；
// MODE=state 输出队伍表、入口与本局状态；MODE=collab 不在妖怪兽道时切过去，联动菜单已开时列出各项，否则与玩家在联动交互点交互相同（原版 OnInteract，先播对话）；
// MODE=collabpick 在联动菜单点「料理对决」；
// MODE=result 关闭结算面板（与确认按钮相同）；MODE=desks 本端占桌表（玩家方、Boss 方）、比分、连击与上菜面板桌号；
// MODE=stock 库存条目数与校验和、QTE 增益是否关闭、移速增益数；MODE=warmer INDEX=料理ID 放入保温箱；MODE=warmers 保温箱数量；
// MODE=cook INDEX=格子 用当日第一道料理开火并开始计时（与 QTE 结束相同）；MODE=cooker INDEX=格子 厨具阶段、成品、本端能否取出与托盘件数；
// MODE=take INDEX=格子 与玩家在该厨具上交互相同（原版 CallCooker：做好时取到托盘）；
// MODE=interact 调原版夜间交互输入（WorkScenePlayerInputGenerator.Interact），输出是否开始了交互协程。
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
    if (mode == "collab") {
      var scene = DayScene.SceneManager.Instance;
      var points = UnityEngine.Object.FindObjectsOfType<DayScene.Interactables.Collections.BehaviourComponents.CollabBehaviourComponent>();
      if (points.Length == 0) {
        string marker = null;
        foreach (var m in GameData.Core.Collections.DaySceneUtility.DataBaseDay.allSpawnMarkerLabels["BeastForest"]) { marker = m; break; }
        scene.SwapMap("BeastForest", marker, 0, true, true, false, null);
        return "no collab point on " + scene.CurrentActiveMapLabel + ", moving to BeastForest";
      }
      var chat = UnityEngine.Object.FindObjectOfType<DayScene.UI.DaySceneChatSelectionPannel>();
      if (chat != null && chat.IsPanelOpened) {
        var texts = new System.Collections.Generic.List<string>();
        foreach (var t in chat.GetComponentsInChildren<TMPro.TextMeshProUGUI>(false)) if (!string.IsNullOrWhiteSpace(t.text)) texts.Add(t.text);
        return "menu: " + string.Join(" | ", texts);
      }
      points[0].OnInteract();
      return "collab interacted, top=" + (SgrYuki.Utils.Panel.TopPanelName ?? "none");
    }
    if (mode == "collabpick") {
      var item = typeof(MetaMystia.VersusManager).GetField("CollabMenuItem", Any).GetValue(null);
      if (Field(item, "_data") == null) return "versus item not on the open panel";
      if (!((System.Func<bool>)Field(item, "_isAvailable"))()) return "versus item unavailable";
      item.GetType().GetMethod("Interact", Any).Invoke(item, null);
      return "picked versus top=" + (SgrYuki.Utils.Panel.TopPanelName ?? "none");
    }
    if (mode == "interact") {
      var gen = UnityEngine.Object.FindObjectOfType<NightScene.Input.WorkScenePlayerInputGenerator>();
      if (gen == null) return "no input generator";
      var process = typeof(NightScene.Input.WorkScenePlayerInputGenerator).GetProperty("onWillExecuteInteractActionProcess", Any);
      process.SetValue(gen, null);
      typeof(NightScene.Input.WorkScenePlayerInputGenerator).GetMethod("Interact", Any).Invoke(gen, new object[] { new UnityEngine.InputSystem.InputAction.CallbackContext() });
      return "interact started=" + (process.GetValue(gen) != null) + " spectating=" + MetaMystia.VersusMatch.IsSpectating;
    }
    if (mode == "result") {
      var panel = UnityEngine.Object.FindObjectOfType<DLC3_BossBattleResultPannel>();
      if (panel == null || !panel.IsPanelOpened) return "result panel not open";
      panel.ClosePanel();
      return "result closed";
    }
    if (mode == "desks") {
      var pm = NightScene.PartnerUtility.PartnerManager.Instance;
      var em = NightScene.EventUtility.EventManager.Instance;
      if (pm == null || em == null) return "not in night scene";
      var mine = new System.Text.StringBuilder();
      foreach (var desk in pm.PlayerOccupiedDeskCodeList) mine.Append(desk + ",");
      var other = new System.Text.StringBuilder();
      foreach (var pair in pm.BossOccupiedDeskCodeList) other.Append(pair.Key + ":" + pair.Value + ",");
      return "player=" + mine + " boss=" + other + " earned=" + em.EarnedFund + " bossScore=" + pm.BossGetScore
        + " combo=" + em.CurrentCombo + " bossCombo=" + pm.BossGetCombo + " panel=" + MetaMystia.Patch.WorkSceneServePannelPatch.PanelDeskCode
        + " bossInfo=" + pm.BossPartnerMatching.Count;
    }
    if (mode == "stock") {
      long ing = 0, bev = 0; int ingCount = 0, bevCount = 0;
      foreach (var p in GameData.RunTime.Common.RunTimeStorage.Ingredients) { ing += p.Key * 31L + p.Value; ingCount++; }
      foreach (var p in GameData.RunTime.Common.RunTimeStorage.Beverages) { bev += p.Key * 31L + p.Value; bevCount++; }
      var qte = NightScene.CookingUtility.QTERewardManager.Instance;
      return "ingredients=" + ingCount + "/" + ing + " beverages=" + bevCount + "/" + bev
        + " qteOff=" + (qte != null && qte.disableReward) + " speedBuffs=" + NightScene.EventUtility.EventManager.Instance.registeredExtraMoveSpeedModifier.Count;
    }
    var cfg = GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance;
    if (mode == "warmer") {
      cfg.StoreFood(GameData.Core.Collections.DataBaseCore.AsNewFood(@@INDEX@@), -1);
      return "stored " + @@INDEX@@;
    }
    if (mode == "warmers") {
      return "warmer=" + Il2CppSystem.Linq.Enumerable.Count(cfg.GetStoredFoods());
    }
    if (mode == "cook" || mode == "cooker" || mode == "take") {
      var cooker = MetaMystia.CookManager.GetCookerControllerByIndex(@@INDEX@@);
      if (cooker == null) return "no cooker " + @@INDEX@@;
      if (mode == "cook") {
        var recipe = cfg.DailyRecipes[0];
        cooker.SetCook(GameData.Core.Collections.DataBaseCore.AsNewFood(recipe.foodID), recipe, false);
        cooker.StartCookCountDown(1f, false);
      }
      if (mode == "take") {
        var system = NightScene.CookingUtility.CookSystemManager.Instance;
        foreach (var pair in system.AllCookers) if (pair.Value.GridIndex == @@INDEX@@) { system.CallCooker(pair.Key); break; }
      }
      var els = GameData.RunTime.NightSceneUtility.IzakayaTray.Instance.Tray.Elements;
      int tray = 0; for (int i = 0; i < els.Length; i++) if (els[i] != null) tray++;
      return "phase=" + cooker.Phase + " result=" + (cooker.Result == null ? "-" : cooker.Result.id.ToString())
        + " canExtract=" + MetaMystia.VersusMatch.CanExtract(@@INDEX@@) + " tray=" + tray;
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
