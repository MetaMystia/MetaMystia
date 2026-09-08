using GameData.RunTime.Common;
using GameData.RunTime.DaySceneUtility;

public static class Payload
{
    public static object Execute()
    {
        var status = StatusTracker.Instance;
        return "map=" + DayScene.SceneManager.Instance.CurrentActiveMapLabel
            + " AP=" + RunTimeDayScene.RemainActions
            + " clock=" + DayScene.UI.UIManager.Instance.GetTimeCode(RunTimeDayScene.RemainActions)
            + " fund=" + RunTimePlayerData.GetFund()
            + " invitedReimu7=" + status.InvitedGuests.Contains(7);
    }
}
