using HarmonyLib;
using System.Linq;

using GameData.Core.Collections.DaySceneUtility.Collections;
using GameData.RunTime.Common;
using GameData.RunTime.DaySceneUtility;

using static GameData.Core.Collections.DaySceneUtility.Collections.Product;

using SgrYuki.Utils;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.RunTime.DaySceneUtility.RunTimeDayScene))]
[AutoLog]
public partial class RunTimeDayScenePatch
{

    [HarmonyPatch(nameof(RunTimeDayScene.GetMerchantData))]
    [HarmonyPostfix]
    public static void GetMerchantData_Postfix(ref GameData.RunTime.DaySceneUtility.Collection.TrackedMerchant __result, string characterKey)
    {
        Log.Info($"DataBaseDay.GetMerchantData Postfix called with key: {characterKey}");
        if (!ResourceExManager.TryGetExMerchantData(characterKey, out Merchant merchant))
        {
            return;
        }

        // 游戏原有的 「香霖堂」「爱莲」以及「萌橙果」「蹦蹦跳跳的三妖精」商人有自己的脚本，会特殊处理商品，这里需要简单处理一下商品
        // 对 Ex 商人的已有「食谱」进行过滤
        __result.products = __result.products
            .Where(m => m.productType != ProductType.Recipe
                   || !RunTimeStorage.Recipes.Contains(m.productId))
            .ToIl2CppReferenceArray();
    }
}
