using System.Linq;

using GameData.Core.Collections;
using GameData.Core.Collections.CharacterUtility;

using SgrYuki.Utils;

namespace MetaMystia;

// 行为半边（mod）：依赖游戏 DataBaseCore / DataBaseCharacter 的装载与日志。
// 数据半边（字段 + 序列化 + 增量逻辑）见 Network/Protocol/Dtos/ResourceDataBaseData.cs。

public static class ResourceDataBase
{
    public static ResourceDataBaseData LoadResourceIds(this ResourceDataBaseData resourceDataBase)
    {
        resourceDataBase.Clear();

        resourceDataBase.Foods.AddRange(DataBaseCore.Foods.ToList().Select(f => f.Key));
        resourceDataBase.Recipes.AddRange(DataBaseCore.Recipes.ToList().Select(r => r.Key));
        resourceDataBase.Beverages.AddRange(DataBaseCore.Beverages.ToList().Select(b => b.Key));
        resourceDataBase.Ingredients.AddRange(DataBaseCore.Ingredients.ToList().Select(i => i.Key));
        resourceDataBase.Cookers.AddRange(DataBaseCore.Cookers.ToList().Select(c => c.Key));
        resourceDataBase.Items.AddRange(DataBaseCore.Items.ToList().Select(i => i.Key));
        resourceDataBase.Izakayas.AddRange(DataBaseCore.Izakayas.ToList().Select(i => i.Key));

        resourceDataBase.SpecialGuests.AddRange(DataBaseCharacter.SpecialGuest.ToList().Select(s => s.Key));
        resourceDataBase.NormalGuests.AddRange(DataBaseCharacter.NormalGuest.ToList().Select(n => n.Key));

        return resourceDataBase;
    }

    public static void LogDataBase(this ResourceDataBaseData resourceDataBase)
    {
        Plugin.Instance?.Log.LogWarning($"Foods: {string.Join(", ", resourceDataBase.Foods)}");
        Plugin.Instance?.Log.LogWarning($"Recipes: {string.Join(", ", resourceDataBase.Recipes)}");
        Plugin.Instance?.Log.LogWarning($"Beverages: {string.Join(", ", resourceDataBase.Beverages)}");
        Plugin.Instance?.Log.LogWarning($"Ingredients: {string.Join(", ", resourceDataBase.Ingredients)}");
        Plugin.Instance?.Log.LogWarning($"Cookers: {string.Join(", ", resourceDataBase.Cookers)}");
        Plugin.Instance?.Log.LogWarning($"Items: {string.Join(", ", resourceDataBase.Items)}");
        Plugin.Instance?.Log.LogWarning($"Izakayas: {string.Join(", ", resourceDataBase.Izakayas)}");

        Plugin.Instance?.Log.LogWarning($"SpecialGuests: {string.Join(", ", resourceDataBase.SpecialGuests)}");
        Plugin.Instance?.Log.LogWarning($"NormalGuests: {string.Join(", ", resourceDataBase.NormalGuests)}");
    }
}
