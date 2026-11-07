// 为一个特效集合创建贴图导入设置、材质与目录。
//
// 材质必须保存为资产文件：prefab 按 GUID 引用材质，只存在于内存中的 Material
// 不会随 prefab 序列化，也不会进入 AssetBundle。

using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class SpellAssetFactory
{
    public string Root { get; }
    public string TextureDir => Root + "/Textures";
    public string MaterialDir => Root + "/Materials";
    public string PrefabDir => Root + "/Prefabs";

    public SpellAssetFactory(string setName) => Root = $"Assets/Spells/{setName}";

    public void PrepareFolders()
    {
        foreach (var dir in new[] { TextureDir, MaterialDir, PrefabDir })
            Directory.CreateDirectory(dir);
        AssetDatabase.Refresh();
    }

    /// <summary>生成贴图的导入设置：Sprite、点采样、无 mipmap、Clamp、不压缩。</summary>
    public void ConfigureTextureImports()
    {
        foreach (var path in Directory.GetFiles(TextureDir, "*.png"))
        {
            var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
            if (importer == null)
                continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    public Texture2D Tex(string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/{name}.png");
        if (asset == null) throw new FileNotFoundException($"missing texture: {name}.png");
        return asset;
    }

    /// <summary>贴图以 Sprite 类型导入时 Unity 自动生成的精灵图子资源。</summary>
    public Sprite SpriteOf(string textureName)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{TextureDir}/{textureName}.png");
        if (sprite == null) throw new FileNotFoundException($"missing sprite: {textureName}.png");
        return sprite;
    }

    public Material AdditiveMat(string name, string textureName, Color tint, float intensity = 1f)
    {
        var mat = LoadOrCreate(name, "SpellVfx/AdditiveParticle");
        mat.SetTexture("_MainTex", Tex(textureName));
        mat.SetColor("_TintColor", tint);
        mat.SetFloat("_Intensity", intensity);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    public Material AlphaMat(string name, string textureName, Color tint)
    {
        var mat = LoadOrCreate(name, "SpellVfx/AlphaParticle");
        mat.SetTexture("_MainTex", Tex(textureName));
        mat.SetColor("_TintColor", tint);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private Material LoadOrCreate(string name, string shaderName)
    {
        var path = $"{MaterialDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
            return mat;

        var shader = Shader.Find(shaderName);
        if (shader == null) throw new FileNotFoundException($"shader not found: {shaderName}");
        mat = new Material(shader) { name = name };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
