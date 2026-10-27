using System;
using System.Collections.Generic;
using System.Linq;

using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.ResourceLocations;
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
    public const int FormatVersion = 3;
    public const int ChunkSize = 1024;
    private const int ImageSizeLimit = 16384;
    private const int PointLimit = 4096;

    // 模板中的渲染器只记录图块路径，地图被加载时才解码并赋予精灵（锚点为图块左下角）。
    private sealed record ArtSlot(SpriteRenderer Renderer, string Path, float PixelsPerUnit);

    private sealed record Entry(DayMapConfig Config, string Package, string Label, string Uri)
    {
        public GameObject Template;
        public DaySceneMap Map;
        public readonly List<ArtSlot> Slots = new();
        public readonly List<UnityEngine.Object> Loaded = new();
        public Action ReleaseBgm;
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

    /// <summary>本包地图引用的图片路径；这些图片在进入地图时才解码。</summary>
    public static IReadOnlySet<string> CollectArtPaths(LoadedResourcePackage package)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var map in package.Config?.dayMaps ?? new())
        {
            if (map == null) continue;
            var images = (map.layers ?? new()).Select(l => l?.image).Concat((map.prefabs ?? new()).Select(p => p?.image))
                .Where(i => i?.dir != null && i.chunks != null).SelectMany(i => i.chunks.Where(c => c is { Length: 2 }).Select(c => ChunkPath(i, c)));
            foreach (var image in images)
            {
                if (RexUri.TryParse(image, out var uri))
                {
                    if (uri.PackageName == package.PackageLabel) paths.Add(uri.Path);
                }
                else if (RexUri.NormalizePath(image) is { } path) paths.Add(path);
            }
        }
        return paths;
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
                InMemoryGameObjectProvider.SetLifetime(RuntimeAddressables.KeyToGuid(entry.Uri), () => AcquireArt(entry), () => ReleaseArt(entry));
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

    private static string ChunkPath(DayMapImageConfig image, int[] chunk) => $"{image.dir}/{chunk[0]}_{chunk[1]}.png";

    // 块 [列,行] 自图片左上角起算；末列末行可能小于 ChunkSize。
    private static Vector2Int ChunkExtent(DayMapImageConfig image, int[] chunk) =>
        new(Math.Min(ChunkSize, image.width - chunk[0] * ChunkSize), Math.Min(ChunkSize, image.height - chunk[1] * ChunkSize));

    private static RexAsset Asset(string path, string package)
    {
        return RexAssetRegistry.TryResolveUri(path, package, out var uri) && RexAssetRegistry.Assets.TryGetValue(uri, out var asset) ? asset : null;
    }

    // 只读 PNG 头部的宽高，校验时不解码。
    private static Vector2Int? PngSize(RexAsset asset)
    {
        var b = asset?.Bytes;
        if (b == null || b.Length < 24 || b[0] != 0x89 || b[1] != 'P' || b[2] != 'N' || b[3] != 'G' || b[12] != 'I' || b[13] != 'H' || b[14] != 'D' || b[15] != 'R') return null;
        return new Vector2Int(b[16] << 24 | b[17] << 16 | b[18] << 8 | b[19], b[20] << 24 | b[21] << 16 | b[22] << 8 | b[23]);
    }

    private static bool Numbers(float[] values, int count) => values != null && values.Length == count && values.All(float.IsFinite);
    private static bool Coordinate(float x, float y) => float.IsFinite(x) && float.IsFinite(y) && Math.Abs(x) <= 4096 && Math.Abs(y) <= 4096;
    private static bool Point(float[] p) => p != null && p.Length == 2 && Coordinate(p[0], p[1]);
    private static bool Points(float[][] points, int min) => points != null && points.Length >= min && points.Length <= PointLimit && points.All(Point);
    private static bool Order(int order) => order >= -32768 && order <= 32767;
    private static bool Sorting(string name) => !string.IsNullOrEmpty(name) && SortingLayer.IDToName(SortingLayer.NameToID(name)) == name;

    private static string ValidateCollider(DayMapColliderConfig c) => c?.shape switch
    {
        "polygon" when c.paths is { Length: > 0 and <= 64 } && c.paths.All(p => Points(p, 3)) => null,
        "edge" when Points(c.points, 2) => null,
        "box" when Coordinate(c.x, c.y) && c.width > 0 && c.height > 0 && float.IsFinite(c.width) && float.IsFinite(c.height) && float.IsFinite(c.rotation) => null,
        "circle" when Coordinate(c.x, c.y) && c.radius > 0 && float.IsFinite(c.radius) => null,
        _ => $"collider invalid: {c?.name ?? c?.shape}"
    };

    // 偶奇规则：多路径时内层路径为洞，与 PolygonCollider2D 一致。
    private static bool InsidePath(float[][] path, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = path.Length - 1; i < path.Length; j = i++)
            if ((path[i][1] > y) != (path[j][1] > y) && x < (path[j][0] - path[i][0]) * (y - path[i][1]) / (path[j][1] - path[i][1]) + path[i][0])
                inside = !inside;
        return inside;
    }

    private static bool Contains(DayMapColliderConfig c, float x, float y)
    {
        switch (c.shape)
        {
            case "polygon": return c.paths.Count(p => InsidePath(p, x, y)) % 2 == 1;
            case "circle": return (x - c.x) * (x - c.x) + (y - c.y) * (y - c.y) <= c.radius * c.radius;
            case "box":
                var angle = -c.rotation * Mathf.Deg2Rad;
                float dx = x - c.x, dy = y - c.y;
                return Math.Abs(dx * Mathf.Cos(angle) - dy * Mathf.Sin(angle)) <= c.width / 2 && Math.Abs(dx * Mathf.Sin(angle) + dy * Mathf.Cos(angle)) <= c.height / 2;
            default: return false;
        }
    }

    private static string ValidateImage(DayMapImageConfig image, string package)
    {
        if (image == null || image.width <= 0 || image.height <= 0 || image.width > ImageSizeLimit || image.height > ImageSizeLimit || string.IsNullOrEmpty(image.dir) || image.chunks == null)
            return "image size/dir invalid";
        var columns = (image.width + ChunkSize - 1) / ChunkSize;
        var rows = (image.height + ChunkSize - 1) / ChunkSize;
        var seen = new HashSet<(int, int)>();
        foreach (var chunk in image.chunks)
        {
            if (chunk is not { Length: 2 } || chunk[0] < 0 || chunk[1] < 0 || chunk[0] >= columns || chunk[1] >= rows || !seen.Add((chunk[0], chunk[1])))
                return "chunk index invalid";
            if (PngSize(Asset(ChunkPath(image, chunk), package)) != ChunkExtent(image, chunk)) return $"chunk missing or size mismatch: {ChunkPath(image, chunk)}";
        }
        return null;
    }

    internal static string Validate(DayMapConfig c, string package)
    {
        if (c.formatVersion != FormatVersion || string.IsNullOrWhiteSpace(c.name)) return "formatVersion/name invalid";
        if (c.pixelsPerUnit <= 0) return "pixelsPerUnit invalid";
        if (c.layers == null || c.prefabs == null || c.objects == null || c.colliders == null || c.spawnMarkers == null || c.camera == null || c.mapBGM == null) return "required collection/camera/mapBGM is null";
        if (c.layers.Count > 32 || c.prefabs.Count > 4096 || c.objects.Count > 8192 || c.colliders.Count > 4096 || c.spawnMarkers.Count > 256) return "map capacity exceeded";
        foreach (var layer in c.layers)
        {
            if (layer == null || !Sorting(layer.sortingLayer) || !Order(layer.sortingOrder) || (layer.color != null && !Numbers(layer.color, 4)) ||
                !Coordinate((float)layer.x / c.pixelsPerUnit, (float)layer.y / c.pixelsPerUnit)) return $"layer settings invalid: {layer?.name}";
            if (ValidateImage(layer.image, package) is { } imageError) return $"{imageError} in layer {layer.name}";
        }
        var prefabs = new Dictionary<string, DayMapPrefabConfig>(StringComparer.Ordinal);
        foreach (var p in c.prefabs)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.key) || !prefabs.TryAdd(p.key, p)) return "duplicate/empty prefab key";
            if (ValidateImage(p.image, package) is { } imageError) return $"{imageError} in prefab {p.key}";
            if (!Numbers(p.pivot, 2) || !float.IsFinite(p.pixelsPerUnit) || p.pixelsPerUnit <= 0 || !Sorting(p.sortingLayer) || !Order(p.sortingOrder)) return $"prefab settings invalid: {p.key}";
            if (p.colliders == null) return $"prefab colliders missing: {p.key}";
            if (p.colliders.Select(ValidateCollider).FirstOrDefault(e => e != null) is { } colliderError) return $"{colliderError} in prefab {p.key}";
        }
        foreach (var o in c.objects)
            if (o == null || o.prefab == null || !prefabs.TryGetValue(o.prefab, out var prefab) || !Coordinate(o.x, o.y) || !Numbers(o.scale, 2) || o.scale.Any(v => v == 0) ||
                (prefab.sortByY && Math.Abs(o.y) > 1023)) return $"object invalid: {o?.name ?? o?.prefab}";
        if (c.colliders.Select(ValidateCollider).FirstOrDefault(e => e != null) is { } mapColliderError) return mapColliderError;
        if (c.height != null)
        {
            if (c.height.cells == null || c.height.cells.Count > 100000) return "height cells missing or capacity exceeded";
            var positions = new HashSet<(int, int)>();
            foreach (var cell in c.height.cells)
                if (cell == null || !Coordinate(cell.x, cell.y) || !float.IsFinite(cell.slope) || Math.Abs(cell.slope) > 1 || !positions.Add((cell.x, cell.y)))
                    return "height cell position/slope invalid";
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var marker in c.spawnMarkers)
        {
            if (marker == null || string.IsNullOrWhiteSpace(marker.name) || !names.Add(marker.name) || !Coordinate(marker.x, marker.y) || !Enum.IsDefined(marker.rotation)) return "spawn marker invalid";
            var blocked = c.colliders.Any(box => Contains(box, marker.x, marker.y)) || c.objects.Any(o =>
                prefabs[o.prefab].colliders.Any(box => Contains(box, (marker.x - o.x) / o.scale[0], (marker.y - o.y) / o.scale[1])));
            if (blocked) return $"spawn inside collision: {marker.name}";
        }
        if (c.defaultSpawnMarker == null || !names.Contains(c.defaultSpawnMarker)) return "defaultSpawnMarker missing";
        var camera = c.camera;
        if (!Numbers(camera.position, 3)) return "camera position invalid";
        if (camera.shouldFollow && !Points(camera.bounds, 3)) return "camera bounds invalid";
        var bgm = c.mapBGM;
        if (bgm.native != null)
        {
            if (bgm.intro != null || bgm.loop != null) return "mapBGM native and intro/loop are exclusive";
            if (!NativeAssetExists<LoopedBGMPackage>(bgm.native)) return $"native BGM not found: {bgm.native}";
        }
        else if (Asset(bgm.intro, package) is not RexAudioAsset intro || intro.Clip == null || Asset(bgm.loop, package) is not RexAudioAsset loop || loop.Clip == null)
            return "BGM intro/loop missing or unsupported";
        return null;
    }

    // 只查询原版 Addressables 地址是否存在，不加载资源本体。
    private static bool NativeAssetExists<T>(string key)
    {
        var handle = UnityEngine.AddressableAssets.Addressables.LoadResourceLocationsAsync((Il2CppSystem.Object)(Il2CppSystem.String)key, Il2CppType.Of<T>());
        var found = handle.WaitForCompletion().Cast<Il2CppSystem.Collections.Generic.ICollection<IResourceLocation>>().Count > 0;
        UnityEngine.AddressableAssets.Addressables.Release(handle);
        return found;
    }

    private static GameObject Child(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Color ToColor(float[] c) => c == null ? Color.white : new Color(c[0], c[1], c[2], c[3]);

    private static Il2CppStructArray<Vector2> Vectors(IEnumerable<float[]> points) => points.Select(p => new Vector2(p[0], p[1])).ToArray();

    private static void AddColliders(Transform parent, List<DayMapColliderConfig> colliders)
    {
        foreach (var c in colliders)
        {
            var go = Child(c.name ?? c.shape, parent);
            switch (c.shape)
            {
                case "polygon":
                    var polygon = go.AddComponent<PolygonCollider2D>();
                    polygon.pathCount = c.paths.Length;
                    for (int i = 0; i < c.paths.Length; i++) polygon.SetPath(i, Vectors(c.paths[i]));
                    break;
                case "edge":
                    go.AddComponent<EdgeCollider2D>().points = Vectors(c.closed ? c.points.Append(c.points[0]) : c.points);
                    break;
                case "box":
                    go.transform.localPosition = new Vector3(c.x, c.y, 0);
                    go.transform.localRotation = Quaternion.Euler(0, 0, c.rotation);
                    go.AddComponent<BoxCollider2D>().size = new Vector2(c.width, c.height);
                    break;
                case "circle":
                    go.transform.localPosition = new Vector3(c.x, c.y, 0);
                    go.AddComponent<CircleCollider2D>().radius = c.radius;
                    break;
            }
        }
    }

    private static GameObject Build(Entry entry)
    {
        var c = entry.Config;
        var root = new GameObject(entry.Label);
        root.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(root);
        var map = root.AddComponent<DaySceneMap>();
        entry.Map = map;
        map.spawnMarkerField = Child("SpawnMarkers", root.transform).transform;
        map.collectableField = Child("Collectables", root.transform).transform;
        map.shouldCameraFollow = c.camera.shouldFollow;
        map.cameraDefaultPosition = new Vector3(c.camera.position[0], c.camera.position[1], c.camera.position[2]);
        map.hideClock = c.hideClock;
        if (c.camera.shouldFollow)
        {
            var boundary = Child("CameraBounds", root.transform);
            boundary.layer = LayerMask.NameToLayer("Ignore Raycast");
            var polygon = boundary.AddComponent<PolygonCollider2D>();
            polygon.SetPath(0, Vectors(c.camera.bounds));
            map.boundingShape = polygon;
        }
        if (c.mapBGM.native == null)
        {
            map.mapBGM = ScriptableObject.CreateInstance<LoopedBGMPackage>();
            map.mapBGM.hideFlags = HideFlags.HideAndDontSave;
            map.mapBGM.intro = ((RexAudioAsset)Asset(c.mapBGM.intro, entry.Package)).Clip;
            map.mapBGM.loop = ((RexAudioAsset)Asset(c.mapBGM.loop, entry.Package)).Clip;
        }

        var layers = Child("Layers", root.transform).transform;
        foreach (var layer in c.layers)
            AddImage(entry, Child(layer.name ?? "Layer", layers).transform, layer.image, new Vector2(layer.x, layer.y), c.pixelsPerUnit,
                layer.sortingLayer, layer.sortingOrder, ToColor(layer.color));

        var prefabs = c.prefabs.ToDictionary(p => p.key, StringComparer.Ordinal);
        var objects = Child("Objects", root.transform).transform;
        foreach (var o in c.objects)
        {
            var prefab = prefabs[o.prefab];
            var go = Child(o.name ?? prefab.key, objects);
            go.transform.localPosition = new Vector3(o.x, o.y, 0);
            go.transform.localScale = new Vector3(o.scale[0], o.scale[1], 1);
            // 与原版预制体一致：图块作为一个 SortingGroup 整体排序；按 Y 排序时首帧写入 (int)(-32×Y) 后移除组件。
            var group = go.AddComponent<SortingGroup>();
            group.sortingLayerName = prefab.sortingLayer;
            group.sortingOrder = prefab.sortingOrder;
            if (prefab.sortByY) go.AddComponent<DEYU.Utils.LayerSortingController>().isStatic = true;
            var image = prefab.image;
            AddImage(entry, go.transform, image, new Vector2(-prefab.pivot[0] * image.width, -prefab.pivot[1] * image.height), prefab.pixelsPerUnit,
                prefab.sortingLayer, 0, Color.white);
            AddColliders(go.transform, prefab.colliders);
        }
        AddColliders(Child("Colliders", root.transform).transform, c.colliders);

        if (c.height?.cells.Count > 0)
        {
            var grid = Child("Grid", root.transform).AddComponent<Grid>();
            grid.cellSize = new Vector3(1, 1, 0);
            map.height = BuildHeight(c.height, grid.transform);
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

    // 每个图块一个渲染器；origin 为图片左下角相对父节点的像素坐标。
    private static void AddImage(Entry entry, Transform parent, DayMapImageConfig image, Vector2 origin, float ppu, string sortingLayer, int sortingOrder, Color color)
    {
        foreach (var chunk in image.chunks)
        {
            var extent = ChunkExtent(image, chunk);
            var go = Child($"{chunk[0]}_{chunk[1]}", parent);
            go.transform.localPosition = new Vector3((origin.x + chunk[0] * ChunkSize) / ppu, (origin.y + image.height - chunk[1] * ChunkSize - extent.y) / ppu, 0);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            renderer.color = color;
            entry.Slots.Add(new(renderer, ChunkPath(image, chunk), ppu));
        }
    }

    private static Texture2D LoadTexture(Entry entry, string path)
    {
        var asset = Asset(path, entry.Package);
        // 跨包引用的图片已由资源注册表解码，共享使用，不随地图释放。
        if (asset is RexImageAsset image) return image.Texture;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = path, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        if (!ImageConversion.LoadImage(texture, asset.Bytes, true)) Log.Error($"[{entry.Package}] failed to decode {path}");
        entry.Loaded.Add(texture);
        return texture;
    }

    private static T Own<T>(Entry entry, T obj) where T : UnityEngine.Object
    {
        obj.hideFlags = HideFlags.HideAndDontSave;
        entry.Loaded.Add(obj);
        return obj;
    }

    private static void AcquireArt(Entry entry)
    {
        var sprites = new Dictionary<(string, float), Sprite>();
        foreach (var slot in entry.Slots)
        {
            if (!sprites.TryGetValue((slot.Path, slot.PixelsPerUnit), out var sprite))
            {
                var texture = LoadTexture(entry, slot.Path);
                sprites[(slot.Path, slot.PixelsPerUnit)] = sprite = Own(entry, Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero, slot.PixelsPerUnit, 0, SpriteMeshType.FullRect));
            }
            slot.Renderer.sprite = sprite;
        }
        if (entry.Config.mapBGM.native is { } native)
        {
            var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<LoopedBGMPackage>((Il2CppSystem.Object)(Il2CppSystem.String)native);
            entry.Map.mapBGM = handle.WaitForCompletion();
            entry.ReleaseBgm = () => UnityEngine.AddressableAssets.Addressables.Release(handle);
        }
        Log.Info($"Loaded day map art {entry.Config.id}: {sprites.Count} chunk(s)");
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

    private static void ReleaseArt(Entry entry)
    {
        foreach (var slot in entry.Slots) slot.Renderer.sprite = null;
        foreach (var obj in entry.Loaded) UnityEngine.Object.Destroy(obj);
        entry.Loaded.Clear();
        if (entry.ReleaseBgm != null)
        {
            entry.Map.mapBGM = null;
            entry.ReleaseBgm();
            entry.ReleaseBgm = null;
        }
        Log.Info($"Released day map art {entry.Config.id}");
    }
}
