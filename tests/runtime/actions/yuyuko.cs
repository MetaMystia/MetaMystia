// 最终试炼一览：主循环位置、本体生命、吞掉的厨具、本体登记与当前订单、备菜轮次、客机已收到的阶段消息。
// MODE=data 另列挑战参数（单阶段时长、一二阶段目标、本体总生命、菜酒等级扣血表、分身伤害与刷新间隔）。
// MODE=pass 只在房主执行的测试捷径：一阶段营业额、二阶段正面符卡数设为目标值，三阶段本体生命清零（经原版显示刷新广播）。重修版本体和分身要 Good 以上才扣血，自动上菜难以达成。各端仍按房主的阶段消息判定。
public static class Program {
  const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
  public static object Execute() {
    var sb = new System.Text.StringBuilder("mode=" + NightScene.NightSceneDirector.ChallengeMode
      + " scene=" + MetaMystia.GameFlow.LocalScene
      + " prepRound=" + MetaMystia.PrepSceneManager.YuyukoPrepRound + (MetaMystia.PrepSceneManager.IsYuyukoPrepActive ? "(open)" : ""));
    var loop = (GameData.Profile.YuyukoBossData._MainChallengeLoop_d__16)typeof(MetaMystia.Patch.YuyukoBossDataPatch).GetField("currentLoop", Any).GetValue(null);
    if (loop == null) return sb.Append(" loop=none").ToString();
    var ctx = loop.__8__1;
    var d = loop.__4__this;
    if ("@@MODE@@" == "pass") {
      if (!MetaMystia.Multiplayer.GameSession.IsRoomHost) return "host only";
      int round = MetaMystia.PrepSceneManager.YuyukoPrepRound;
      if (round == 1) ctx.eventManager.EarnedFund = d.phase1TargetPoints;
      else if (round == 2) ctx.positiveSpellCount = d.phase2TargetPositiveSpells;
      else { ctx.yuyukoTotalLife = 0; if (ctx.statusDisplayer != null) ctx.statusDisplayer.SetTargetProgress(0); }
      sb.Append(" passed");
    }
    sb.Append(" state=" + loop.__1__state + " life=" + ctx.yuyukoTotalLife + " earned=" + ctx.eventManager.EarnedFund + " spells=" + ctx.positiveSpellCount);
    var locked = ctx.eventManager.LockedCookers;
    sb.Append(" locked=[");
    if (locked != null) for (int i = 0; i < locked.Length; i++) sb.Append((i > 0 ? "," : "") + locked[i]);
    sb.Append("]");
    var guest = (MetaMystia.Multiplayer.Guests.GuestRecord)typeof(MetaMystia.YuyukoGuestSync).GetField("guest", Any).GetValue(null);
    sb.Append(guest == null ? " body=unbound" : " body=#" + guest.RuntimeId + ":" + guest.State + "/" + guest.OrderSeq);
    var body = ctx.yuyuko;
    if (body != null) {
      sb.Append("@" + body.DeskCode);
      if (body.AllOrdersCount > 0) {
        var order = body.PeekOrders();
        var special = order.TryCast<NightScene.GuestManagementUtility.GuestsManager.SpecialOrder>();
        sb.Append(special == null ? " order=food" + order.foodRequest + "+bev" + order.beverageRequest
          : " order=tag" + special.RequestFoodTag + "+tag" + special.RequestBeverageTag);
      } else sb.Append(" order=none");
    }
    var phases = (System.Collections.IDictionary)typeof(MetaMystia.YuyukoGuestSync).GetField("phases", Any).GetValue(null);
    sb.Append(" phases=[");
    bool first = true;
    foreach (var k in phases.Keys) { sb.Append((first ? "" : ",") + k); first = false; }
    sb.Append("]");
    if ("@@MODE@@" == "data") {
      sb.Append(" | round=" + d.singleRoundDuration + " p1=" + d.phase1TargetPoints + " p2=" + d.phase2TargetPositiveSpells + " totalLife=" + d.phase3YuyukoTotalLife
        + " standEx=" + d.phase3DamageWhenStandBeCompletelyDefeated + " standGood=" + d.phase3DamageWhenStandBeNormalDefeated
        + " standInterval=" + d.phase3YuyukoStandSpawnInterval + " hp=");
      foreach (var v in d.phase3FoodLevelToYuyukoHpData) sb.Append(v.x + ":" + v.y + " ");
    }
    return sb.ToString().TrimEnd();
  }
}
