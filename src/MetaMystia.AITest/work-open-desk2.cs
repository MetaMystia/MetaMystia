using NightScene.GuestManagementUtility;

public static class Payload
{
    public static object Execute()
    {
        GuestsManager.Instance.ExcuteEventAtCorodinate(2);
        return "ExcuteEventAtCorodinate desk=2 submitted";
    }
}
