using System;
using System.Collections.Generic;
using System.Linq;

using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.Tilemaps;

using DayScene;
using DayScene.Interactables;
using GameData.Core.Collections.DaySceneUtility;
using GameData.CoreLanguage;
using GameData.CoreLanguage.Collections;
using GameData.Profile;

using MetaMiku;
using MetaMystia.ResourceEx.Addressables;
using MetaMystia.ResourceEx.Addressables.Providers;
using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

[AutoLog]
public static partial class DayMapRegistry
{
    private sealed record Entry(DayMapConfig Config, string Package, string Label, string Uri)
    {
        public GameObject Template;
    }

    private static readonly Dictionary<int, Entry> Maps = new();
    private static readonly HashSet<int> Conflicts = new();
    private static bool providerRegistered;

    public static IEnumerable<int> Ids => Maps.Where(p => p.Value.Template != null).Select(p => p.Key);
    public static bool IsRegistered(int id) => Maps.TryGetValue(id, out var entry) && entry.Template != null;
    public static string GetDisplayName(int id) => IsRegistered(id) ? Maps[id].Config.name : null;
    public static string GetLabel(int id) => Maps.TryGetValue(id, out var entry) ? entry.Label : $"_ResourceEx_Map_{id}";
    public static string GetMarker(int id, string name) => $"{GetLabel(id)}_{name}";

    public static bool TryGetId(string label, out int id)
    {
        foreach (var pair in Maps)
        {
            if (pair.Value.Template == null || pair.Value.Label != label) continue;
            id = pair.Key;
            return true;
        }
        id = -1;
        return false;
    }

    public static void Merge(LoadedResourcePackage package)
    {
        foreach (var config in package.Config.dayMaps ?? new())
        {
            if (config == null) { Log.Error($"[{package.PackageName}] null dayMaps entry"); continue; }
            if (Conflicts.Contains(config.id)) continue;
            if (Maps.Remove(config.id))
            {
                Conflicts.Add(config.id);
                Log.Error($"Duplicate day map id {config.id}; all maps with this id are disabled");
                continue;
            }
            var label = GetLabel(config.id);
            Maps.Add(config.id, new(config, package.PackageLabel, label, $"rex://{package.PackageLabel}/dayMaps/{config.id}"));
        }
    }

    public static void RegisterAll()
    {
        if (Maps.Count == 0) return;
        if (!providerRegistered)
        {
            ClassInjector.RegisterTypeInIl2Cpp<InMemoryGameObjectProvider>();
            RuntimeAddressables.RegisterProviderType<GameObject>(new InMemoryGameObjectProvider().Cast<IResourceProvider>(),
                InMemoryGameObjectProvider.ProviderIdConst, InMemoryGameObjectProvider.AddAsset,
                InMemoryGameObjectProvider.RemoveAsset, InMemoryGameObjectProvider.HasAsset, InMemoryGameObjectProvider.GetAsset);
            providerRegistered = true;
        }
        foreach (var entry in Maps.Values)
        {
            if (entry.Template == null)
            {
                var error = Validate(entry.Config, entry.Package);
                if (error != null) { Log.Error($"[{entry.Package}] dayMaps[{entry.Config.id}]: {error}"); continue; }
                entry.Template = Build(entry);
                RuntimeAddressables.Register(entry.Uri, entry.Template);
            }
            RuntimeAddressables.TryGetReference<GameObject>(entry.Uri, out var reference);
            var c = entry.Config;
            var markerNames = c.spawnMarkers.Select(m => GetMarker(c.id, m.name)).ToArray();
            DataBaseDay.mapData.ForceAddOrUpdateBoxedValue(entry.Label, new DaySceneMapProfile.MapNode
            {
                mapName = entry.Label, parent = "", mapAssetReference = reference,
                mapCollectableLabels = new Il2CppStringArray(0), mapSpawnMarkerLabels = new Il2CppStringArray(markerNames),
                level1IzakayaId = new Il2CppStructArray<int>(0), level2IzakayaId = new Il2CppStructArray<int>(0), level3IzakayaId = new Il2CppStructArray<int>(0)
            });
            DataBaseDay.mapReference[entry.Label] = reference;
            DataBaseDay.allCollectablesLabels[entry.Label] = new Il2CppSystem.Collections.Generic.HashSet<string>();
            var markers = new Il2CppSystem.Collections.Generic.HashSet<string>();
            foreach (var name in markerNames) markers.Add(name);
            DataBaseDay.allSpawnMarkerLabels[entry.Label] = markers;
            Log.Info($"Registered day map {c.id}: {entry.Label}");
        }
        RegisterLanguages();
    }

    public static void RegisterLanguages()
    {
        if (DaySceneLanguage.MapLanguageData == null) return;
        foreach (var entry in Maps.Values)
            if (entry.Template != null)
                DaySceneLanguage.MapLanguageData[entry.Label] = new LanguageBase(entry.Config.name, entry.Config.description ?? "");
    }

    public static bool TryGetDestination(int id, string marker, out string label, out string markerName)
    {
        label = markerName = null;
        if (!Maps.TryGetValue(id, out var entry) || entry.Template == null) return false;
        marker = string.IsNullOrEmpty(marker) ? entry.Config.defaultSpawnMarker : marker;
        if (!entry.Config.spawnMarkers.Any(m => m.name == marker)) return false;
        label = entry.Label;
        markerName = GetMarker(id, marker);
        return DataBaseDay.mapReference.ContainsKey(label);
    }

    private static RexAsset Asset(string path, string package)
    {
        return RexAssetRegistry.TryResolveUri(path, package, out var uri) && RexAssetRegistry.Assets.TryGetValue(uri, out var asset) ? asset : null;
    }

    private static bool Numbers(float[] values, int count) => values != null && values.Length == count && values.All(float.IsFinite);
    private static bool Coordinate(float x, float y) => float.IsFinite(x) && float.IsFinite(y) && Math.Abs(x) <= 4096 && Math.Abs(y) <= 4096;
    private static bool Sorting(string name) => !string.IsNullOrEmpty(name) && SortingLayer.IDToName(SortingLayer.NameToID(name)) == name;

    internal static string Validate(DayMapConfig c, string package)
    {
        if (c.formatVersion != 1 || string.IsNullOrWhiteSpace(c.name)) return "formatVersion/name invalid";
        if (c.tiles == null || c.layers == null || c.objects == null || c.collisions == null || c.spawnMarkers == null || c.camera == null) return "required collection/camera is null";
        if (c.tiles.Count > 4096 || c.layers.Count > 32 || c.objects.Count > 4096 || c.collisions.Count > 4096 || c.spawnMarkers.Count > 256) return "map capacity exceeded";
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in c.tiles)
        {
            if (t == null || string.IsNullOrWhiteSpace(t.key) || !keys.Add(t.key)) return "duplicate/empty tile key";
            if (Asset(t.image, package) is not RexImageAsset image) return $"image missing: {t.image}";
            var r = t.rect;
            if (r == null || r.Length != 4 || r[0] < 0 || r[1] < 0 || r[2] <= 0 || r[3] <= 0 || (long)r[0] + r[2] > image.Texture.width || (long)r[1] + r[3] > image.Texture.height) return $"tile rect out of bounds: {t.key}";
            if (!Numbers(t.pivot, 2) || t.pivot.Any(v => v < 0 || v > 1) || !float.IsFinite(t.pixelsPerUnit) || t.pixelsPerUnit <= 0) return $"tile geometry invalid: {t.key}";
        }
        int cells = 0;
        foreach (var layer in c.layers)
        {
            if (layer == null || layer.cells == null || !Sorting(layer.sortingLayer) || layer.sortingOrder < -32768 || layer.sortingOrder > 32767) return "layer settings invalid";
            cells += layer.cells.Count;
            if (cells > 100000) return "cell limit exceeded";
            var positions = new HashSet<(int, int)>();
            foreach (var cell in layer.cells)
                if (cell == null || cell.tile == null || !keys.Contains(cell.tile) || !Coordinate(cell.x, cell.y) || !positions.Add((cell.x, cell.y))) return "cell reference/position invalid";
        }
        foreach (var o in c.objects)
            if (o == null || o.tile == null || !keys.Contains(o.tile) || !Coordinate(o.x, o.y) || !Numbers(o.scale, 2) || o.scale.Any(v => v <= 0) || !Sorting(o.sortingLayer) || o.sortingOrder < -32768 || o.sortingOrder > 32767 || (o.sortByY && Math.Abs(o.y) > 1023)) return "object settings invalid";
        if (c.height != null)
        {
            if (c.height.cells == null || c.height.cells.Count > 100000) return "height cells missing or capacity exceeded";
            var positions = new HashSet<(int, int)>();
            foreach (var cell in c.height.cells)
                if (cell == null || !Coordinate(cell.x, cell.y) || !float.IsFinite(cell.slope) || Math.Abs(cell.slope) > 1 ||
                    !positions.Add((cell.x, cell.y))) return "height cell position/slope invalid";
        }
        foreach (var box in c.collisions)
            if (box == null || !Coordinate(box.x, box.y) || !float.IsFinite(box.width) || !float.IsFinite(box.height) || box.width <= 0 || box.height <= 0) return "collision box invalid";
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var marker in c.spawnMarkers)
        {
            if (marker == null || string.IsNullOrWhiteSpace(marker.name) || !names.Add(marker.name) || !Coordinate(marker.x, marker.y) || !Enum.IsDefined(marker.rotation)) return "spawn marker invalid";
            foreach (var box in c.collisions)
                if (Math.Abs(marker.x - box.x) <= box.width / 2 && Math.Abs(marker.y - box.y) <= box.height / 2) return $"spawn inside collision: {marker.name}";
        }
        if (c.defaultSpawnMarker == null || !names.Contains(c.defaultSpawnMarker)) return "defaultSpawnMarker missing";
        var camera = c.camera;
        if (!Numbers(camera.position, 3)) return "camera position invalid";
        if (camera.shouldFollow && (!Numbers(camera.bounds, 4) || camera.bounds[0] >= camera.bounds[2] || camera.bounds[1] >= camera.bounds[3])) return "camera bounds invalid";
        if (c.mapBGM == null || Asset(c.mapBGM.intro, package) is not RexAudioAsset intro || intro.Clip == null ||
            Asset(c.mapBGM.loop, package) is not RexAudioAsset loop || loop.Clip == null) return "BGM intro/loop missing or unsupported";
        return null;
    }

    private static GameObject Child(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject Build(Entry entry)
    {
        var c = entry.Config;
        var root = new GameObject(entry.Label);
        root.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(root);
        var map = root.AddComponent<DaySceneMap>();
        map.spawnMarkerField = Child("SpawnMarkers", root.transform).transform;
        map.collectableField = Child("Collectables", root.transform).transform;
        map.shouldCameraFollow = c.camera.shouldFollow;
        map.cameraDefaultPosition = new Vector3(c.camera.position[0], c.camera.position[1], c.camera.position[2]);
        if (c.camera.shouldFollow)
        {
            var boundary = Child("CameraBounds", root.transform);
            boundary.layer = LayerMask.NameToLayer("Ignore Raycast");
            var polygon = boundary.AddComponent<PolygonCollider2D>();
            var b = c.camera.bounds;
            polygon.SetPath(0, new Vector2[] { new(b[0], b[1]), new(b[0], b[3]), new(b[2], b[3]), new(b[2], b[1]) });
            map.boundingShape = polygon;
        }
        map.mapBGM = ScriptableObject.CreateInstance<LoopedBGMPackage>();
        map.mapBGM.hideFlags = HideFlags.HideAndDontSave;
        map.mapBGM.intro = ((RexAudioAsset)Asset(c.mapBGM.intro, entry.Package)).Clip;
        map.mapBGM.loop = ((RexAudioAsset)Asset(c.mapBGM.loop, entry.Package)).Clip;
        var tiles = new Dictionary<string, Tile>();
        foreach (var t in c.tiles)
        {
            var image = (RexImageAsset)Asset(t.image, entry.Package);
            var r = t.rect;
            var sprite = Sprite.Create(image.Texture, new Rect(r[0], r[1], r[2], r[3]), new Vector2(t.pivot[0], t.pivot[1]), t.pixelsPerUnit);
            sprite.name = t.key;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.hideFlags = HideFlags.HideAndDontSave;
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            tiles.Add(t.key, tile);
        }
        var grid = Child("Grid", root.transform).AddComponent<Grid>();
        grid.cellSize = new Vector3(1, 1, 0);
        if (c.height?.cells.Count > 0) map.height = BuildHeight(c.height, grid.transform);
        foreach (var layer in c.layers)
        {
            var go = Child(layer.name ?? "Tilemap", grid.transform);
            var tm = go.AddComponent<Tilemap>();
            tm.tileAnchor = Vector3.zero;
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = layer.sortingLayer;
            renderer.sortingOrder = layer.sortingOrder;
            foreach (var cell in layer.cells) tm.SetTile(new Vector3Int(cell.x, cell.y, 0), tiles[cell.tile]);
            tm.CompressBounds();
        }
        foreach (var o in c.objects)
        {
            var go = Child(o.name ?? o.tile, root.transform);
            go.transform.localPosition = new Vector3(o.x, o.y, 0);
            go.transform.localScale = new Vector3(o.scale[0], o.scale[1], 1);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = tiles[o.tile].sprite;
            renderer.sortingLayerName = o.sortingLayer;
            renderer.sortingOrder = o.sortingOrder;
            if (o.sortByY) go.AddComponent<DEYU.Utils.LayerSortingController>();
        }
        foreach (var b in c.collisions)
        {
            var go = Child(b.name ?? "Collision", root.transform);
            go.transform.localPosition = new Vector3(b.x, b.y, 0);
            go.AddComponent<BoxCollider2D>().size = new Vector2(b.width, b.height);
        }
        foreach (var m in c.spawnMarkers)
        {
            var go = Child(m.name, map.spawnMarkerField);
            go.transform.localPosition = new Vector3(m.x, m.y, 0);
            var marker = go.AddComponent<SpawnMarker>();
            marker.spawnMarkerName = GetMarker(c.id, m.name);
            marker.targetRotation = m.rotation;
        }
        return root;
    }

    private static Tilemap BuildHeight(DayMapHeightConfig config, Transform grid)
    {
        // 原游戏采样红色通道为坡度大小，绿色通道决定正负；纹理须保持 CPU 可读。
        var texture = new Texture2D(256, 2, TextureFormat.RGBA32, false);
        texture.name = "HeightSlopes";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[512];
        for (int x = 0; x < 256; x++)
        {
            pixels[x] = new Color(x / 255f, 0, 0, 1);
            pixels[256 + x] = new Color(x / 255f, 1, 0, 1);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        var map = Child("HEIGHT_MAP", grid).AddComponent<Tilemap>();
        map.tileAnchor = Vector3.zero;
        var tiles = new Dictionary<int, Tile>();
        foreach (var cell in config.cells)
        {
            int magnitude = Mathf.RoundToInt(Math.Abs(cell.slope) * 255);
            if (magnitude == 0) continue;
            int sign = cell.slope > 0 ? 1 : 0;
            int key = sign * 256 + magnitude;
            if (!tiles.TryGetValue(key, out var tile))
            {
                var sprite = Sprite.Create(texture, new Rect(magnitude, sign, 1, 1), Vector2.zero, 1);
                sprite.hideFlags = HideFlags.HideAndDontSave;
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = $"Slope_{cell.slope}";
                tile.hideFlags = HideFlags.HideAndDontSave;
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                tiles.Add(key, tile);
            }
            map.SetTile(new Vector3Int(cell.x, cell.y, 0), tile);
        }
        // 只供 HeightBlendedInputProcessorComponent 读取，不添加渲染器或碰撞组件。
        map.CompressBounds();
        return map;
    }
}
