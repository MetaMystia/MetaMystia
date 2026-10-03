using System;
using System.Collections.Generic;
using System.Linq;

using Mystia.Assets;
using Mystia.Data;
using Mystia.Numerics;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 资源包白天地图：解析配置、校验、把配置拼成框架的 <see cref="DayMapSpec"/>，交给
/// <c>ICommonServices.MapBuilder</c> 构建（框架建 Tilemap／相机／高度图／刷新点，并把地图本体登记进游戏资产管线），
/// 之后由框架的 <c>TryPublish</c> 写游戏的地图引用表。模组侧不再自建 GameObject／组件，也不再碰注入管线。
/// 数据面（mapData／刷新点与采集点标签／地图语言／映射）仍由框架按 <c>OnInjectDayMaps</c> 写入。
/// </summary>
[AutoLog]
public static partial class DayMapRegistry
{
    private sealed record Entry(DayMapConfig Config, string Package, string Label, string Key)
    {
        public DayMapHandle Map { get; set; }
    }

    private static readonly Dictionary<int, Entry> Maps = new();
    private static readonly HashSet<int> Conflicts = new();

    public static IEnumerable<int> Ids => Maps.Where(p => p.Value.Map != null).Select(p => p.Key);
    public static bool IsRegistered(int id) => Maps.TryGetValue(id, out var entry) && entry.Map != null;
    public static string GetDisplayName(int id) => IsRegistered(id) ? Maps[id].Config.name : null;
    public static string GetLabel(int id) => Maps.TryGetValue(id, out var entry) ? entry.Label : $"_ResourceEx_Map_{id}";
    public static string GetMarker(int id, string name) => $"{GetLabel(id)}_{name}";

    public static bool TryGetId(string label, out int id)
    {
        foreach (var pair in Maps)
        {
            if (pair.Value.Map == null || pair.Value.Label != label) continue;
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

    /// <summary>
    /// 校验配置并交给框架构建地图；只有构建成功的地图才进入数据面。
    /// 由 <c>ModDatabaseExtension.OnInjectDayMaps</c> 在框架收集注入数据时调用：此时资源包已加载，
    /// 且早于 DataBaseDay.Initialize 的框架写入与随后的发布。
    /// </summary>
    public static void BuildAll()
    {
        if (Maps.Count == 0) return;
        if (ModRuntime.MapBuilder is not { } builder)
        {
            Log.Error("Day map builder unavailable; no resource pack map was built.");
            return;
        }

        foreach (var entry in Maps.Values)
        {
            if (entry.Map != null) continue;
            var error = Validate(entry.Config, entry.Package);
            if (error != null) { Log.Error($"[{entry.Package}] dayMaps[{entry.Config.id}]: {error}"); continue; }
            if (!TryAssemble(entry, builder, out var map)) continue;

            entry.Map = map;
            Log.Info($"Built day map {entry.Config.id}: {entry.Label}");
        }
    }

    /// <summary>已构建成功的地图配置；数据面只注入这些地图（校验失败、构建被拒或重复 id 的地图不写表）。</summary>
    internal static IEnumerable<DayMapConfig> BuiltConfigs =>
        Maps.Values.Where(entry => entry.Map != null).Select(entry => entry.Config);

    /// <summary>
    /// 把已构建的地图发布给游戏：框架把地图本体的地址引用写进 <c>DataBaseDay.mapReference</c>
    /// （换图时 <c>SpawnMapReferenceAsync</c> 从这里取）。该字典由 DataBaseDay.Initialize 重建，
    /// 因此时机与迁移前一致：DataBaseDay.Initialize 的后缀（<c>OnDataBaseDayInitialized</c>）。
    /// 重复发布是幂等的，地图像表被重建后再次调用即可继续可用。
    /// </summary>
    public static void RegisterMapReferences()
    {
        if (ModRuntime.MapBuilder is not { } builder)
        {
            Log.Error("Day map builder unavailable; no resource pack map was published.");
            return;
        }

        foreach (var entry in Maps.Values)
            if (entry.Map is { } map && !builder.TryPublish(map))
                Log.Error($"Day map {entry.Label} was not published; the map cannot be entered.");
    }

    public static bool TryGetDestination(int id, string marker, out string label, out string markerName)
    {
        label = markerName = null;
        if (!Maps.TryGetValue(id, out var entry) || entry.Map == null) return false;
        marker = string.IsNullOrEmpty(marker) ? entry.Config.defaultSpawnMarker : marker;
        if (!entry.Config.spawnMarkers.Any(m => m.name == marker)) return false;
        label = entry.Label;
        markerName = GetMarker(id, marker);
        // 发布状态即游戏地图引用表的状态：DataBaseDay.Initialize 后与白天场景唤醒时各发布一次，
        // 场景内调用本方法时该表就是已发布的样子。
        return entry.Map.IsPublished;
    }

    private static RexAsset Asset(string path, string package)
    {
        return RexAssetRegistry.TryResolveUri(path, package, out var uri) && RexAssetRegistry.Assets.TryGetValue(uri, out var asset) ? asset : null;
    }

    private static bool Numbers(float[] values, int count) => values != null && values.Length == count && values.All(float.IsFinite);
    private static bool Coordinate(float x, float y) => float.IsFinite(x) && float.IsFinite(y) && Math.Abs(x) <= 4096 && Math.Abs(y) <= 4096;

    // 排序层是否存在由框架在构建前判定（本构建的互操作里没有 SortingLayer.NameToID／Renderer.sortingLayerName 读取），
    // 这里只校验配置给了名字；名字不存在时构建被拒，地图同样不进入数据面。
    private static bool Sorting(string name) => !string.IsNullOrEmpty(name);

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
            if (r == null || r.Length != 4 || r[0] < 0 || r[1] < 0 || r[2] <= 0 || r[3] <= 0 || (long)r[0] + r[2] > image.Width || (long)r[1] + r[3] > image.Height) return $"tile rect out of bounds: {t.key}";
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

    /// <summary>
    /// 把配置拼成 <see cref="DayMapSpec"/> 并交给框架构建：每个图块按包内声明从图片贴图上切一张精灵，
    /// 层／摆件／碰撞盒／刷新点／高度图／BGM 都按值交给框架。
    /// </summary>
    private static bool TryAssemble(Entry entry, IDayMapBuilder builder, out DayMapHandle map)
    {
        map = null;
        var c = entry.Config;

        if (ModRuntime.Assets is not { } assets)
        {
            Log.Error($"[{entry.Package}] dayMaps[{c.id}]: asset factory unavailable");
            return false;
        }

        if (c.mapBGM == null || Asset(c.mapBGM.intro, entry.Package) is not RexAudioAsset intro || Asset(c.mapBGM.loop, entry.Package) is not RexAudioAsset loop
            || intro.Clip == null || loop.Clip == null)
        {
            Log.Error($"[{entry.Package}] dayMaps[{c.id}]: BGM intro/loop missing or unsupported");
            return false;
        }

        var tiles = new List<DayMapTile>(c.tiles.Count);
        foreach (var t in c.tiles)
        {
            if (Asset(t.image, entry.Package) is not RexImageAsset image)
            {
                Log.Error($"[{entry.Package}] dayMaps[{c.id}]: image missing: {t.image}");
                return false;
            }

            var r = t.rect;
            if (!assets.TryCreateSprite(
                    image.Texture,
                    new Rect(r[0], r[1], r[2], r[3]),
                    new Vector2(t.pivot[0], t.pivot[1]),
                    t.pixelsPerUnit,
                    out var sprite))
            {
                Log.Error($"[{entry.Package}] dayMaps[{c.id}]: tile rect refused by the asset factory: {t.key}");
                return false;
            }

            tiles.Add(new DayMapTile(t.key, sprite));
        }

        var layers = c.layers.Select(layer => new DayMapLayer
        {
            Name = layer.name,
            SortingLayer = layer.sortingLayer,
            SortingOrder = layer.sortingOrder,
            Cells = layer.cells.Select(cell => new DayMapCell(cell.x, cell.y, cell.tile)).ToList(),
        }).ToList();

        var props = c.objects.Select(o => new DayMapProp
        {
            Name = o.name,
            Tile = o.tile,
            Position = new Vector2(o.x, o.y),
            Scale = new Vector2(o.scale[0], o.scale[1]),
            SortByY = o.sortByY,
            SortingLayer = o.sortingLayer,
            SortingOrder = o.sortingOrder,
        }).ToList();

        var collisions = c.collisions
            .Select(box => new DayMapCollision(box.name, new Vector2(box.x, box.y), new Vector2(box.width, box.height)))
            .ToList();

        var markers = c.spawnMarkers
            .Select(marker => new DayMapSpawnMarker(marker.name, new Vector2(marker.x, marker.y), (CharacterRotationKind)(int)marker.rotation))
            .ToList();

        var height = c.height?.cells
            .Select(cell => new DayMapHeightCell(cell.x, cell.y, cell.slope))
            .ToList();

        var camera = new DayMapCamera
        {
            Follows = c.camera.shouldFollow,
            Position = new Vector3(c.camera.position[0], c.camera.position[1], c.camera.position[2]),
            // 配置给的是相机中心的矩形两个角，框架要的是「一个角 + 范围」。
            Bounds = c.camera.shouldFollow
                ? new Rect(c.camera.bounds[0], c.camera.bounds[1], c.camera.bounds[2] - c.camera.bounds[0], c.camera.bounds[3] - c.camera.bounds[1])
                : null,
        };

        var spec = new DayMapSpec
        {
            Key = entry.Key,
            Label = entry.Label,
            CellSize = new Vector2(1f, 1f),
            Camera = camera,
            Music = new DayMapMusic { Intro = intro.Clip, Loop = loop.Clip },
            Tiles = tiles,
            Layers = layers,
            Props = props,
            Collisions = collisions,
            SpawnMarkers = markers,
            DefaultSpawnMarker = c.defaultSpawnMarker,
            Height = height,
        };

        if (!builder.TryBuild(spec, out map))
        {
            Log.Error($"[{entry.Package}] dayMaps[{c.id}]: the framework refused the map {entry.Label} (see the host trace)");
            return false;
        }

        return true;
    }
}
