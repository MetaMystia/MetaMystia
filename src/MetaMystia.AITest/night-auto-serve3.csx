using System.Collections;
using System.Collections.Generic;

using BepInEx.Unity.IL2CPP.Utils;
using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;
using GameData.RunTime.Common;
using GameData.RunTime.NightSceneUtility;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;
using NightScene.UI.GuestManagementUtility;
using UnityEngine;

public static class AITestAutoServeV3
{
    public static string Status = "idle";
    public static int Ticks;
    public static float LastTickTime;
    public static bool Running;

    public static string Info()
    {
        return "running=" + Running + " ticks=" + Ticks + " status=" + Status
            + " lastTickAge=" + (Running ? (Time.realtimeSinceStartup - LastTickTime).ToString("0.0") : "-")
            + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    }

    public static string Reset()
    {
        Running = false;
        Ticks = 0;
        Status = "idle";
        return Info();
    }

    public static string Start()
    {
        var gm = GuestsManager.Instance;
        if (gm == null) return "no guests manager";
        if (Running) return "already running | " + Info();
        Running = true;
        Ticks = 0;
        Status = "watching";
        LastTickTime = Time.realtimeSinceStartup;
        gm.StartCoroutine(Run(gm, Time.realtimeSinceStartup + 240f));
        return Info();
    }

    static int FoodRecipe(string tag)
    {
        if (tag == "力量涌现") return 2016;
        if (tag == "肉") return 55;
        if (tag == "中华") return 61;
        if (tag == "饱腹") return 35;
        return 35;
    }

    static int BeverageId(string tag)
    {
        if (tag == "提神") return 1001;
        if (tag == "古典") return 19;
        if (tag == "可加热") return 22;
        return 22;
    }

    static bool PutFoodToTray(int recipeId)
    {
        var recipe = recipeId.RefRecipe();
        foreach (int id in recipe.Ingredients) RunTimeStorage.IngredientOut(id, false);
        var tray = IzakayaTray.Instance;
        if (tray.IsTrayFull) return false;
        tray.Receive(DataBaseCore.AsNewFood(recipe.FoodID));
        return true;
    }

    static bool PutBevToTray(int bevId)
    {
        if (RunTimeStorage.GetBeverageCountById(bevId) <= 0) return false;
        var tray = IzakayaTray.Instance;
        if (tray.IsTrayFull) return false;
        RunTimeStorage.BeverageOut(bevId, false);
        tray.Receive(bevId.AsNewBeverage());
        return true;
    }

    static IEnumerator Run(GuestsManager gm, float deadline)
    {
        while (Time.realtimeSinceStartup < deadline)
        {
            LastTickTime = Time.realtimeSinceStartup;
            Ticks++;
            for (int code = 0; code < gm.MaxDeskNum; code++)
            {
                var g = gm.GetInDeskGuest(code);
                if (g == null || g.AllOrdersCount <= 0) continue;
                if (!(g is SpecialGuestsController sp) || sp.SpecialGuest.Id != 15) continue;
                var order = g.PeekOrders();
                if (order == null || order.Type != GuestsManager.OrderBase.OrderType.Special
                    || order.ServFood != null || order.ServBeverage != null) continue;
                string foodTag = order.foodRequest.GetFoodTag();
                string bevTag = order.beverageRequest.GetBeverageTag();
                Status = "serving desk=" + code + " foodTag=" + foodTag + " bevTag=" + bevTag;
                int foodRecipe = FoodRecipe(foodTag);
                int bevId = BeverageId(bevTag);
                if (!PutFoodToTray(foodRecipe) || !PutBevToTray(bevId))
                {
                    Status = "prep failed desk=" + code;
                    Running = false;
                    yield break;
                }
                gm.ExcuteEventAtCorodinate(code);
                var panelDeadline = Time.realtimeSinceStartup + 3f;
                WorkSceneServePannel panel = null;
                while (Time.realtimeSinceStartup < panelDeadline)
                {
                    foreach (var p in Object.FindObjectsOfType<WorkSceneServePannel>(true))
                        if (p.isActiveAndEnabled) { panel = p; break; }
                    if (panel != null) break;
                    yield return null;
                }
                if (panel == null)
                {
                    Status = "panel missing desk=" + code;
                    Running = false;
                    yield break;
                }
                var tray = IzakayaTray.Instance;
                Sellable food = null;
                Sellable bev = null;
                for (int i = 0; i < tray.TrayMaxNum; i++)
                {
                    var s = tray.Tray[i];
                    if (s == null) continue;
                    if (s.Type == Sellable.SellableType.Food && food == null) food = s;
                    if (s.Type == Sellable.SellableType.Beverage && bev == null) bev = s;
                }
                if (food == null || bev == null)
                {
                    Status = "tray incomplete desk=" + code;
                    Running = false;
                    yield break;
                }
                panel.Send(food);
                panel.Send(bev);
                panel.ClosePanel();
                Status = "done desk=" + code + " food=" + food.Id + " bev=" + bev.Id;
                Running = false;
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.2f);
        }
        if (Status == "watching") Status = "timeout";
        Running = false;
    }
}

AITestAutoServeV3.Info()
