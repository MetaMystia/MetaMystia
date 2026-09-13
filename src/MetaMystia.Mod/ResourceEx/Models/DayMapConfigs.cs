using System.Collections.Generic;

using DayScene.Input;

namespace MetaMystia.ResourceEx.Models;

public class DayMapConfig
{
    public int id { get; set; }
    public int formatVersion { get; set; } = 1;
    public string name { get; set; }
    public string description { get; set; } = "";
    public List<DayMapTileConfig> tiles { get; set; } = new();
    public List<DayMapLayerConfig> layers { get; set; } = new();
    public DayMapHeightConfig height { get; set; }
    public List<DayMapObjectConfig> objects { get; set; } = new();
    public List<DayMapCollisionConfig> collisions { get; set; } = new();
    public List<DayMapSpawnConfig> spawnMarkers { get; set; } = new();
    public string defaultSpawnMarker { get; set; }
    public DayMapCameraConfig camera { get; set; } = new();
    public DayMapBgmConfig mapBGM { get; set; }
}

public class DayMapTileConfig
{
    public string key { get; set; }
    public string image { get; set; }
    public int[] rect { get; set; }
    public float[] pivot { get; set; } = new float[] { 0, 0 };
    public float pixelsPerUnit { get; set; } = 48;
}

public class DayMapLayerConfig
{
    public string name { get; set; }
    public string sortingLayer { get; set; } = "Background";
    public int sortingOrder { get; set; } = -2000;
    public List<DayMapCellConfig> cells { get; set; } = new();
}

public class DayMapCellConfig
{
    public int x { get; set; }
    public int y { get; set; }
    public string tile { get; set; }
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

public class DayMapObjectConfig
{
    public string name { get; set; }
    public string tile { get; set; }
    public float x { get; set; }
    public float y { get; set; }
    public float[] scale { get; set; } = new float[] { 1, 1 };
    public bool sortByY { get; set; } = true;
    public string sortingLayer { get; set; } = "Character";
    public int sortingOrder { get; set; }
}

public class DayMapCollisionConfig
{
    public string name { get; set; }
    public float x { get; set; }
    public float y { get; set; }
    public float width { get; set; }
    public float height { get; set; }
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
    // 原生相机中心范围，不是美术外框。
    public float[] bounds { get; set; }
    public float[] position { get; set; } = new float[] { 0, 0, -10 };
}

public class DayMapBgmConfig
{
    public string intro { get; set; }
    public string loop { get; set; }
}
