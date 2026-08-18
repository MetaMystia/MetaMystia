using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using BepInEx;
using GameData.Core.Collections.CharacterUtility;
using SgrYuki.Utils;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MetaMystia;

/// <summary>
/// 这是一个导出工具类，提供将游戏内资源导出为外部文件的功能，方便美术创作者进行符合原游戏 Sprite 规格的二次创作。
/// 理论上不应在运行时调用此类方法，建议在调试阶段使用后移除相关调用。
/// </summary>
[AutoLog]
public static partial class ExportUtils
{
    public static string ExportRoot { get; set; } = Path.Combine(Paths.GameRootPath, "Exports");

    public static void ExportAllFoodSprite(string exportDir)
    {

        var allFood = GameData.CoreLanguage.Collections.DataBaseLanguage.Foods;
        foreach (var kvp in allFood)
        {
            Log.Warning($"Food ID: {kvp.Key}, BriefName: {kvp.Value.BriefName}, BriefDescription: {kvp.Value.BriefDescription}");
            Sprite sprite = kvp.Value.Visual;
            if (sprite != null)
            {
                var filename = $"Food_{kvp.Key}_{kvp.Value.BriefName}.png";
                var filepath = Path.Combine(exportDir, filename);

                TrySaveSprite(sprite, filepath);
                Log.LogInfo($"Exported: {filepath}");
            }
            else
            {
                Log.LogWarning($"Food ID {kvp.Key} has no sprite.");
            }
        }
    }
    
    
    public static void ExportAllIngredientSprite(string exportDir)
    {

        var allIngredients = GameData.CoreLanguage.Collections.DataBaseLanguage.Ingredients;
        foreach (var kvp in allIngredients)
        {
            Log.Warning($"Food ID: {kvp.Key}, BriefName: {kvp.Value.BriefName}, BriefDescription: {kvp.Value.BriefDescription}");
            Sprite sprite = kvp.Value.Visual;
            if (sprite != null)
            {
                var filename = $"Ingredent_{kvp.Key}_{kvp.Value.BriefName}.png";
                var filepath = Path.Combine(exportDir, filename);

                TrySaveSprite(sprite, filepath);
                Log.LogInfo($"Exported: {filepath}");
            }
            else
            {
                Log.LogWarning($"Food ID {kvp.Key} has no sprite.");
            }
        }
    }


    public static void ExportAllSpellSprite(string exportDir)
    {
        var allSpellCardLang = GameData.CoreLanguage.Collections.DataBaseLanguage.SpellLang;

        var allSpellCards = GameData.Core.Collections.NightSceneUtility.DataBaseNight.SpecialGuestSpellPortrayal;
        foreach (var kvp in allSpellCards)
        {
            var spellLang = allSpellCardLang.TryGetValue(kvp.Key, out var lang) ? lang : null;
            string positiveLang = spellLang != null && spellLang.Length > 0 ? spellLang[0]?.Name : "N/A";
            string negativeLang = spellLang != null && spellLang.Length > 1 ? spellLang[1]?.Name : "N/A";
            Log.Warning($"SpellCard ID: {kvp.Key} name: {positiveLang}/{negativeLang}");
            var positiveSpell = kvp.Value.Item1;
            var negativeSpell = kvp.Value.Item2;
            var sprite1 = positiveSpell.Asset.TryCast<Sprite>();
            if (sprite1 != null)
            {
                Log.LogInfo($"Exporting SpellCard {kvp.Key} - {positiveLang}");
                TrySaveSprite(sprite1, Path.Combine(exportDir, $"符卡_{kvp.Key}_奖励_{(positiveLang == "N/A" ? "Unknown" : positiveLang)}.png"));
            }
            var sprite2 = negativeSpell.Asset.TryCast<Sprite>();
            if (sprite2 != null)
            {
                Log.LogInfo($"Exporting SpellCard {kvp.Key} - {negativeLang}");
                TrySaveSprite(sprite2, Path.Combine(exportDir, $"符卡_{kvp.Key}_惩罚_{(negativeLang == "N/A" ? "Unknown" : negativeLang)}.png"));
            }
        }
    }

    public static void ExportAllPortrayals(string exportDir)
    {
        if (!Directory.Exists(exportDir))
            Directory.CreateDirectory(exportDir);

        var specialGuestPortrayals = DataBaseCharacter.SpecialGuestVisual;
        if (specialGuestPortrayals == null)
        {
            Log.LogError("DataBaseCharacter.SpecialGuestVisual is null");
            return;
        }

        foreach (var kvp in specialGuestPortrayals)
        {
            var id = kvp.Key;
            var visualData = kvp.Value;

            if (visualData == null || visualData.characterPortrayal == null || visualData.characterPortrayal.defaultPortrayal == null)
                continue;

            var portrayals = visualData.characterPortrayal.defaultPortrayal.m_VisualAssetAtlasReference;
            if (portrayals == null) continue;

            for (int i = 0; i < portrayals.Length; i++)
            {
                var assetRef = portrayals[i];
                if (assetRef == null) continue;

                Sprite sprite = null;
                if (assetRef.Asset != null)
                {
                    sprite = assetRef.Asset.TryCast<Sprite>();
                }

                if (sprite == null)
                {
                    var handle = assetRef.LoadAssetAsync<Sprite>();
                    sprite = handle.WaitForCompletion();
                }

                if (sprite != null)
                {
                    var subObjectName = assetRef.SubObjectName;
                    if (string.IsNullOrEmpty(subObjectName)) subObjectName = "Unnamed";

                    foreach (var c in Path.GetInvalidFileNameChars())
                    {
                        subObjectName = subObjectName.Replace(c, '_');
                    }

                    var filename = $"Special_{id}_{i}_{subObjectName}.png";
                    var filepath = Path.Combine(exportDir, filename);

                    TrySaveSprite(sprite, filepath);
                    Log.LogInfo($"Exported: {filepath}");
                }
                else
                {
                    Log.LogWarning($"Failed to load sprite for Special_{id}_{i}");
                }
            }
        }
    }

    public static void ExportAllSpriteCompact()
    {
        string exportDir = "E:/Desktop/TMI/SpriteCompacts";
        if (!Directory.Exists(exportDir))
            Directory.CreateDirectory(exportDir);

        Utils.FindAndProcessResources<CharacterSpriteSetCompact>(spriteSet =>
        {
            if (spriteSet == null || spriteSet.mainSprite == null)
                return;

            foreach (var eye in spriteSet.eyeSprite)
            {
                var filename = $"{eye.name}.png";
                var filepath = Path.Combine(exportDir, spriteSet.name, filename);
                Log.Warning($"Exporting eye sprite: {filepath}");
                TrySaveSprite(eye, filepath);
            }
            foreach (var main in spriteSet.mainSprite)
            {
                var filename = $"{main.name}.png";
                var filepath = Path.Combine(exportDir, spriteSet.name, filename);
                Log.Warning($"Exporting main sprite: {filepath}");
                TrySaveSprite(main, filepath);
            }
            Log.Warning($"Exported sprite set: {spriteSet.name}");
        });
    }
    public static void TrySaveSprite(Sprite sprite, string filepath)
    {
        Texture2D readableTexture = null;
        Texture2D finalTexture = null;
        try
        {
            var originalTexture = sprite.texture;
            var rect = sprite.textureRect;

            // 1. 提取裁剪后的区域 (readableTexture)
            if (!originalTexture.isReadable)
            {
                // 使用 RenderTexture 复制不可读的纹理
                var rt = RenderTexture.GetTemporary(
                    originalTexture.width,
                    originalTexture.height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default);

                Graphics.Blit(originalTexture, rt);
                var previous = RenderTexture.active;
                RenderTexture.active = rt;

                // 创建临时的完整纹理
                var tempFullTexture = new Texture2D(originalTexture.width, originalTexture.height, TextureFormat.RGBA32, false);
                tempFullTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tempFullTexture.Apply();

                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);

                // 从完整纹理中提取 Sprite 区域
                readableTexture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
                var pixels = tempFullTexture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
                readableTexture.SetPixels(pixels);
                readableTexture.Apply();

                // 立即销毁临时纹理
                UnityEngine.Object.DestroyImmediate(tempFullTexture);
            }
            else
            {
                // 纹理可读，直接提取 Sprite 区域
                readableTexture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
                var pixels = originalTexture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
                readableTexture.SetPixels(pixels);
                readableTexture.Apply();
            }

            // 2. 创建目标画布并根据偏移放置
            int targetWidth = Mathf.RoundToInt(sprite.rect.width);
            int targetHeight = Mathf.RoundToInt(sprite.rect.height);
            finalTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);

            // 填充透明背景
            Color[] transparent = new Color[targetWidth * targetHeight];
            for (int i = 0; i < transparent.Length; i++) transparent[i] = Color.clear;
            finalTexture.SetPixels(transparent);

            // 获取偏移并放置
            Vector2 offset = sprite.textureRectOffset;
            finalTexture.SetPixels(Mathf.RoundToInt(offset.x), Mathf.RoundToInt(offset.y), (int)rect.width, (int)rect.height, readableTexture.GetPixels());
            finalTexture.Apply();

            byte[] pngData = ImageConversion.EncodeToPNG(finalTexture);
            var directory = Path.GetDirectoryName(filepath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllBytes(filepath, pngData);
        }
        finally
        {
            if (readableTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(readableTexture);
            }
            if (finalTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(finalTexture);
            }
        }
    }
    public static string DumpDataBase(string exportDir = null)
    {
        var recipesList = new System.Collections.Generic.List<object>();
        foreach (var kvp in GameData.Core.Collections.DataBaseCore.Recipes)
        {
            var recipe = kvp.Value;
            recipesList.Add(new
            {
                id = recipe.Id,
                foodId = recipe.FoodID,
                ingredients = recipe.Ingredients,
                baseCookTime = recipe.BaseCookTime,
                cookerType = recipe.CookerType.ToString()
            });
        }

        var foodsList = new System.Collections.Generic.List<object>();
        foreach (var kvp in GameData.Core.Collections.DataBaseCore.Foods)
        {
            var food = kvp.Value;
            object tags = null;
            try { tags = food.Tags; } catch { }

            foodsList.Add(new
            {
                id = food.Id,
                briefName = food.Text.BriefName,
                briefDescription = food.Text.BriefDescription,
                level = food.Level,
                tags = tags,
                baseValue = food.BaseValue,
            });
        }

        var ingredientsList = new System.Collections.Generic.List<object>();
        foreach (var kvp in GameData.Core.Collections.DataBaseCore.Ingredients)
        {
            var ingredient = kvp.Value;
            object tags = null;
            try { tags = ingredient.Tags; } catch { }

            ingredientsList.Add(new
            {
                id = ingredient.Id,
                name = ingredient.Text.BriefName,
                description = ingredient.Text.BriefDescription,
                level = ingredient.Level,
                prefix = ingredient.Prefix,
                isFish = ingredient.IsFish,
                isMeat = ingredient.IsMeat,
                isVeg = ingredient.IsVeg,
                baseValue = ingredient.BaseValue,
                tags = tags
            });
        }

        var beverageList = new System.Collections.Generic.List<object>();
        foreach (var kvp in GameData.Core.Collections.DataBaseCore.Beverages)
        {
            var beverage = kvp.Value;
            object tags = null;
            try { tags = beverage.Tags; } catch { }

            beverageList.Add(new
            {
                id = beverage.Id,
                briefName = beverage.Text.BriefName,
                briefDescription = beverage.Text.BriefDescription,
                level = beverage.Level,
                tags = tags,
                baseValue = beverage.BaseValue,
            });
        }

        var foodTagsList = new System.Collections.Generic.List<object>();
        foreach (var kvp in GameData.CoreLanguage.Collections.DataBaseLanguage.FoodTags)
        {
            foodTagsList.Add(new
            {
                id = kvp.Key,
                name = kvp.Value,
            });
        }

        var bevTagsList = new System.Collections.Generic.List<object>();
        foreach (var kvp in GameData.CoreLanguage.Collections.DataBaseLanguage.BeverageTags)
        {
            bevTagsList.Add(new
            {
                id = kvp.Key,
                name = kvp.Value,
            });
        }


        var data = new
        {
            recipes = recipesList,
            foods = foodsList,
            ingredients = ingredientsList,
            beverages = beverageList,
            foodTags = foodTagsList,
            beverageTags = bevTagsList
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        string json = JsonSerializer.Serialize(data, options);

        if (!string.IsNullOrEmpty(exportDir))
        {
            if (!Directory.Exists(exportDir))
            {
                Directory.CreateDirectory(exportDir);
            }
            string filePath = Path.Combine(exportDir, "database.json");
            File.WriteAllText(filePath, json);
            Log.LogInfo($"Database dumped to {filePath}");
        }

        Log.LogInfo("Dumped database.");
        return json;
    }

    public static void ExportAllSelfClothesSprite()
    {
        string exportDir = Path.Combine(ExportRoot, "SelfClothes");
        if (!Directory.Exists(exportDir))
            Directory.CreateDirectory(exportDir);



        DumpClothesSprite(DataBaseCharacter.SelfSpriteSet.defaultSkin, exportDir);

        foreach (var clothes in DataBaseCharacter.SelfSpriteSet.explicits)
        {
            DumpClothesSprite(clothes, exportDir);
        }
    }

    public static void ExportBaseSprite()
    {
        Il2CppSystem.Collections.Generic.IReadOnlyList<Sprite> baseSprite = GameData.Core.Collections.CharacterUtility.CharacterSpriteSetFull.BaseSprite; // LENGTH = 12
        for (int i = 0; i < 12; i++)
        {
            var sprite = baseSprite[i];
            if (sprite != null)
            {
                var filename = $"Base_{i / 3}, {i % 3}.png";
                var filepath = Path.Combine(ExportRoot, filename);
                Log.Warning($"Exporting base sprite: {filepath}");
                TrySaveSprite(sprite, filepath);
            }
        }
    }
    public static void DumpClothesSprite(CharacterSpriteSetCompact clothes, string exportDir)
    {
        Sprite[] mainSprites = clothes.MainSprite;
        Sprite[] eyeSprites = clothes.EyeSprite;
        Sprite[] hairSprites = clothes.HairSprite;
        Sprite[] backSprites = clothes.BackSprite;
        string clothesName = clothes.name;
        for (int i = 0; i < mainSprites.Length; i++)
        {
            var main = mainSprites[i];
            if (main != null)
            {
                var filename = $"{clothesName}/Main_{i / 3}, {i % 3}.png";
                var filepath = Path.Combine(exportDir, filename);
                Log.Warning($"Exporting self clothes main sprite: {filepath}");
                TrySaveSprite(main, filepath);
            }
        }
        for (int i = 0; i < eyeSprites.Length; i++)
        {
            var eye = eyeSprites[i];
            if (eye != null)
            {
                var filename = $"{clothesName}/Eyes_{i / 4}, {i % 4}.png";
                var filepath = Path.Combine(exportDir, filename);
                Log.Warning($"Exporting self clothes eye sprite: {filepath}");
                TrySaveSprite(eye, filepath);
            }
        }
        for (int i = 0; i < hairSprites.Length; i++)
        {
            var hair = hairSprites[i];
            if (hair != null)
            {
                var filename = $"{clothesName}/Hair_{i / 3}, {i % 3}.png";
                var filepath = Path.Combine(exportDir, filename);
                Log.Warning($"Exporting self clothes hair sprite: {filepath}");
                TrySaveSprite(hair, filepath);
            }
        }
        for (int i = 0; i < backSprites.Length; i++)
        {
            var back = backSprites[i];
            if (back != null)
            {
                var filename = $"{clothesName}/Back_{i / 3}, {i % 3}.png";
                var filepath = Path.Combine(exportDir, filename);
                Log.Warning($"Exporting self clothes back sprite: {filepath}");
                TrySaveSprite(back, filepath);
            }
        }
    }

    // 错误的使用
    public static void ExportAllClothesPortrait()
    {
        string exportDir = Path.Combine(ExportRoot, "ClothesPortraits");
        if (!Directory.Exists(exportDir))
            Directory.CreateDirectory(exportDir);

        foreach (var clothes in DataBaseCharacter.SelfPortrayalSet.explicitPortrayals)
        {
            var sprite = clothes.m_VisualAssetAtlasReference[0].Asset.TryCast<Sprite>();
            if (sprite != null)
            {
                var filename = $"{clothes.name}.png";
                var filepath = Path.Combine(exportDir, filename);
                Log.Warning($"Exporting self clothes portrait sprite: {filepath}");
                TrySaveSprite(sprite, filepath);
            }
        }
    }
    public static void ExportAllClothesItemIcon()
    {
        string exportDir = Path.Combine(ExportRoot, "ClothesItemIcons");
        if (!Directory.Exists(exportDir))
            Directory.CreateDirectory(exportDir);

        GameData.Core.Collections.DataBaseCore.Items.ToList().Where(x => x.Value.IsClothes).ToList().ForEach(x =>
        {
            var item = x.Value;
            var sprite = item.Text.Visual;
            if (sprite != null)
            {
                var filename = $"ClothItem_{x.Key}.png";
                var filepath = Path.Combine(exportDir, filename);
                Log.Warning($"Exporting cloth item icon: {filepath}");
                TrySaveSprite(sprite, filepath);
            }
        });
    }

    /// <summary>
    /// 将 Tilemap 导出为一张完整的 PNG 图片。
    /// 通过逐 Tile 读取 Sprite 并合成到目标纹理来实现，支持翻转和颜色着色。
    /// 注意：不处理 Tile 旋转（非90°倍数），仅处理水平/垂直翻转。
    /// </summary>
    public static void ExportTilemap(Tilemap tilemap, string filepath)
    {
        if (tilemap == null)
        {
            Log.LogError("ExportTilemap: Tilemap is null");
            return;
        }

        tilemap.CompressBounds();
        var bounds = tilemap.cellBounds;
        var grid = tilemap.layoutGrid;

        if (bounds.size.x <= 0 || bounds.size.y <= 0)
        {
            Log.LogWarning($"ExportTilemap: {tilemap.name} has no tiles.");
            return;
        }

        float ppu = DetectTilemapPPU(tilemap, bounds);
        int cellW = Mathf.RoundToInt(grid.cellSize.x * ppu);
        int cellH = Mathf.RoundToInt(grid.cellSize.y * ppu);

        // 使用 TilemapRenderer.bounds 获取实际视觉范围
        var tmRenderer = tilemap.GetComponent<TilemapRenderer>();
        float worldMinX, worldMinY;
        int totalW, totalH;
        if (tmRenderer != null)
        {
            var rBounds = tmRenderer.bounds;
            worldMinX = rBounds.min.x;
            worldMinY = rBounds.min.y;
            totalW = Mathf.CeilToInt(rBounds.size.x * ppu);
            totalH = Mathf.CeilToInt(rBounds.size.y * ppu);
        }
        else
        {
            var cellMin = tilemap.CellToWorld(bounds.min);
            var cellMax = tilemap.CellToWorld(bounds.max);
            worldMinX = cellMin.x;
            worldMinY = cellMin.y;
            totalW = Mathf.CeilToInt((cellMax.x - cellMin.x) * ppu);
            totalH = Mathf.CeilToInt((cellMax.y - cellMin.y) * ppu);
        }

        Log.LogInfo($"ExportTilemap: {tilemap.name} => {bounds.size.x}x{bounds.size.y} cells, " +
                    $"{cellW}x{cellH} px/cell, total {totalW}x{totalH}, PPU={ppu}");

        var outputPixels = new Color[totalW * totalH];
        var textureCache = new System.Collections.Generic.Dictionary<int, Texture2D>();

        try
        {
            for (int cx = bounds.xMin; cx < bounds.xMax; cx++)
            {
                for (int cy = bounds.yMin; cy < bounds.yMax; cy++)
                {
                    var cellPos = new Vector3Int(cx, cy, 0);
                    var sprite = tilemap.GetSprite(cellPos);
                    if (sprite == null) continue;

                    var tileColor = tilemap.GetColor(cellPos);
                    var matrix = tilemap.GetTransformMatrix(cellPos);
                    bool flipX = matrix.GetColumn(0).x < 0;
                    bool flipY = matrix.GetColumn(1).y < 0;

                    Color[] texPixels = GetReadableSpritePixels(sprite, textureCache);
                    if (texPixels == null) continue;

                    int texW = Mathf.RoundToInt(sprite.textureRect.width);
                    int texH = Mathf.RoundToInt(sprite.textureRect.height);
                    int logicalW = Mathf.RoundToInt(sprite.rect.width);
                    int logicalH = Mathf.RoundToInt(sprite.rect.height);
                    var texOff = sprite.textureRectOffset;
                    int offX = Mathf.RoundToInt(texOff.x);
                    int offY = Mathf.RoundToInt(texOff.y);

                    // 翻转时镜像 offset
                    int finalOffX = flipX ? (logicalW - texW - offX) : offX;
                    int finalOffY = flipY ? (logicalH - texH - offY) : offY;

                    // Sprite Pivot 对齐到 Cell Anchor，用世界坐标计算左下角像素坐标
                    var anchor = tilemap.tileAnchor;
                    var cellWorld = tilemap.CellToWorld(cellPos);
                    int cellPixelX = Mathf.RoundToInt((cellWorld.x - worldMinX) * ppu);
                    int cellPixelY = Mathf.RoundToInt((cellWorld.y - worldMinY) * ppu);
                    int spriteOriginX = cellPixelX + Mathf.RoundToInt(anchor.x * cellW) - Mathf.RoundToInt(sprite.pivot.x);
                    int spriteOriginY = cellPixelY + Mathf.RoundToInt(anchor.y * cellH) - Mathf.RoundToInt(sprite.pivot.y);

                    for (int py = 0; py < texH; py++)
                    {
                        for (int px = 0; px < texW; px++)
                        {
                            int srcX = flipX ? (texW - 1 - px) : px;
                            int srcY = flipY ? (texH - 1 - py) : py;

                            Color pixel = texPixels[srcY * texW + srcX];
                            pixel = new Color(pixel.r * tileColor.r, pixel.g * tileColor.g,
                                              pixel.b * tileColor.b, pixel.a * tileColor.a);
                            if (pixel.a <= 0f) continue;

                            int outX = spriteOriginX + finalOffX + px;
                            int outY = spriteOriginY + finalOffY + py;
                            if (outX < 0 || outX >= totalW || outY < 0 || outY >= totalH) continue;

                            int idx = outY * totalW + outX;
                            Color dst = outputPixels[idx];

                            // Source-over alpha 合成
                            float srcA = pixel.a;
                            float dstA = dst.a;
                            float outA = srcA + dstA * (1f - srcA);
                            if (outA > 0f)
                            {
                                outputPixels[idx] = new Color(
                                    (pixel.r * srcA + dst.r * dstA * (1f - srcA)) / outA,
                                    (pixel.g * srcA + dst.g * dstA * (1f - srcA)) / outA,
                                    (pixel.b * srcA + dst.b * dstA * (1f - srcA)) / outA,
                                    outA);
                            }
                        }
                    }
                }
            }

            var output = new Texture2D(totalW, totalH, TextureFormat.RGBA32, false);
            output.SetPixels(outputPixels);
            output.Apply();

            byte[] pngData = ImageConversion.EncodeToPNG(output);
            var dir = Path.GetDirectoryName(filepath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(filepath, pngData);

            UnityEngine.Object.DestroyImmediate(output);
            Log.LogInfo($"ExportTilemap: Saved to {filepath}");
        }
        finally
        {
            foreach (var tex in textureCache.Values)
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }

    /// <summary>
    /// 导出当前居酒屋地图的所有可视 Tilemap 合成图和各层单独图片。
    /// 自动过滤碰撞/逻辑层，仅合成可视层。
    /// </summary>
    public static void ExportIzakayaTilemaps()
    {
        var mapRoot = NightScene.MapManager.instance.transform;
        var allTilemaps = new System.Collections.Generic.List<Tilemap>();
        var visualTilemaps = new System.Collections.Generic.List<Tilemap>();
        foreach (var tm in mapRoot.GetComponentsInChildren<Tilemap>())
        {
            tm.CompressBounds();
            if (tm.cellBounds.size.x <= 0 || tm.cellBounds.size.y <= 0) continue;
            allTilemaps.Add(tm);
            var n = tm.name;
            if (n.Contains("Collision") || n.Contains("Collider") ||
                n.Contains("Ray") || n.Contains("Height") || n.Contains("Navigation"))
                continue;
            visualTilemaps.Add(tm);
        }
        ExportTilemapsComposite(visualTilemaps.ToArray(),
            Path.Combine(ExportRoot, "Tilemaps", "Composite_Visual.png"));
        foreach (var tm in allTilemaps)
            ExportTilemap(tm, Path.Combine(ExportRoot, "Tilemaps", $"Tilemap_{tm.name}.png"));
    }

    /// <summary>
    /// 将多个 Tilemap 按世界坐标合成到一张图上导出。
    /// tilemaps 应按渲染顺序传入（从底层到顶层）。
    /// </summary>
    public static void ExportTilemapsComposite(Tilemap[] tilemaps, string filepath)
    {
        if (tilemaps == null || tilemaps.Length == 0)
        {
            Log.LogError("ExportTilemapsComposite: No tilemaps provided");
            return;
        }

        // 从首个有效 Tile 获取 PPU
        float ppu = 0f;
        foreach (var tm in tilemaps)
        {
            if (tm == null) continue;
            tm.CompressBounds();
            if (ppu <= 0f) ppu = DetectTilemapPPU(tm, tm.cellBounds);
        }
        if (ppu <= 0f) ppu = 48f;

        // 使用 TilemapRenderer.bounds 获取实际视觉范围（包含超出 cell 的 sprite 溢出）
        float worldMinX = float.MaxValue, worldMinY = float.MaxValue;
        float worldMaxX = float.MinValue, worldMaxY = float.MinValue;

        foreach (var tm in tilemaps)
        {
            if (tm == null) continue;
            var bounds = tm.cellBounds;
            if (bounds.size.x <= 0 || bounds.size.y <= 0) continue;

            var tmRenderer = tm.GetComponent<TilemapRenderer>();
            if (tmRenderer != null)
            {
                var rBounds = tmRenderer.bounds;
                worldMinX = Mathf.Min(worldMinX, rBounds.min.x);
                worldMinY = Mathf.Min(worldMinY, rBounds.min.y);
                worldMaxX = Mathf.Max(worldMaxX, rBounds.max.x);
                worldMaxY = Mathf.Max(worldMaxY, rBounds.max.y);
            }
            else
            {
                var min = tm.CellToWorld(bounds.min);
                var max = tm.CellToWorld(bounds.max);
                worldMinX = Mathf.Min(worldMinX, min.x);
                worldMinY = Mathf.Min(worldMinY, min.y);
                worldMaxX = Mathf.Max(worldMaxX, max.x);
                worldMaxY = Mathf.Max(worldMaxY, max.y);
            }
        }

        if (worldMinX >= worldMaxX || worldMinY >= worldMaxY)
        {
            Log.LogWarning("ExportTilemapsComposite: No valid tilemap bounds");
            return;
        }

        int totalW = Mathf.CeilToInt((worldMaxX - worldMinX) * ppu);
        int totalH = Mathf.CeilToInt((worldMaxY - worldMinY) * ppu);

        Log.LogInfo($"ExportTilemapsComposite: world ({worldMinX},{worldMinY})-({worldMaxX},{worldMaxY}), " +
                    $"total {totalW}x{totalH} px, PPU={ppu}");

        var outputPixels = new Color[totalW * totalH];
        var textureCache = new System.Collections.Generic.Dictionary<int, Texture2D>();

        try
        {
            foreach (var tm in tilemaps)
            {
                if (tm == null) continue;
                var bounds = tm.cellBounds;
                if (bounds.size.x <= 0 || bounds.size.y <= 0) continue;

                var grid = tm.layoutGrid;
                int cellW = Mathf.RoundToInt(grid.cellSize.x * ppu);
                int cellH = Mathf.RoundToInt(grid.cellSize.y * ppu);

                Log.LogInfo($"  Compositing: {tm.name} ({bounds.size.x}x{bounds.size.y} cells)");

                for (int cx = bounds.xMin; cx < bounds.xMax; cx++)
                {
                    for (int cy = bounds.yMin; cy < bounds.yMax; cy++)
                    {
                        var cellPos = new Vector3Int(cx, cy, 0);
                        var sprite = tm.GetSprite(cellPos);
                        if (sprite == null) continue;

                        var tileColor = tm.GetColor(cellPos);
                        var matrix = tm.GetTransformMatrix(cellPos);
                        bool flipX = matrix.GetColumn(0).x < 0;
                        bool flipY = matrix.GetColumn(1).y < 0;

                        Color[] texPixels = GetReadableSpritePixels(sprite, textureCache);
                        if (texPixels == null) continue;

                        int texW = Mathf.RoundToInt(sprite.textureRect.width);
                        int texH = Mathf.RoundToInt(sprite.textureRect.height);
                        int logicalW = Mathf.RoundToInt(sprite.rect.width);
                        int logicalH = Mathf.RoundToInt(sprite.rect.height);
                        var texOff = sprite.textureRectOffset;
                        int offX = Mathf.RoundToInt(texOff.x);
                        int offY = Mathf.RoundToInt(texOff.y);

                        int finalOffX = flipX ? (logicalW - texW - offX) : offX;
                        int finalOffY = flipY ? (logicalH - texH - offY) : offY;

                        // 用世界坐标计算 cell 在输出图中的位置
                        var anchor = tm.tileAnchor;
                        var cellWorld = tm.CellToWorld(cellPos);
                        int cellPixelX = Mathf.RoundToInt((cellWorld.x - worldMinX) * ppu);
                        int cellPixelY = Mathf.RoundToInt((cellWorld.y - worldMinY) * ppu);
                        int spriteOriginX = cellPixelX + Mathf.RoundToInt(anchor.x * cellW) - Mathf.RoundToInt(sprite.pivot.x);
                        int spriteOriginY = cellPixelY + Mathf.RoundToInt(anchor.y * cellH) - Mathf.RoundToInt(sprite.pivot.y);

                        for (int py = 0; py < texH; py++)
                        {
                            for (int px = 0; px < texW; px++)
                            {
                                int srcX = flipX ? (texW - 1 - px) : px;
                                int srcY = flipY ? (texH - 1 - py) : py;

                                Color pixel = texPixels[srcY * texW + srcX];
                                pixel = new Color(pixel.r * tileColor.r, pixel.g * tileColor.g,
                                                  pixel.b * tileColor.b, pixel.a * tileColor.a);
                                if (pixel.a <= 0f) continue;

                                int outX = spriteOriginX + finalOffX + px;
                                int outY = spriteOriginY + finalOffY + py;
                                if (outX < 0 || outX >= totalW || outY < 0 || outY >= totalH) continue;

                                int idx = outY * totalW + outX;
                                Color dst = outputPixels[idx];
                                float srcA = pixel.a;
                                float dstA = dst.a;
                                float outA = srcA + dstA * (1f - srcA);
                                if (outA > 0f)
                                {
                                    outputPixels[idx] = new Color(
                                        (pixel.r * srcA + dst.r * dstA * (1f - srcA)) / outA,
                                        (pixel.g * srcA + dst.g * dstA * (1f - srcA)) / outA,
                                        (pixel.b * srcA + dst.b * dstA * (1f - srcA)) / outA,
                                        outA);
                                }
                            }
                        }
                    }
                }
            }

            var output = new Texture2D(totalW, totalH, TextureFormat.RGBA32, false);
            output.SetPixels(outputPixels);
            output.Apply();

            byte[] pngData = ImageConversion.EncodeToPNG(output);
            var dir = Path.GetDirectoryName(filepath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(filepath, pngData);

            UnityEngine.Object.DestroyImmediate(output);
            Log.LogInfo($"ExportTilemapsComposite: Saved to {filepath}");
        }
        finally
        {
            foreach (var tex in textureCache.Values)
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }

    private static float DetectTilemapPPU(Tilemap tilemap, BoundsInt bounds)
    {
        for (int x = bounds.xMin; x < bounds.xMax; x++)
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                var sprite = tilemap.GetSprite(new Vector3Int(x, y, 0));
                if (sprite != null) return sprite.pixelsPerUnit;
            }
        return 16f;
    }

    private static Color[] GetReadableSpritePixels(Sprite sprite,
        System.Collections.Generic.Dictionary<int, Texture2D> cache)
    {
        var originalTex = sprite.texture;
        var rect = sprite.textureRect;
        int w = Mathf.RoundToInt(rect.width);
        int h = Mathf.RoundToInt(rect.height);

        // Crunch 压缩纹理即使 isReadable 也无法 GetPixels，必须走 RenderTexture 路径
        var fmt = originalTex.format;
        bool isCrunched = fmt == TextureFormat.DXT1Crunched
                       || fmt == TextureFormat.DXT5Crunched
                       || fmt == TextureFormat.ETC_RGB4Crunched
                       || fmt == TextureFormat.ETC2_RGBA8Crunched;

        if (originalTex.isReadable && !isCrunched)
            return originalTex.GetPixels((int)rect.x, (int)rect.y, w, h);

        int texId = originalTex.GetInstanceID();
        if (!cache.TryGetValue(texId, out var readable))
        {
            var rt = RenderTexture.GetTemporary(
                originalTex.width, originalTex.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            Graphics.Blit(originalTex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            readable = new Texture2D(originalTex.width, originalTex.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            readable.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            cache[texId] = readable;
        }

        return readable.GetPixels((int)rect.x, (int)rect.y, w, h);
    }

    /// <summary>
    /// 递归遍历 GameObject 层级并 dump 所有 Renderer 信息，用于分析 Prefab 的可视化结构。
    /// </summary>
    public static void DumpHierarchyRenderers(GameObject root, string exportFilePath = null)
    {
        var sb = new System.Text.StringBuilder();
        DumpRendererRecursive(root.transform, 0, sb);
        string result = sb.ToString();
        Log.LogInfo($"Hierarchy dump for '{root.name}':\n{result}");

        if (!string.IsNullOrEmpty(exportFilePath))
        {
            var dir = Path.GetDirectoryName(exportFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(exportFilePath, result);
            Log.LogInfo($"Hierarchy dump saved to {exportFilePath}");
        }
    }

    private static void DumpRendererRecursive(Transform t, int depth, System.Text.StringBuilder sb)
    {
        string indent = new string(' ', depth * 2);
        string active = t.gameObject.activeSelf ? "" : " [INACTIVE]";
        sb.AppendLine($"{indent}{t.name}{active}");

        var renderers = t.GetComponents<Renderer>();
        foreach (var r in renderers)
        {
            string type = r.GetIl2CppType().Name;
            string extra = "";

            var sr = r.TryCast<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                var sp = sr.sprite;
                extra = $" sprite='{sp.name}' tex='{sp.texture?.name}' rect={sp.rect} ppu={sp.pixelsPerUnit}";
            }
            var tr = r.TryCast<UnityEngine.Tilemaps.TilemapRenderer>();
            if (tr != null)
            {
                var tilemap = t.GetComponent<Tilemap>();
                if (tilemap != null)
                {
                    tilemap.CompressBounds();
                    var b = tilemap.cellBounds;
                    extra = $" cells={b.size.x}x{b.size.y} (tiles exist={b.size.x > 0 && b.size.y > 0})";
                }
            }
            sb.AppendLine($"{indent}  [{type}] sortingLayer={r.sortingLayerName} order={r.sortingOrder}{extra}");
        }

        for (int i = 0; i < t.childCount; i++)
        {
            DumpRendererRecursive(t.GetChild(i), depth + 1, sb);
        }
    }

    public static void DumpAllBuff()
    {
        var buffDescription = GameData.CoreLanguage.Collections.DataBaseLanguage.BuffDescription;
        foreach (var kvp in buffDescription)
        {
            var id = kvp.Key;
            var desc = kvp.Value;
            Log.LogInfo($"Buff ID: {id}, Name: {desc.BriefName} Description: {desc.BriefDescription}");
            var sprite = desc.Visual;
            if (sprite != null)
            {
                var filename = $"Buff_{id}.png";
                var filepath = Path.Combine(ExportRoot, filename);
                Log.Warning($"Exporting buff sprite: {filepath}");
                TrySaveSprite(sprite, filepath);
            }
        }
    }
}
