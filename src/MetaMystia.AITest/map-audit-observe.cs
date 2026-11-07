using System.Text;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

using DayScene;
using DayScene.Interactables.Collections.ConditionComponents;

public static class Payload
{
    public static object Execute()
    {
        var s = new StringBuilder();
        s.AppendLine($"unity={Application.unityVersion} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        var sm = DayScene.SceneManager.Instance;
        if (sm == null || sm.CurrentActiveMap == null) return s.ToString();
        var map = sm.CurrentActiveMap;
        s.AppendLine($"map={sm.CurrentActiveMapLabel} type={map.GetIl2CppType().FullName} swapping={sm.IsMapSwapping} player={sm.Character.transform.position} actions={GameData.RunTime.DaySceneUtility.RunTimeDayScene.RemainActions}");
        s.AppendLine($"root={map.transform.position} scale={map.transform.lossyScale} follow={map.shouldCameraFollow} height={map.height?.name} boundary={map.boundingShape?.name}");
        foreach (var cam in Camera.allCameras)
            s.AppendLine($"camera={cam.name} sort={cam.transparencySortMode} axis={cam.transparencySortAxis} ortho={cam.orthographicSize}");
        foreach (var grid in map.GetComponentsInChildren<Grid>(true))
            s.AppendLine($"grid={grid.name} cell={grid.cellSize} gap={grid.cellGap} layout={grid.cellLayout} swizzle={grid.cellSwizzle}");
        foreach (var tm in map.GetComponentsInChildren<Tilemap>(true))
        {
            var r = tm.GetComponent<TilemapRenderer>();
            s.AppendLine($"tilemap={tm.name} bounds={tm.cellBounds} anchor={tm.tileAnchor} color={tm.color} active={tm.gameObject.activeInHierarchy} layer={tm.gameObject.layer} renderer={r?.enabled} sorting={r?.sortingLayerName}/{r?.sortingOrder} mode={r?.mode} material={r?.sharedMaterial?.name} shader={r?.sharedMaterial?.shader?.name}");
            int count = 0;
            int samples = 0;
            var b = tm.cellBounds;
            for (int y = b.yMin; y < b.yMax; y++)
                for (int x = b.xMin; x < b.xMax; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    if (!tm.HasTile(cell)) continue;
                    count++;
                    if (samples++ >= 2) continue;
                    var sprite = tm.GetSprite(cell);
                    s.AppendLine($"  sample={cell} tile={tm.GetTile(cell)?.GetIl2CppType().FullName} sprite={sprite?.name} rect={sprite?.rect} ppu={sprite?.pixelsPerUnit} pivot={sprite?.pivot} readable={sprite?.texture?.isReadable} collider={tm.GetColliderType(cell)} matrix={tm.GetTransformMatrix(cell)}");
                }
            s.AppendLine($"  occupied={count}");
        }
        foreach (var c in map.GetComponentsInChildren<Collider2D>(true))
        {
            s.AppendLine($"collider={c.name} type={c.GetIl2CppType().Name} layer={c.gameObject.layer}/{LayerMask.LayerToName(c.gameObject.layer)} enabled={c.enabled} active={c.gameObject.activeInHierarchy} trigger={c.isTrigger} offset={c.offset} bounds={c.bounds} composite={c.usedByComposite}");
            var poly = c.TryCast<PolygonCollider2D>();
            if (poly != null) s.AppendLine($"  paths={poly.pathCount} points={poly.GetTotalPointCount()}");
            var composite = c.TryCast<CompositeCollider2D>();
            if (composite != null) s.AppendLine($"  paths={composite.pathCount} points={composite.pointCount} geometry={composite.geometryType}");
        }
        foreach (var sg in sm.Character.GetComponentsInChildren<SortingGroup>(true))
            s.AppendLine($"playerSorting={sg.name} layer={sg.sortingLayerName} order={sg.sortingOrder}");
        foreach (var exit in map.GetComponentsInChildren<MapTransitionConditionComponent>(true))
            s.AppendLine($"exit={exit.name} target={exit.transitionData.targetSceneLabel}/{exit.transitionData.targetSceneSpawnMarker} cost={exit.transitionData.shouldCostAction}");
        foreach (var dec in map.GetComponentsInChildren<DEYU.BinaryTilemap.BinaryTilemapDecompressor>(true))
            s.AppendLine($"decompressor={dec.name} awake={dec.m_DecompressOnAwake} maps={dec.m_TilemapList?.Length} profile={dec.m_BinaryTilemapProfile?.name}");
        return s.ToString();
    }
}
