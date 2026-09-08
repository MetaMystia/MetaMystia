using NightScene.GuestManagementUtility;

public static class Payload
{
    public static object Execute()
    {
        var gm = GuestsManager.Instance;
        for (int code = 0; code < gm.MaxDeskNum; code++)
        {
            var g = gm.GetInDeskGuest(code);
            if (g == null || !(g is SpecialGuestsController sp) || sp.SpecialGuest.Id != 15) continue;
            gm.ExcuteEventAtCorodinate(code);
            return "Open serve panel for desk " + code + " (Meirin)";
        }
        return "Meirin not at any desk";
    }
}
