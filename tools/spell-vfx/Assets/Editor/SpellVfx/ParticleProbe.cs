// 批处理检查：模拟每个 prefab，输出粒子在屏幕平面（XY）与深度（Z）上的分布与速度。
// Z 方向速度明显大于 XY 时，说明发射方向朝向相机，画面上会看成静止或一条线。
//   Unity.exe -batchmode -quit -nographics -projectPath <proj> -executeMethod ParticleProbe.Run [-spell <Name>] [-probeTime 0.8]

using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ParticleProbe
{
    public static void Run()
    {
        var time = float.TryParse(SpellBundleBuilder.CommandLineValue("-probeTime"), out var t) ? t : 0.8f;
        foreach (var set in SpellBundleBuilder.Sets(SpellBundleBuilder.CommandLineValue("-spell")))
        {
            var dir = new SpellAssetFactory(set.Name).PrefabDir;
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { dir }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
                {
                    ps.Simulate(time, false, true);
                    var buf = new ParticleSystem.Particle[ps.particleCount];
                    var n = ps.GetParticles(buf);
                    if (n == 0)
                    {
                        Debug.Log($"PROBE {set.Name}/{go.name}/{ps.name} n=0");
                        continue;
                    }
                    var p = buf.Take(n).Select(x => x.position).ToArray();
                    var v = buf.Take(n).Select(x => x.velocity).ToArray();
                    float Range(System.Func<Vector3, float> f) => p.Max(f) - p.Min(f);
                    Debug.Log($"PROBE {set.Name}/{go.name}/{ps.name} n={n} spread x={Range(q => q.x):F2} y={Range(q => q.y):F2} z={Range(q => q.z):F2}"
                        + $" | |vxy|={v.Average(q => new Vector2(q.x, q.y).magnitude):F2} |vz|={v.Average(q => Mathf.Abs(q.z)):F2}");
                }
                Object.DestroyImmediate(go);
            }
        }
    }
}
