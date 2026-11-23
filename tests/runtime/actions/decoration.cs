// ResourceEx 装饰。IDS 为逗号分隔的装饰物品 ID，BUFF 为要报告剩余秒数的 buff ID。
// MODE=info    白天：每件的 注册/拥有/装备、玩家移速 spd 与等级移速 lv；夜间：玩家额外移速 ms、伙伴 效率/移速、返还食材概率 free、烹饪时间倍率 cook、BUFF 剩余秒数
// MODE=give    原版 RunTimeStorage.ItemInRange 获得 IDS（同拾取、购买的入库）
// MODE=equip   原版装饰面板提交的同一调用 RunTimeAlbum.TryRecordUsedDecoration；MODE=unequip 对应 TryRemoveUsedDecoration
// MODE=dark    夜间：以黑暗料理调用 CookSystemManager.CallOnResultCompleteCallback（原版烹饪完成的汇合点，跳过烹饪过程）；MODE=food 以当日第一道料理调用
public static class Program {
  public static object Execute() {
    string mode = "@@MODE@@";
    int buff = @@BUFF@@;
    var ids = new System.Collections.Generic.List<int>();
    foreach (var s in "@@IDS@@".Split(',')) ids.Add(int.Parse(s));
    var local = MetaMystia.GameFlow.LocalScene;
    switch (mode) {
      case "give": {
        var list = new Il2CppSystem.Collections.Generic.List<int>();
        foreach (var id in ids) list.Add(id);
        GameData.RunTime.Common.RunTimeStorage.ItemInRange(list.Cast<Il2CppSystem.Collections.Generic.IEnumerable<int>>(), false);
        break;
      }
      case "equip": foreach (var id in ids) GameData.RunTime.Common.RunTimeAlbum.TryRecordUsedDecoration(id); break;
      case "unequip": foreach (var id in ids) GameData.RunTime.Common.RunTimeAlbum.TryRemoveUsedDecoration(id); break;
      case "dark":
      case "food": {
        if (local != Common.UI.Scene.WorkScene) return "not in night scene: " + local;
        int foodId = mode == "dark" ? GameData.RunTime.Common.RunTimeStorage.DARK_MATTER_ID
          : GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance.DailyRecipes[0].foodID;
        NightScene.CookingUtility.CookSystemManager.Instance.CallOnResultCompleteCallback(GameData.Core.Collections.DataBaseCore.AsNewFood(foodId));
        break;
      }
      case "info": break;
      default: return "unknown mode " + mode;
    }
    var sb = new System.Text.StringBuilder(mode + " " + local);
    foreach (var id in ids) {
      var db = GameData.Core.Collections.DataBaseCore.Decorations;
      string impl = db.ContainsKey(id) ? db[id].SpecialBuff.GetIl2CppType().Name.Replace("Decoration", "") + "/" + db[id].DecorationSettingType : "-";
      sb.Append(" " + id + ":" + impl
        + " own=" + (GameData.RunTime.Common.RunTimeStorage.ContainsItem(id) ? 1 : 0)
        + " use=" + (GameData.RunTime.Common.RunTimeAlbum.HasDecorationUsing(id) ? 1 : 0));
    }
    if (local == Common.UI.Scene.DayScene) {
      var player = DayScene.SceneManager.Instance.Character.Character;
      sb.Append(" spd=" + player.MoveSpeedMultiplier.ToString("0.###")
        + " lv=" + GameData.RunTime.Common.RunTimePlayerData.LevelProfile.MoveSpeedMultiplier.ToString("0.###"));
    } else if (local == Common.UI.Scene.WorkScene) {
      var em = NightScene.EventUtility.EventManager.Instance;
      float ms = 1f; foreach (var f in em.registeredExtraMoveSpeedModifier) ms *= f;
      var unit = NightScene.SceneManager.Instance.PlayerCharacter.GetComponent<Common.CharacterUtility.CharacterControllerUnit>();
      float cook = 1f; foreach (var f in em.extraCookTimeModifier) cook *= f.Invoke();
      sb.Append(" ms=" + ms.ToString("0.###") + " unit=" + unit.extraMoveSpeed.ToString("0.###"));
      if (NightScene.PartnerUtility.PartnerManager.hasInstance)
        foreach (var p in Il2CppSystem.Linq.Enumerable.ToArray(NightScene.PartnerUtility.PartnerManager.Instance.GetAllPartners())) {
          int pid = p.PartnerData.PartnerId;
          sb.Append(" p" + pid + "=" + em.GetExtraPartnerWorkSpeed(pid, p.PartnerIndex).ToString("0.###") + "/" + em.GetExtraPartnerMoveSpeed(pid, p.PartnerIndex).ToString("0.###"));
        }
      sb.Append(" free=" + em.FreeCookRate.ToString("0.###") + " cook=" + cook.ToString("0.###"));
      Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int> left;
      sb.Append(" buff=" + (em.TryGetBuffTime((NightScene.EventUtility.EventManager.BuffType)buff, out left) ? string.Join("|", (int[])left) : "none"));
    }
    return sb.ToString();
  }
}
