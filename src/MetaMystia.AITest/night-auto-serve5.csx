using System.Collections;
using System.Collections.Generic;

using BepInEx.Unity.IL2CPP.Utils;
using GameData.Core.Collections;
using GameData.RunTime.Common;
using GameData.RunTime.NightSceneUtility;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;
using NightScene.UI.GuestManagementUtility;
using UnityEngine;

public static class AITestAutoServeV5
{
    static readonly int[] FoodRecipes = { 35, 7, 68, 10, 34, 61, 2016, 11000 };
    static readonly int[] Beverages = { 22, 19, 13, 1001, 20, 17, 14, 27 };

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
        gm.StartCoroutine(Run(gm, Time.realtimeSinceStartup + 480f));
        return Info();
    }

    static int FindFoodRecipe(int tagId)
    {
        foreach (int recipeId in FoodRecipes)
        {
            var food = recipeId.RefRecipe().Food;
            foreach (int t in food.Tags)
                if (t == tagId) return recipeId;
        }
        return 0;
    }

    static int FindBeverage(int tagId)
    {
        foreach (int bevId in Beverages)
        {
            var bev = DataBaseCore.RefBeverage(bevId);
            foreach (int t in bev.Tags)
                if (t == tagId) return bevId;
        }
        return 0;
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
                if (!(g is SpecialGuestsController sp)) continue;
                var order = g.PeekOrders();
                if (order == null || order.Type != GuestsManager.OrderBase.OrderType.Special
                    || order.ServFood != null || order.ServBeverage != null) continue;
                int guestId = sp.SpecialGuest.Id;
                int foodTagId = order.foodRequest;
                int bevTagId = order.beverageRequest;
                int foodRecipe = FindFoodRecipe(foodTagId);
                int bevId = FindBeverage(bevTagId);
                if (foodRecipe == 0 || bevId == 0)
                {
                    Status = "unsupported guest=" + guestId + " desk=" + code + " foodTagId=" + foodTagId + " bevTagId=" + bevTagId;
                    continue;
                }
                Status = "serving guest=" + guestId + " desk=" + code + " recipe=" + foodRecipe + " bev=" + bevId;
                if (!PutFoodToTray(foodRecipe) || !PutBevToTray(bevId))
                {
                    Status = "prep failed guest=" + guestId + " desk=" + code;
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
                    Status = "panel missing guest=" + guestId + " desk=" + code;
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
                    Status = "tray incomplete guest=" + guestId + " desk=" + code;
                    Running = false;
                    yield break;
                }
                panel.Send(food);
                panel.Send(bev);
                panel.ClosePanel();
                Status = "done guest=" + guestId + " desk=" + code + " food=" + food.Id + " bev=" + bev.Id;
            }
            yield return new WaitForSecondsRealtime(0.2f);
        }
        if (Status == "watching" || Status.StartsWith("serving")) Status = "timeout";
        Running = false;
    }
}

AITestAutoServeV5.Info()
