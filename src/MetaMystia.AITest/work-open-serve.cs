using NightScene.GuestManagementUtility;

public static class Payload
{
    public static object Execute()
    {
        var gm = GuestsManager.Instance;
        for (int code = 0; code < gm.MaxDeskNum; code++)
        {
            var g = gm.GetInDeskGuest(code);
            if (g == null || !(g is SpecialGuestsController sp) || sp.SpecialGuest.Id != 7) continue;
            gm.ExcuteEventAtCorodinate(code);
            return "Open serve panel for desk " + code + " (Reimu)";
        }
        return "Reimu not at any desk";
    }
}
