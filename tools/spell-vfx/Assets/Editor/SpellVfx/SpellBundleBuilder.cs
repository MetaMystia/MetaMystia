// 构建特效集合的 prefab 与 AssetBundle。
// 菜单：SpellVfx/Build All；批处理：
//   Unity.exe -batchmode -quit -nographics -projectPath <proj> -executeMethod SpellBundleBuilder.BuildFromBatch [-spell <Name>]

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class SpellBundleBuilder
{
    private const string OutputDir = "Build";

    [MenuItem("SpellVfx/Build All")]
    public static void BuildAllFromMenu() => BuildAll(null);

    public static void BuildFromBatch() => BuildAll(CommandLineValue("-spell"));

    /// <summary>工程中所有 ISpellVfxSet 实现；only 非空时只返回同名集合。</summary>
    public static IEnumerable<ISpellVfxSet> Sets(string only) =>
        TypeCache.GetTypesDerivedFrom<ISpellVfxSet>()
            .Where(t => !t.IsAbstract)
            .Select(t => (ISpellVfxSet)Activator.CreateInstance(t))
            .Where(s => only == null || s.Name == only);

    public static string CommandLineValue(string key)
    {
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static void BuildAll(string only)
    {
        var sets = Sets(only).ToList();
        if (sets.Count == 0)
            throw new ArgumentException($"no ISpellVfxSet found{(only == null ? "" : $" named {only}")}");
        foreach (var set in sets)
            Build(set);
    }

    private static void Build(ISpellVfxSet set)
    {
        var assets = new SpellAssetFactory(set.Name);
        assets.PrepareFolders();
        assets.ConfigureTextureImports();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var paths = new List<string>();
        foreach (var root in set.BuildPrefabs(assets))
        {
            var path = $"{assets.PrefabDir}/{root.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            paths.Add(path);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Directory.CreateDirectory(OutputDir);
        var build = new AssetBundleBuild
        {
            assetBundleName = set.BundleName,
            assetNames = paths.ToArray(),
            addressableNames = paths.ToArray(),
        };
        BuildPipeline.BuildAssetBundles(OutputDir, new[] { build },
            BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
        Debug.Log($"[SpellVfx] bundle written: {Path.Combine(OutputDir, set.BundleName)} ({paths.Count} prefabs)");
    }
}
