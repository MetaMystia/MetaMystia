// 示例特效集合：舞（11001）的符卡特效。运行时由 Mod 按 prefab 名称加载。
//
// 约定：所有发射器使用 World 模拟空间；全屏遮罩由 CameraQuad 生成，运行时转成 Overlay 画布。

using System.Collections.Generic;
using UnityEngine;

public sealed class MaiVfxSet : ISpellVfxSet
{
    public string Name => "Mai";
    public string BundleName => "maispell";

    // 只用冷白与冰蓝，与店内暖色灯光区分。
    static readonly Color IceWhite = new Color(0.96f, 0.99f, 1f, 1f);
    static readonly Color IcePale = new Color(0.80f, 0.91f, 1f, 1f);
    static readonly Color IceBlue = new Color(0.55f, 0.79f, 1f, 1f);
    static readonly Color FrostHaze = new Color(0.72f, 0.87f, 1f, 0.40f);

    SpellAssetFactory _assets;

    public IEnumerable<GameObject> BuildPrefabs(SpellAssetFactory assets)
    {
        _assets = assets;
        return new[] { Cast(), IceShard(), Snowfall(), BevTrail(), FrostField(), CoolDown(), SteamBurst() };
    }

    #region red card

    /// <summary>Casting: a magic circle unfolding under Mai, with a wingbeat of feathers.</summary>
    GameObject Cast()
    {
        var root = new GameObject("Mai_Cast");

        var circle = Add(root, "ps_circle",
            _assets.AdditiveMat("M_Circle", "magic_circle", IceBlue, 0.9f), "EffectOverlay", 100);
        SetLifetime(circle, 2.2f, 2.2f);
        SetSize(circle, 1.2f, 5.4f);
        SetBurst(circle, 0f, 1);
        SetFade(circle, true);
        SetSizeCurve(circle, (0f, 0.35f), (0.35f, 1f), (1f, 1.06f));
        SetRotation(circle, 12f, 24f);

        var ring = Add(root, "ps_ring",
            _assets.AdditiveMat("M_Ring", "ring", IcePale, 0.85f), "EffectOverlay", 90);
        SetLifetime(ring, 0.9f, 0.9f);
        SetSize(ring, 0.4f, 0.5f);
        SetBurst(ring, 0.05f, 1);
        SetFade(ring);
        SetSizeCurve(ring, (0f, 0.2f), (1f, 2.6f));
        SetShapeCircle(ring, 0.1f);

        var snow = Add(root, "ps_snow",
            _assets.AlphaMat("M_Snowflake", "snowflake", IceWhite), "Overlay", 40);
        SetLifetime(snow, 1.1f, 1.6f);
        SetSize(snow, 0.08f, 0.2f);
        SetSpeed(snow, 0.8f, 2.2f);
        SetGravity(snow, 0.22f);
        SetBurst(snow, 0.6f, 34);
        SetFade(snow);
        SetRotation(snow, -140f, 140f);
        SetShapeBox(snow, new Vector2(5.5f, 0.4f));

        var feathers = Add(root, "ps_feather",
            _assets.AlphaMat("M_Feather", "feather", IceWhite), "Overlay", 45);
        SetLifetime(feathers, 1.6f, 2.4f);
        SetSize(feathers, 0.12f, 0.26f);
        SetSpeed(feathers, 1.4f, 3.2f);
        SetGravity(feathers, 0.16f);
        SetBurst(feathers, 0.55f, 22);
        SetFade(feathers);
        SetRotation(feathers, -90f, 90f);
        SetRadialVelocity(feathers, 1.1f, 0.5f);
        SetShapeCircle(feathers, 0.35f);

        var glow = Add(root, "ps_glow",
            _assets.AdditiveMat("M_Glow", "soft_dot", IceBlue, 0.7f), "EffectOverlay", 85);
        SetLifetime(glow, 0.9f, 1.3f);
        SetSize(glow, 0.8f, 2.6f);
        SetBurst(glow, 0f, 1);
        SetFade(glow, true);
        SetSizeCurve(glow, (0f, 0.3f), (0.4f, 1f), (1f, 1.3f));

        return root;
    }

    /// <summary>Per-drink shard: a chip of ice and a sparkle where the glass lands.</summary>
    GameObject IceShard()
    {
        var root = new GameObject("Mai_IceShard");

        var shard = Add(root, "ps_shard",
            _assets.AlphaMat("M_Shard", "crystal_shard", IceWhite), "Overlay", 60);
        SetLifetime(shard, 0.55f, 0.8f);
        SetSize(shard, 0.5f, 0.95f);
        SetSpeed(shard, 0.6f, 1.6f);
        SetGravity(shard, 0.5f);
        SetBurst(shard, 0f, 1);
        SetFade(shard);
        SetRotation(shard, -220f, 220f);
        SetShapeCircle(shard, 0.06f);

        var sparkle = Add(root, "ps_sparkle",
            _assets.AdditiveMat("M_Sparkle", "sparkle", IceWhite, 1.1f), "Overlay", 62);
        SetLifetime(sparkle, 0.35f, 0.6f);
        SetSize(sparkle, 0.18f, 0.42f);
        SetSpeed(sparkle, 0.4f, 1.2f);
        SetBurst(sparkle, 0f, 5);
        SetFade(sparkle);
        SetSizeCurve(sparkle, (0f, 0.15f), (0.3f, 1f), (1f, 0.2f));
        SetShapeCircle(sparkle, 0.05f);

        var flash = Add(root, "ps_flash",
            _assets.AdditiveMat("M_Flash", "ring", IcePale, 0.8f), "Overlay", 58);
        SetLifetime(flash, 0.32f, 0.32f);
        SetSize(flash, 0.25f, 0.3f);
        SetBurst(flash, 0f, 1);
        SetFade(flash);
        SetSizeCurve(flash, (0f, 0.3f), (1f, 2.2f));

        return root;
    }

    /// <summary>Cold trail that follows the thrown drink to the table.</summary>
    GameObject BevTrail()
    {
        var root = new GameObject("Mai_BevTrail");

        var mist = Add(root, "ps_mist",
            _assets.AlphaMat("M_Mist", "mist", FrostHaze), "Overlay", 30);
        SetLifetime(mist, 0.45f, 0.8f);
        SetSize(mist, 0.5f, 1.1f);
        SetSpeed(mist, 0.15f, 0.5f);
        SetRate(mist, 26f);
        SetFade(mist);
        SetSizeCurve(mist, (0f, 0.5f), (1f, 1.5f));
        SetNoise(mist, 0.3f, 0.6f, 0.4f);

        var flecks = Add(root, "ps_fleck",
            _assets.AdditiveMat("M_Fleck", "snow_dot", IceWhite, 1f), "Overlay", 55);
        SetLifetime(flecks, 0.3f, 0.55f);
        SetSize(flecks, 0.06f, 0.16f);
        SetSpeed(flecks, 0.1f, 0.4f);
        SetRate(flecks, 34f);
        SetFade(flecks);
        SetShapeCircle(flecks, 0.12f);

        return root;
    }

    /// <summary>Snow drifting across the room for the duration of the reward.</summary>
    GameObject Snowfall()
    {
        var root = new GameObject("Mai_Snowfall");

        var snow = Add(root, "ps_snow",
            _assets.AlphaMat("M_Snow", "snowflake", IceWhite), "Overlay", 20);
        SetLifetime(snow, 6f, 9f);
        SetSize(snow, 0.07f, 0.17f);
        SetSpeed(snow, 0.25f, 0.7f);
        SetGravity(snow, 0.06f);
        SetRate(snow, 14f);
        SetFade(snow, true);
        SetRotation(snow, -60f, 60f);
        SetShapeBox(snow, new Vector2(28f, 0.2f), downward: true, offsetY: 8f);
        SetNoise(snow, 0.9f, 0.22f, 0.25f);

        return root;
    }

    #endregion

    #region black card

    /// <summary>Screen frost creeping in from the edges, plus a cold tint over the frame.</summary>
    GameObject FrostField()
    {
        var root = new GameObject("Mai_FrostField");

        CameraQuad.Build(root.transform, "frost",
            _assets.AlphaMat("M_Frost", "frost_vignette", Color.white), _assets.SpriteOf("frost_vignette"), "UI", 200);

        // A whisper of blue over the whole frame; the vignette carries the shape.
        CameraQuad.Build(root.transform, "tint",
            _assets.AlphaMat("M_Tint", "white", new Color(0.42f, 0.70f, 1f, 0.07f)), _assets.SpriteOf("white"), "UI", 190);

        return root;
    }

    /// <summary>Breath fog that puffs up from the guests while the room stays cold.</summary>
    GameObject CoolDown()
    {
        var root = new GameObject("Mai_CoolDown");

        var chill = Add(root, "ps_chill",
            _assets.AlphaMat("M_Chill", "mist", FrostHaze), "Overlay", 35);
        SetLifetime(chill, 0.8f, 1.4f);
        SetSize(chill, 0.35f, 0.85f);
        SetSpeed(chill, 0.12f, 0.4f);
        SetRate(chill, 3.5f);
        SetFade(chill, true);
        SetSizeCurve(chill, (0f, 0.4f), (1f, 1.6f));
        SetShapeBox(chill, new Vector2(5f, 0.3f));
        SetNoise(chill, 0.25f, 0.5f, 0.2f);

        var breath = Add(root, "ps_breath",
            _assets.AdditiveMat("M_Breath", "soft_dot", IcePale, 0.30f), "Overlay", 38);
        SetLifetime(breath, 0.9f, 1.5f);
        SetSize(breath, 0.12f, 0.3f);
        SetSpeed(breath, 0.25f, 0.7f);
        SetRate(breath, 5f);
        SetFade(breath, true);
        SetSizeCurve(breath, (0f, 0.3f), (0.4f, 1f), (1f, 1.4f));
        SetShapeBox(breath, new Vector2(5f, 0.2f));

        return root;
    }

    /// <summary>Steam when Mai's frost cuts off Yuki's fire — the "betrayal" beat.</summary>
    GameObject SteamBurst()
    {
        var root = new GameObject("Mai_SteamBurst");

        var steam = Add(root, "ps_steam",
            _assets.AlphaMat("M_Steam", "mist", new Color(0.95f, 0.93f, 0.9f, 0.5f)), "Overlay", 70);
        SetLifetime(steam, 1.0f, 1.8f);
        SetSize(steam, 0.4f, 1.2f);
        SetSpeed(steam, 0.6f, 1.8f);
        SetGravity(steam, -0.05f);
        SetBurst(steam, 0f, 12);
        SetFade(steam, true);
        SetSizeCurve(steam, (0f, 0.35f), (1f, 1.8f));
        SetShapeCircle(steam, 0.3f);
        SetNoise(steam, 0.4f, 0.7f, 0.5f);

        var hiss = Add(root, "ps_hiss",
            _assets.AdditiveMat("M_Hiss", "streak", IceWhite, 0.9f), "Overlay", 72);
        SetLifetime(hiss, 0.25f, 0.45f);
        SetSize(hiss, 0.25f, 0.6f);
        SetSpeed(hiss, 1.2f, 3.0f);
        SetBurst(hiss, 0f, 10);
        SetFade(hiss);
        SetShapeCircle(hiss, 0.12f);

        return root;
    }

    #endregion

    #region module shorthands

    static ParticleSystem Add(GameObject root, string name, Material mat, string sortingLayer, int order)
        => ParticleBuilder.AddEmitter(root.transform, name, mat, sortingLayer, order);

    static void SetLifetime(ParticleSystem ps, float a, float b) => ParticleBuilder.SetLifetime(ps, a, b);
    static void SetSize(ParticleSystem ps, float a, float b) => ParticleBuilder.SetSize(ps, a, b);
    static void SetSpeed(ParticleSystem ps, float a, float b) => ParticleBuilder.SetSpeed(ps, a, b);
    static void SetGravity(ParticleSystem ps, float g) => ParticleBuilder.SetGravity(ps, g);
    static void SetBurst(ParticleSystem ps, float t, int n) => ParticleBuilder.SetBurst(ps, t, n);
    static void SetRate(ParticleSystem ps, float r) => ParticleBuilder.SetRate(ps, r);
    static void SetFade(ParticleSystem ps, bool fadeIn = false) => ParticleBuilder.SetFade(ps, fadeIn);
    static void SetRotation(ParticleSystem ps, float a, float b) => ParticleBuilder.SetRotation(ps, a, b);
    static void SetShapeCircle(ParticleSystem ps, float r) => ParticleBuilder.SetShapeCircle(ps, r);
    static void SetShapeBox(ParticleSystem ps, Vector2 size, bool downward = false, float offsetY = 0f)
        => ParticleBuilder.SetShapeBox(ps, size, downward, offsetY);
    static void SetRadialVelocity(ParticleSystem ps, float r, float t) => ParticleBuilder.SetRadialVelocity(ps, r, t);
    static void SetNoise(ParticleSystem ps, float s, float f, float sc) => ParticleBuilder.SetNoise(ps, s, f, sc);
    static void SetSizeCurve(ParticleSystem ps, params (float, float)[] keys) => ParticleBuilder.SetSizeCurve(ps, keys);

    #endregion
}
