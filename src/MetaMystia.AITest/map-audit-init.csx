using System.Collections;

using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.Tilemaps;

using DayScene;
using DayScene.Interactables;
using GameData.Core.Collections.DaySceneUtility;
using GameData.CoreLanguage;
using GameData.CoreLanguage.Collections;
using GameData.Profile;

using MetaMystia.ResourceEx.Addressables;

public class MapAuditProvider : ResourceProviderBase
{
    public const string Id = "MetaMystia.AITest.MapAuditProvider";
    public static GameObject Asset;
    public MapAuditProvider(IntPtr ptr) : base(ptr) { }
    public MapAuditProvider() : base(ClassInjector.DerivedConstructorPointer<MapAuditProvider>())
    {
        ClassInjector.DerivedConstructorBody(this);
        m_ProviderId = Id;
    }
    public override string ProviderId => Id;
    public override Il2CppSystem.Type GetDefaultType(IResourceLocation location) => Il2CppType.Of<GameObject>();
    public override bool CanProvide(Il2CppSystem.Type type, IResourceLocation location) => location != null && location.ProviderId == Id && Asset != null;
    public override void Provide(ProvideHandle handle) => handle.Complete<GameObject>(Asset, Asset != null, (Il2CppSystem.Exception)null);
    public override void Release(IResourceLocation location, Il2CppSystem.Object asset) { }
}

public static class MapAudit
{
    public const string Label = "_MapAudit_Sanzu";
    public const string Entry = "_MapAudit_Sanzu_Entry";
    public const string Key = "rex://map-audit/maps/sanzu";
    public static string ReturnLabel;
    public static string ReturnMarker;
    public static Vector3 ReturnPosition;
    public static GameObject Root;
    public static string Status = "idle";
    public static readonly System.Collections.Generic.List<UnityEngine.Object> Assets = new();

    static GameObject Child(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static Tile MakeTile(string name, Color color)
    {
        var tex = new Texture2D(48, 48, TextureFormat.RGBA32, false);
        tex.name = name;
        tex.filterMode = FilterMode.Point;
        var pixels = new Color[48 * 48];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, 48, 48), Vector2.zero, 48);
        sprite.name = name;
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None;
        Assets.Add(tex);
        Assets.Add(sprite);
        Assets.Add(tile);
        return tile;
    }

    static Tilemap Layer(string name, Transform grid, string sorting, int order)
    {
        var go = Child(name, grid);
        var tm = go.AddComponent<Tilemap>();
        tm.tileAnchor = Vector3.zero;
        var renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingLayerName = sorting;
        renderer.sortingOrder = order;
        return tm;
    }

    static void Wall(string name, Vector2 position, Vector2 size)
    {
        var go = Child(name, Root.transform);
        go.transform.localPosition = new Vector3(position.x, position.y, 0);
        go.AddComponent<BoxCollider2D>().size = size;
    }

    public static string Build()
    {
        var sm = DayScene.SceneManager.Instance;
        if (Root != null || sm == null || sm.CurrentActiveMap == null || sm.IsMapSwapping || DataBaseDay.IsMapNodePresent(Label)) return "unavailable";
        ReturnLabel = sm.CurrentActiveMapLabel;
        ReturnPosition = sm.Character.transform.position;
        foreach (var marker in sm.CurrentActiveMap.AllSpawnMarkers) { ReturnMarker = marker.Key; break; }
        Root = new GameObject(Label);
        Root.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(Root);
        var map = Root.AddComponent<DaySceneMap>();
        map.mapBGM = sm.CurrentActiveMap.mapBGM;
        map.shouldCameraFollow = true;
        map.spawnMarkerField = Child("Spawns", Root.transform).transform;
        map.collectableField = Child("Collectables", Root.transform).transform;
        var markerGo = Child(Entry, map.spawnMarkerField);
        markerGo.transform.localPosition = new Vector3(-6, 0, 0);
        var spawn = markerGo.AddComponent<SpawnMarker>();
        spawn.spawnMarkerName = Entry;
        var grid = Child("Grid", Root.transform).AddComponent<Grid>();
        grid.cellSize = new Vector3(1, 1, 0);
        var floor = Layer("Ground", grid.transform, "Background", -2000);
        var bank = MakeTile("Bank", new Color(0.28f, 0.32f, 0.36f));
        var river = MakeTile("River", new Color(0.13f, 0.28f, 0.42f));
        var bridge = MakeTile("Bridge", new Color(0.60f, 0.46f, 0.32f));
        for (int y = -10; y < 10; y++)
            for (int x = -18; x < 18; x++)
                floor.SetTile(new Vector3Int(x, y, 0), x >= 0 && x < 4 ? (y >= -1 && y < 1 ? bridge : river) : bank);
        var canopy = Layer("Overhead", grid.transform, "Overlay", 5);
        canopy.SetTile(new Vector3Int(-6, 3, 0), MakeTile("Canopy", new Color(0.65f, 0.22f, 0.36f)));
        var prop = Child("DepthProp", Root.transform);
        prop.transform.localPosition = new Vector3(-9, 0, 0);
        var propRenderer = prop.AddComponent<SpriteRenderer>();
        propRenderer.sprite = MakeTile("DepthProp", new Color(0.85f, 0.7f, 0.35f)).sprite;
        prop.transform.localScale = new Vector3(2, 3, 1);
        propRenderer.sortingLayerName = "Character";
        prop.AddComponent<DEYU.Utils.LayerSortingController>();
        Wall("RiverNorth", new Vector2(2, 5.5f), new Vector2(4, 9));
        Wall("RiverSouth", new Vector2(2, -5.5f), new Vector2(4, 9));
        Wall("North", new Vector2(0, 10), new Vector2(36, 1));
        Wall("South", new Vector2(0, -10), new Vector2(36, 1));
        Wall("West", new Vector2(-18, 0), new Vector2(1, 20));
        Wall("East", new Vector2(18, 0), new Vector2(1, 20));
        var cameraGo = Child("CameraBounds", Root.transform);
        cameraGo.layer = LayerMask.NameToLayer("Ignore Raycast");
        var cameraBounds = cameraGo.AddComponent<PolygonCollider2D>();
        cameraBounds.SetPath(0, new Vector2[] { new(-18, -10), new(-18, 10), new(18, 10), new(18, -10) });
        map.boundingShape = cameraBounds;
        ClassInjector.RegisterTypeInIl2Cpp<MapAuditProvider>();
        RuntimeAddressables.RegisterProviderType<GameObject>(new MapAuditProvider().Cast<IResourceProvider>(), MapAuditProvider.Id, (key, asset) => MapAuditProvider.Asset = asset,
            key => { MapAuditProvider.Asset = null; return true; }, key => MapAuditProvider.Asset != null, key => MapAuditProvider.Asset);
        var reference = RuntimeAddressables.Register<GameObject>(Key, Root);
        DataBaseDay.mapReference[Label] = reference;
        DataBaseDay.mapData[Label] = new DaySceneMapProfile.MapNode
        {
            mapName = Label, parent = "", mapAssetReference = reference,
            mapCollectableLabels = new Il2CppStringArray(0),
            mapSpawnMarkerLabels = new Il2CppStringArray(new[] { Entry }),
            level1IzakayaId = new Il2CppStructArray<int>(0),
            level2IzakayaId = new Il2CppStructArray<int>(0),
            level3IzakayaId = new Il2CppStructArray<int>(0)
        };
        DataBaseDay.allCollectablesLabels[Label] = new Il2CppSystem.Collections.Generic.HashSet<string>();
        var markers = new Il2CppSystem.Collections.Generic.HashSet<string>();
        markers.Add(Entry);
        DataBaseDay.allSpawnMarkerLabels[Label] = markers;
        DaySceneLanguage.MapLanguageData[Label] = new LanguageBase("三途川·结构试验", "临时瓦片、碰撞与图层测试");
        return Status = "built; original=" + ReturnLabel + "/" + ReturnMarker;
    }

    public static IEnumerator Preflight()
    {
        Status = "loading via SpawnMapReferenceAsync";
        var awaiter = DataBaseDay.SpawnMapReferenceAsync(Label).GetAwaiter();
        while (!awaiter.IsCompleted) yield return null;
        var result = awaiter.GetResult();
        var map = result.Item2;
        map.PreInitialize(Label, result.Item1);
        Status = $"preflight passed: type={map.GetIl2CppType().FullName} markers={map.AllSpawnMarkers.Count} handle={map._Handle != null} tiles={map.GetComponentsInChildren<Tilemap>(true).Length}";
        UnityEngine.Object.Destroy(map.gameObject);
    }

    public static void Enter() => DayScene.SceneManager.Instance.SwapMap(Label, Entry, 0, true, true, false, null);
    public static void Leave() => DayScene.SceneManager.Instance.SwapMap(ReturnLabel, ReturnMarker, 0, true, true, false, null);

    public static string Cleanup()
    {
        var sm = DayScene.SceneManager.Instance;
        if (sm.IsMapSwapping || sm.CurrentActiveMapLabel != ReturnLabel) return "return to original map first";
        sm.Character.transform.position = ReturnPosition;
        DataBaseDay.mapData.Remove(Label);
        DataBaseDay.mapReference.Remove(Label);
        DataBaseDay.allCollectablesLabels.Remove(Label);
        DataBaseDay.allSpawnMarkerLabels.Remove(Label);
        DataBaseDay.additiveMapData.Remove(Label);
        GameData.RunTime.DaySceneUtility.RunTimeDayScene.trackedNPCs.Remove(Label);
        GameData.RunTime.DaySceneUtility.RunTimeDayScene.trackedMaps.Remove(Label);
        DaySceneLanguage.MapLanguageData.Remove(Label);
        RuntimeAddressables.Unregister<GameObject>(Key);
        MapAuditProvider.Asset = null;
        UnityEngine.Object.Destroy(Root);
        foreach (var asset in Assets) UnityEngine.Object.Destroy(asset);
        Assets.Clear();
        return Status = "cleaned";
    }
}

MapAudit.Status
