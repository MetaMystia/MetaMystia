using NightScene.CookingUtility;

public static class Payload
{
    public static object Execute()
    {
        var m = CookSystemManager.Instance;
        if (m == null) return "no cook manager";
        var d = m.AllCookers;
        return "manager=ok allCookersCount=" + d.Count + " controllersType=" + m.AllCookerControllers.GetType().FullName;
    }
}
