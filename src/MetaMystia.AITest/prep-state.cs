using System.Collections.Generic;
using UnityEngine;
using GameData.Core.Collections;
using GameData.RunTime.Common;
using PrepNightScene.UI;

public static class Payload
{
    public static object Execute()
    {
        var panel = Object.FindObjectOfType<IzakayaConfigPannel>();
        var config = panel.m_IzakayaConfigure;
        var rows = new List<string>();
        foreach (var r in config.DailyRecipes) rows.Add($"MENU recipe={r.Id} {r.Food.Text.Name} cooker={r.CookerType}");
        foreach (var b in config.DailyBeverages) rows.Add($"MENU beverage={b.Id} {b.Text.Name}");
        for (int i = 0; i < config.CookerConfigure.Length; i++)
        {
            int id = config.CookerConfigure[i];
            rows.Add($"SLOT {i} id={id} {(id < 0 ? "empty" : DataBaseCore.RefCooker(id).Text.Name)}");
        }
        foreach (var pair in RunTimeStorage.GetAllCookers())
            rows.Add($"OWN cooker={pair.Key.Id} {pair.Key.Text.Name} count={pair.Value}");
        foreach (var r in RunTimeStorage.GetAllRecipes())
            if (r.Food.Text.Name == "大江户船祭" || r.Food.Text.Name == "白雪" || r.Food.Text.Name == "山泉双色果盘") rows.Add($"OWN recipe={r.Id} {r.Food.Text.Name} cooker={r.CookerType} count={r.CookCount}");
        foreach (var pair in RunTimeStorage.GetAllBeverages())
            if (pair.Key.Text.Name == "十四夜" || pair.Key.Text.Name == "雀酒") rows.Add($"OWN beverage={pair.Key.Id} {pair.Key.Text.Name} count={pair.Value}");
        return string.Join("\n", rows);
    }
}
