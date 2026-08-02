using System;
using System.IO;
using Il2CppInterop.Runtime;
using UnityEngine;


namespace MetaMystia;

[AutoLog]
public static partial class Utils
{
    public static void test()
    {
        var Ingredients = GameData.CoreLanguage.Collections.DataBaseLanguage.Ingredients;
        foreach (var kvp in Ingredients)
        {
            Log.LogInfo($"Ingredient ID: {kvp.Key}, Name: {kvp.Value.ToString()}");
        }
    }
    public static Sprite GetArtWork(string filePath, Vector2 pivot, int width = 0, int height = 0, int pixelOffsetX = 0, int pixelOffsetY = 0)
    {
        if (!File.Exists(filePath)) return null;
        byte[] fileData = File.ReadAllBytes(filePath);
        var sprite = GetArtWorkFromBytes(fileData, pivot, width, height, pixelOffsetX, pixelOffsetY);
        if (sprite != null)
        {
            sprite.name = Path.GetFileNameWithoutExtension(filePath);
        }
        return sprite;
    }

    public static Sprite GetArtWorkFromBytes(byte[] fileData, Vector2 pivot, int width = 0, int height = 0, int pixelOffsetX = 0, int pixelOffsetY = 0)
    {
        var texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        ImageConversion.LoadImage(texture2D, fileData);

        if (width > 0 && height > 0 && (texture2D.width != width || texture2D.height != height))
        {
            var newTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var colors = new Color[width * height];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.clear;
            newTexture.SetPixels(colors);

            int x = (width - texture2D.width) / 2 + pixelOffsetX;
            int y = (height - texture2D.height) / 2 + pixelOffsetY;

            x = Mathf.Clamp(x, 0, width - texture2D.width);
            y = Mathf.Clamp(y, 0, height - texture2D.height);

            newTexture.SetPixels(x, y, texture2D.width, texture2D.height, texture2D.GetPixels());
            newTexture.Apply();
            texture2D = newTexture;
        }

        texture2D.filterMode = FilterMode.Point;
        texture2D.wrapMode = TextureWrapMode.Clamp;

        var sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), pivot, 48f);

        return sprite;
    }
    public static void FindAndProcessResources<T>(Action<T> action) where T : UnityEngine.Object
    {
        try
        {
            var type = Il2CppType.Of<T>();
            var foundAssets = Resources.FindObjectsOfTypeAll(type);

            if (foundAssets == null || foundAssets.Length == 0)
            {
                Log.LogWarning($"No {typeof(T).Name} assets found in memory.");
                return;
            }

            Log.LogDebug($"Found {foundAssets.Length} {typeof(T).Name} asset(s).");

            for (var i = 0; i < foundAssets.Length; i++)
            {
                var asset = foundAssets[i].TryCast<T>();
                if (asset == null) continue;
                action(asset);
            }
        }
        catch (Exception e)
        {
            Log.LogError($"Failed to process {typeof(T).Name} contents: {e.Message}\n{e.StackTrace}");
        }
    }
    public static Sprite BuildEmptySprite(int width = 64, int height = 64)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var colors = new Color[width * height];
        var transparent = new Color(0f, 0f, 0f, 0f);
        for (int i = 0; i < colors.Length; i++) colors[i] = transparent;
        texture.SetPixels(colors);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 48f);
    }
}
