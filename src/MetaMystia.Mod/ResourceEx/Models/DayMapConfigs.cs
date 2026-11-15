using System.Collections.Generic;

using DayScene.Input;

namespace MetaMystia.ResourceEx.Models;

// 画布地图（formatVersion 3）。美术坐标为整数像素，世界坐标 = 像素 / pixelsPerUnit；Y 轴向上。
public class DayMapConfig
{
    public int id { get; set; }
    public int formatVersion { get; set; } = 3;
    public string name { get; set; }
    public string description { get; set; } = "";
    public int pixelsPerUnit { get; set; } = 48;
    public bool hideClock { get; set; }
    public List<DayMapLayerConfig> layers { get; set; } = new();
    public List<DayMapPrefabConfig> prefabs { get; set; } = new();
    public List<DayMapObjectConfig> objects { get; set; } = new();
    public List<DayMapColliderConfig> colliders { get; set; } = new();
    public DayMapHeightConfig height { get; set; }
    public List<DayMapSpawnConfig> spawnMarkers { get; set; } = new();
    public string defaultSpawnMarker { get; set; }
    public DayMapCameraConfig camera { get; set; } = new();
    public DayMapBgmConfig mapBGM { get; set; }
}

// 切块存放的大图：按 1024 像素切块，块 [列,行] 自图片左上角起算，文件为 <dir>/<列>_<行>.png，只列出非空块。
public class DayMapImageConfig
{
    public int width { get; set; }
    public int height { get; set; }
    public string dir { get; set; }
    public List<int[]> chunks { get; set; } = new();
}

// 一张大图，对应原版的一层 Tilemap。
public class DayMapLayerConfig
{
    public string name { get; set; }
    public string sortingLayer { get; set; } = "Background";
    public int sortingOrder { get; set; }
    public float[] color { get; set; }
    // 图片左下角的像素坐标。
    public int x { get; set; }
    public int y { get; set; }
    public DayMapImageConfig image { get; set; }
}

// 物件模板，对应原版带 SpriteRenderer 或 SortingGroup 的预制体；碰撞使用模板局部坐标。
public class DayMapPrefabConfig
{
    public string key { get; set; }
    public DayMapImageConfig image { get; set; }
    public float[] pivot { get; set; } = { 0.5f, 0f };
    public float pixelsPerUnit { get; set; } = 48;
    public string sortingLayer { get; set; } = "Character";
    public int sortingOrder { get; set; }
    public bool sortByY { get; set; } = true;
    public List<DayMapColliderConfig> colliders { get; set; } = new();
}

public class DayMapObjectConfig
{
    public string name { get; set; }
    public string prefab { get; set; }
    public float x { get; set; }
    public float y { get; set; }
    // 负值表示翻转，碰撞随之翻转。
    public float[] scale { get; set; } = { 1, 1 };
}

// shape：polygon（实心，可多路径，内层路径为洞）、edge（折线，closed 时首尾相连）、box（中心与尺寸，可旋转）、circle。
public class DayMapColliderConfig
{
    public string name { get; set; }
    public string shape { get; set; }
    public float[][][] paths { get; set; }
    public float[][] points { get; set; }
    public bool closed { get; set; }
    public float x { get; set; }
    public float y { get; set; }
    public float width { get; set; }
    public float height { get; set; }
    public float rotation { get; set; }
    public float radius { get; set; }
}

public class DayMapHeightConfig
{
    public List<DayMapHeightCellConfig> cells { get; set; } = new();
}

public class DayMapHeightCellConfig
{
    public int x { get; set; }
    public int y { get; set; }
    // 向右移动时增加的纵向位移比例，原游戏可表达范围为 [-1, 1]。
    public float slope { get; set; }
}

public class DayMapSpawnConfig
{
    public string name { get; set; }
    public float x { get; set; }
    public float y { get; set; }
    public DayScenePlayerInputGenerator.CharacterRotation rotation { get; set; } = DayScenePlayerInputGenerator.CharacterRotation.Down;
}

public class DayMapCameraConfig
{
    public bool shouldFollow { get; set; } = true;
    // 相机中心可移动范围的多边形顶点，不是美术外框。
    public float[][] bounds { get; set; }
    public float[] position { get; set; } = { 0, 0, -10 };
}

// 二选一：包内音频 intro/loop，或原版 LoopedBGMPackage 的 Addressables 地址 native。
public class DayMapBgmConfig
{
    public string intro { get; set; }
    public string loop { get; set; }
    public string native { get; set; }
}
