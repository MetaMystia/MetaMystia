// 本端接待统计：普客、稀客接待人数，来客组数，以及当日稀客池与登记表中各位稀客的图鉴是否已记录本店（id:1 为已记录）。
// 两端对照生成前后的增量。
public static class Program {
  public static object Execute() {
    if (MetaMystia.GameFlow.LocalScene != Common.UI.Scene.WorkScene) return "not in night scene: " + MetaMystia.GameFlow.LocalScene;
    var st = GameData.RunTime.Common.StatusTracker.Instance;
    var izakaya = GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance.IzakayaData;
    var album = GameData.RunTime.Common.RunTimeAlbum.RecordedSpecialNPCs;
    var ids = new System.Collections.Generic.SortedSet<int>();
    var pool = GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance.SpecialGuestPoolIdentityData;
    if (pool != null) foreach (var id in pool) ids.Add(id);
    foreach (var g in MetaMystia.Multiplayer.Guests.GuestRegistry.Current.All)
      if (g.SpawnInfo.GuestType == NightScene.GuestManagementUtility.GuestsManager.GuestType.Special) ids.Add(g.SpawnInfo.Ids[0]);
    var sb = new System.Text.StringBuilder("normal=" + st.NormalGuestReception + " special=" + st.SpecialGuestReception
      + " sets=" + NightScene.EventUtility.EventManager.Instance.ServedGuestSetNum + " album=");
    foreach (var id in ids) {
      bool revealed = izakaya != null && album.ContainsKey(id) && album[id].RevealedIzakaya.Contains(izakaya.Id);
      sb.Append(id + ":" + (revealed ? 1 : 0) + " ");
    }
    return sb.ToString().TrimEnd();
  }
}
