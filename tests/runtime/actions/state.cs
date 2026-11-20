// 一行状态，供脚本和验证清单判断进度。
public static class Program {
  public static object Execute() {
    return "scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
      + " local=" + MetaMystia.GameFlow.LocalScene
      + " title=" + MainScene.SceneManager.hasInstance
      + " ready=" + MetaMystia.GameFlow.CharactersReady
      + " story=" + MetaMystia.GameFlow.InStory
      + " stage=" + typeof(MetaMystia.GameFlow).GetProperty("Stage").GetValue(null)
      + " dest=" + MetaMystia.GameFlow.Destination
      + " room=" + MetaMystia.Multiplayer.GameSession.IsInRoom
      + " host=" + MetaMystia.Multiplayer.GameSession.IsRoomHost
      + " peers=" + MetaMystia.Multiplayer.GameSession.HasRoomPeers
      + " timeScale=" + UnityEngine.Time.timeScale;
  }
}
