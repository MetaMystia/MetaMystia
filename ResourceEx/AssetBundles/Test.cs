using UnityEngine;

namespace MetaMystia.ResourceEx.AssetBundles;

[AutoLog]
public static partial class Test
{
    public static GameObject TestObj;
    public static void Test1()
    {
        Log.Warning("Loading test bunble");

        var ab = AssetBundle.LoadFromFile("M:/_testbundle");
        var allAssets = ab.LoadAllAssetsAsync<GameObject>().allAssets;
        foreach (var ase in allAssets)
        {
            Log.Warning($"Loaded asset {ase.name}");
            if (ase.name == "_TestStar")
            {
                TestObj = ase.Cast<GameObject>();
            }
        }
    }
}
