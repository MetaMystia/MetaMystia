using Common.UI;

using MetaMystia.Network;
using MetaMystia.Protocol;

namespace MetaMystia;

// 本地游戏生命周期不随房间绑定清空。
public static class GameContext
{
    public static Scene Scene { get; private set; } = Scene.EmptyScene;
    public static long SceneEpoch { get; private set; }
    public static bool HasVisitedMain { get; private set; }
    public static bool InStory { get; private set; }
    public static bool EndingDay { get; set; }
    public static bool CanEnterRoom => !EndingDay && Scene is Scene.MainScene or Scene.DayScene;

    public static void RefreshStory()
    {
        var director = Common.SceneDirector.Instance?.playableDirector;
        InStory = director != null && director.state is UnityEngine.Playables.PlayState.Playing or UnityEngine.Playables.PlayState.Delayed;
    }
    public static void OnSceneChanged(Scene scene)
    {
        Scene = scene;
        SceneEpoch++;
        if (scene is Scene.DayScene or Scene.MainScene) EndingDay = false;
        if (scene == Scene.MainScene)
        {
            HasVisitedMain = true;
            RoomGameplay.Leave();
        }
        SceneTransitAction.Send(scene);
        RoomGameplay.OnLocalSceneChanged(scene);
    }
}
