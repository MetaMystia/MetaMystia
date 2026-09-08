using System.Collections.Generic;

using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

public static class Payload
{
    public static object Execute()
    {
        var rows = new List<string>();
        rows.Add("countdown=" + EventManager.Instance.TotalCountDown);
        for (int code = 0; code < GuestsManager.Instance.MaxDeskNum; code++)
        {
            var g = GuestsManager.Instance.GetInDeskGuest(code);
            if (g == null) continue;
            rows.Add("desk=" + code + " guest=" + g.OnGetGuestName());
            if (g is SpecialGuestsController sp)
                rows.Add("special id=" + sp.SpecialGuest.Id);
            if (g.AllOrdersCount > 0)
                rows.Add("order=" + g.PeekOrders().ToString());
        }
        return string.Join("\n", rows);
    }
}
