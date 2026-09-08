using System.Collections.Generic;

using GameData.CoreLanguage.Collections;
using NightScene.GuestManagementUtility;

public static class Payload
{
    public static object Execute()
    {
        var rows = new List<string>();
        var gm = GuestsManager.Instance;
        rows.Add("maxDesk=" + gm.MaxDeskNum + " countdown=" + NightScene.EventUtility.EventManager.Instance.TotalCountDown);
        for (int code = 0; code < gm.MaxDeskNum; code++)
        {
            var g = gm.GetInDeskGuest(code);
            if (g == null) continue;
            rows.Add("desk=" + code + " type=" + g.GetType().FullName + " name=" + g.OnGetGuestName()
                + " orderCount=" + g.AllOrdersCount);
            var sp = g as SpecialGuestsController;
            if (sp != null) rows.Add("  isSpecial=1 id=" + sp.SpecialGuest.Id);
            if (g.AllOrdersCount <= 0) continue;
            var order = g.PeekOrders();
            rows.Add("  orderType=" + order.Type + " isSpecialOrder=" + (order.Type == GuestsManager.OrderBase.OrderType.Special)
                + " foodNull=" + (order.ServFood == null) + " bevNull=" + (order.ServBeverage == null));
            rows.Add("  baseFieldFood=" + order.foodRequest + " baseFieldBev=" + order.beverageRequest);
            var so = order as GuestsManager.SpecialOrder;
            rows.Add("  castSpecial=" + (so != null));
            if (so != null)
                rows.Add("  foodTag=" + so.RequestFoodTag + " " + so.RequestFoodTag.GetFoodTag()
                    + " bevTag=" + so.RequestBeverageTag + " " + so.RequestBeverageTag.GetBeverageTag());
        }
        return string.Join("\n", rows);
    }
}
