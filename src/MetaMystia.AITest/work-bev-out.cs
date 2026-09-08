using GameData.Core.Collections;
using GameData.RunTime.Common;
using GameData.RunTime.NightSceneUtility;

public static class Payload
{
    public static object Execute()
    {
        const int bevId = 22;
        if (RunTimeStorage.GetBeverageCountById(bevId) <= 0) return "No beverage id " + bevId;
        var tray = IzakayaTray.Instance;
        if (tray.IsTrayFull) return "Tray full";
        var sell = bevId.AsNewBeverage();
        RunTimeStorage.BeverageOut(bevId, false);
        tray.Receive(sell);
        return "Put beverage id=" + bevId + " " + sell.Text.Name + " on tray; trayFull=" + tray.IsTrayFull;
    }
}
