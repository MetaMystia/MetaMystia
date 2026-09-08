using NightScene.GuestManagementUtility;

public static class Payload
{
    public static object Execute()
    {
        GuestsManager.Instance.ExcuteEventAtCorodinate(1);
        return "ExcuteEventAtCorodinate desk=1 submitted";
    }
}
