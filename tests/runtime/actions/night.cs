// 夜间营业一行状态（调试器的字符串结果超过 300 字会截断，保持简短）：
// wait=开店等待 frozen=房间冻结 ts=timeScale cd=营业倒计时 close=打烊中 occ=占用桌数 panel=打开的上菜面板桌号
// desks=桌号:客人编号/FSM 状态；单机没有 GuestFSM，显示 桌号:o订单数
public static class Program {
  public static object Execute() {
    var sb = new System.Text.StringBuilder();
    sb.Append("wait=" + MetaMystia.BusinessStart.IsWaitingForStart);
    sb.Append(" frozen=" + MetaMystia.Patch.GameTimeManagerPatch.IsFrozen);
    sb.Append(" ts=" + UnityEngine.Time.timeScale);
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.WorkScene) return sb.Append(" scene=" + MetaMystia.GameFlow.LocalScene).ToString();
    sb.Append(" cd=" + NightScene.EventUtility.EventManager.Instance.TotalCountDown.ToString("F0"));
    var gm = NightScene.GuestManagementUtility.GuestsManager.Instance;
    sb.Append(" close=" + gm.isIzakayaClosing + " occ=" + gm.occupiedDesks.Count + " panel=" + MetaMystia.Patch.WorkSceneServePannelPatch.PanelDeskCode + " desks=");
    for (int d = 0; d < 16; d++) {
      NightScene.GuestManagementUtility.GuestGroupController c = null;
      try { c = gm.GetInDeskGuest(d); } catch { }
      if (c == null) continue;
      var fsm = MetaMystia.Multiplayer.GameSession.HasRoomPeers ? MetaMystia.GuestsMap.GetGuestFsm(c) : null;
      // 单机查 GuestFSM 会让模组记 Error 日志
      sb.Append(d + ":" + (fsm == null ? "o" + c.AllOrdersCount : fsm.RuntimeId + "/" + fsm.CurrentState) + " ");
    }
    return sb.ToString().TrimEnd();
  }
}
