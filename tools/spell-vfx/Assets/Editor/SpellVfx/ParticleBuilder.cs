// 用代码构建粒子 prefab 的通用工具。prefab 由代码生成而非手工编辑，
// 效果可复现，改动可以在 diff 中审阅。
//
// 游戏主相机是沿 Z 轴观察的正交相机，Z 方向的运动在屏幕上不可见：
// 所有发射形状都要让粒子在 XY 平面内运动。

using UnityEngine;

public static class ParticleBuilder
{
    /// <summary>
    /// 按名称取排序层 ID。Unity 找不到层名时会静默回退到 Default，这里改为直接报错；
    /// 层名与 ID 以工程 ProjectSettings/TagManager.asset 为准，须与游戏一致。
    /// </summary>
    public static int SortingLayerId(string name)
    {
        var id = SortingLayer.NameToID(name);
        if (!SortingLayer.IsValid(id) || (id == 0 && name != "Default"))
            throw new System.ArgumentException($"sorting layer not found in TagManager: {name}");
        return id;
    }

    /// <summary>添加带 ParticleSystem 的子对象，渲染器使用 <paramref name="mat"/>；各模块由调用方继续配置。</summary>
    public static ParticleSystem AddEmitter(Transform parent, string name, Material mat,
        string sortingLayer, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.sortingLayerID = SortingLayerId(sortingLayer);
        r.sortingOrder = sortingOrder;
        r.renderMode = ParticleSystemRenderMode.Billboard;

        var main = ps.main;
        // 运行时只 Instantiate，不调用 Play()；playOnAwake 为 false 时实例会一直静止。
        main.playOnAwake = true;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 1f;
        main.startSpeed = 0f;
        main.startSize = 1f;
        main.stopAction = ParticleSystemStopAction.None;

        // 新建的 ParticleSystem 自带默认发射，先清空，由各发射器自行设置。
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[0]);

        // 默认 Cone 沿局部 Z 发射，在游戏画面中看不见运动；改为朝屏幕上方。
        var shape = ps.shape;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        return ps;
    }

    // ParticleSystem 的各模块按值返回结构体，须先取到局部变量再修改。

    public static void SetLifetime(ParticleSystem ps, float min, float max)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(min, max);
    }

    public static void SetSize(ParticleSystem ps, float min, float max)
    {
        var main = ps.main;
        main.startSize = new ParticleSystem.MinMaxCurve(min, max);
    }

    public static void SetSpeed(ParticleSystem ps, float min, float max)
    {
        var main = ps.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(min, max);
    }

    public static void SetColor(ParticleSystem ps, Color a)
    {
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(a);
    }

    public static void SetColor(ParticleSystem ps, Color a, Color b)
    {
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
    }

    public static void SetGravity(ParticleSystem ps, float gravity)
    {
        var main = ps.main;
        main.gravityModifier = gravity;
    }

    public static void SetSimulationSpace(ParticleSystem ps, ParticleSystemSimulationSpace space)
    {
        var main = ps.main;
        main.simulationSpace = space;
    }

    public static void SetBurst(ParticleSystem ps, float time, int count)
    {
        var e = ps.emission;
        e.enabled = true;
        e.rateOverTime = 0f;
        e.SetBursts(new[] { new ParticleSystem.Burst(time, (short)Mathf.Clamp(count, 0, 32767)) });
    }

    public static void SetRate(ParticleSystem ps, float rate)
    {
        var e = ps.emission;
        e.enabled = true;
        e.rateOverTime = rate;
        e.SetBursts(new ParticleSystem.Burst[0]);
    }

    /// <summary>粒子在生命周期内的透明度渐变；fadeIn 为 true 时先淡入。</summary>
    public static void SetFade(ParticleSystem ps, bool fadeIn = false)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            fadeIn
                ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.35f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);
    }

    public static void SetSizeCurve(ParticleSystem ps, params (float, float)[] keys)
    {
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, Curve(keys));
    }

    public static void SetRotation(ParticleSystem ps, float minDegPerSec, float maxDegPerSec)
    {
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(minDegPerSec * Mathf.Deg2Rad, maxDegPerSec * Mathf.Deg2Rad);
    }

    public static void SetShapeCircle(ParticleSystem ps, float radius, float arcDegrees = 360f, bool ring = false)
    {
        var s = ps.shape;
        s.enabled = true;
        s.shapeType = ParticleSystemShapeType.Circle;
        s.radius = radius;
        s.radiusThickness = ring ? 0f : 1f;
        s.arc = arcDegrees;
        // 不旋转时圆位于屏幕 XY 平面，并在平面内径向发射；旋转 90° 会被压成一条线。
        s.rotation = Vector3.zero;
    }

    /// <summary>
    /// 屏幕上宽高为 <paramref name="size"/> 的发射盒，朝上（或朝下）发射。
    /// Box 沿局部 Z 发射，因此旋转到 Y 方向，并把厚度压到 Z 方向。
    /// </summary>
    public static void SetShapeBox(ParticleSystem ps, Vector2 size, bool downward = false, float offsetY = 0f)
    {
        var s = ps.shape;
        s.enabled = true;
        s.shapeType = ParticleSystemShapeType.Box;
        s.rotation = new Vector3(downward ? 90f : -90f, 0f, 0f);
        s.scale = new Vector3(size.x, 0f, size.y);
        s.position = new Vector3(0f, offsetY, 0f);
    }

    public static void SetRadialVelocity(ParticleSystem ps, float radial, float tangential = 0f)
    {
        var v = ps.velocityOverLifetime;
        v.enabled = true;
        v.space = ParticleSystemSimulationSpace.Local;
        v.radial = new ParticleSystem.MinMaxCurve(radial);
        v.orbitalZ = new ParticleSystem.MinMaxCurve(tangential);
    }

    public static void SetNoise(ParticleSystem ps, float strength, float frequency, float scroll)
    {
        var n = ps.noise;
        n.enabled = true;
        n.strength = new ParticleSystem.MinMaxCurve(strength);
        n.frequency = frequency;
        n.scrollSpeed = new ParticleSystem.MinMaxCurve(scroll);
    }

    public static AnimationCurve Curve(params (float time, float value)[] keys)
    {
        var curve = new AnimationCurve();
        foreach (var key in keys) curve.AddKey(key.Item1, key.Item2);
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
        return curve;
    }
}
