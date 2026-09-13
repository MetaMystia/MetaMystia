using MetaMystia.ConsoleSystem;
using MetaMystia.ResourceEx.Registries;
using UnityEngine.Tilemaps;

// 适用已安装 IsolatedMapTest.zip 的单机白天；每次动作后单独读取 Snapshot 核验完成。
public static class DayMapTest
{
    public static string Command(string command)
    {
        var messages = new System.Collections.Generic.List<string>();
        CommandRegistry.Execute("resourceex map " + command, new ConsoleContext(messages.Add));
        return string.Join("\n", messages);
    }

    public static string Snapshot()
    {
        var sm = DayScene.SceneManager.Instance;
        var output = new StringBuilder();
        output.AppendLine("scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " ids=" + string.Join(",", DayMapRegistry.Ids));
        if (sm == null || sm.CurrentActiveMap == null) return output.ToString();
        var map = sm.CurrentActiveMap;
        output.AppendLine($"map={sm.CurrentActiveMapLabel} swapping={sm.IsMapSwapping} pos={sm.Character?.transform.position} actions={GameData.RunTime.DaySceneUtility.RunTimeDayScene.RemainActions}");
        output.AppendLine($"active={map.gameObject.activeSelf} height={map.height != null} bgm={map.mapBGM.Valid} input={Common.UI.UniversalGameManager.IsInputEnabled}");
        if (!sm.CurrentActiveMapLabel.StartsWith("_ResourceEx_Map_")) return output.ToString();
        foreach (var tm in map.GetComponentsInChildren<Tilemap>())
        {
            int count = 0;
            var bounds = tm.cellBounds;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                    if (tm.HasTile(new Vector3Int(x, y, 0))) count++;
            var renderer = tm.GetComponent<TilemapRenderer>();
            output.AppendLine($"layer={tm.name} cells={count} sorting={renderer?.sortingLayerName}/{renderer?.sortingOrder}");
        }
        output.AppendLine("boxes=" + map.GetComponentsInChildren<BoxCollider2D>().Length + " markers=" + map.AllSpawnMarkers.Count);
        var tree = map.transform.Find("Willow").GetComponent<SpriteRenderer>();
        var group = sm.Character.GetComponent<UnityEngine.Rendering.SortingGroup>();
        output.AppendLine($"playerOrder={group.sortingOrder} treeOrder={tree.sortingOrder} camera={Camera.main.transform.position}");
        return output.ToString();
    }
}

DayMapTest.Snapshot()
