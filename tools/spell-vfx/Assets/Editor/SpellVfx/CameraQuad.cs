// 全屏遮罩层的数据载体。游戏 UI 是 Screen Space Overlay 画布，世界空间渲染器盖不住它，
// 运行时应读取这些 SpriteRenderer 的精灵图与颜色，转成 Overlay 画布上的 RawImage。
//
// SpriteRenderer 会用精灵图的贴图覆盖材质的 _MainTex，因此精灵图必须就是遮罩贴图本身。

using UnityEngine;

public static class CameraQuad
{
    public static SpriteRenderer Build(Transform parent, string name, Material mat, Sprite sprite,
        string sortingLayerName, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = mat;
        sr.sortingLayerID = ParticleBuilder.SortingLayerId(sortingLayerName);
        sr.sortingOrder = sortingOrder;
        sr.sprite = sprite;
        sr.color = Color.white;
        return sr;
    }
}
