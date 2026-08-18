using Il2CppSystem.IO;
using System.Linq;

using GameData.Core.Collections;
using NightScene.CookingUtility;

using DEYU.Utils;

namespace MetaMystia;

[AutoLog]
public static partial class CookManager
{
    public static CookController GetCookerControllerByIndex(int gridIndex)
    {
        if (CookSystemManager.Instance == null)
        {
            return null;
        }
        var AllCookerControllers = CookSystemManager.Instance.AllCookerControllers;
        if (AllCookerControllers == null)
        {
            return null;
        }
        var query = from cooker in AllCookerControllers.ToArray()
                    where cooker.GridIndex == gridIndex
                    select cooker;
        return query.FirstOrDefault();
    }

    public static Cooker GetCookerByCookerId(int cookerId)
    {
        return CookSystemManager.Instance?.AllCookerControllers?.ToArray().FirstOrDefault((cooker) => cooker.Cooker.Id == cookerId)?.Cooker;
    }
}
