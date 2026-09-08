using System.Collections.Generic;

using GameData.RunTime.Common;
using GameData.RunTime.DaySceneUtility;

public static class Payload
{
    public static object Execute()
    {
        var rows = new List<string>();
        rows.Add("map=" + DayScene.SceneManager.Instance.CurrentActiveMapLabel
            + " AP=" + RunTimeDayScene.RemainActions
            + " clock=" + DayScene.UI.UIManager.Instance.GetTimeCode(RunTimeDayScene.RemainActions)
            + " fund=" + RunTimePlayerData.GetFund());
        var list = new List<int>();
        foreach (int id in StatusTracker.Instance.InvitedGuests) list.Add(id);
        rows.Add("invitedCount=" + list.Count + " ids=" + string.Join(",", list));
        return string.Join("\n", rows);
    }
}
