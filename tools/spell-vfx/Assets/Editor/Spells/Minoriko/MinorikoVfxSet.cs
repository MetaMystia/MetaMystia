using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

using static ParticleBuilder;

/// <summary>以稻穗与葡萄的局部生长、升起和消散表现丰收。</summary>
public sealed class MinorikoVfxSet : ISpellVfxSet
{
    public string Name => "Minoriko";
    public string BundleName => "minorikospell";

    private static readonly Color Gold = new Color(1f, 0.72f, 0.27f);
    private static readonly Color Cream = new Color(1f, 0.94f, 0.67f);
    private SpellAssetFactory _assets;

    public IEnumerable<GameObject> BuildPrefabs(SpellAssetFactory assets)
    {
        _assets = assets;
        foreach (var texture in new[] { "rice", "grapes", "grain", "mote", "spark" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath($"{assets.TextureDir}/{texture}.png");
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
        return new[] { Cast(), Harvest(), Autumn(), Sated() };
    }

    private GameObject Cast()
    {
        var root = new GameObject("Minoriko_Cast");
        var rice = AddEmitter(root.transform, "opening_sheaf",
            _assets.AlphaMat("Rice", "rice", Color.white), "EffectOverlay", 50);
        SetLifetime(rice, 2.2f, 2.2f);
        SetSize(rice, 3.4f, 3.4f);
        SetBurst(rice, 0f, 1);
        SetFade(rice, true);
        var shape = rice.shape;
        shape.enabled = false;
        var velocity = rice.velocityOverLifetime;
        velocity.enabled = true;
        velocity.y = 0.35f;
        // 穗束从下端舒展，不使用圆形法阵。
        rice.GetComponent<ParticleSystemRenderer>().pivot = new Vector3(0f, 0.35f, 0f);
        var size = rice.sizeOverLifetime;
        size.enabled = true;
        size.separateAxes = true;
        size.x = new ParticleSystem.MinMaxCurve(1f, Curve((0f, 0.3f), (0.3f, 1f), (1f, 1.1f)));
        size.y = new ParticleSystem.MinMaxCurve(1f, Curve((0f, 0.05f), (0.4f, 1f), (1f, 1.15f)));
        size.z = 1f;

        var grain = AddEmitter(root.transform, "rising_grain",
            _assets.AlphaMat("Grain", "grain", Color.white), "EffectOverlay", 51);
        SetLifetime(grain, 1.1f, 2f);
        SetSize(grain, 0.12f, 0.24f);
        SetSpeed(grain, 1f, 2.5f);
        SetBurst(grain, 0.2f, 24);
        SetShapeBox(grain, new Vector2(2.6f, 0.3f));
        SetRotation(grain, -90f, 90f);
        SetFade(grain);
        return root;
    }

    private GameObject Harvest()
    {
        var root = new GameObject("Minoriko_Harvest");
        var rice = AddEmitter(root.transform, "ripening_rice",
            _assets.AlphaMat("Rice", "rice", Color.white), "Overlay", 22);
        SetLifetime(rice, 2.8f, 4f);
        SetSize(rice, 0.85f, 1.3f);
        SetSpeed(rice, 0.28f, 0.45f);
        SetRate(rice, 2f);
        SetShapeBox(rice, new Vector2(9f, 2.2f), offsetY: -0.5f);
        SetFade(rice, true);
        SetSizeCurve(rice, (0f, 0.3f), (0.4f, 1f), (1f, 0.85f));
        SetRotation(rice, -6f, 6f);
        var main = rice.main;
        main.loop = true;

        var grapes = AddEmitter(root.transform, "ripening_grapes",
            _assets.AlphaMat("Grapes", "grapes", Color.white), "Overlay", 23);
        SetLifetime(grapes, 2.6f, 3.6f);
        SetSize(grapes, 0.45f, 0.65f);
        SetSpeed(grapes, 0.22f, 0.4f);
        SetRate(grapes, 0.65f);
        SetShapeBox(grapes, new Vector2(7.5f, 2f));
        SetFade(grapes, true);
        SetRotation(grapes, -10f, 10f);
        main = grapes.main;
        main.loop = true;

        var pollen = AddEmitter(root.transform, "harvest_motes",
            _assets.AdditiveMat("Mote", "mote", Gold, 0.6f), "EffectOverlay", 18);
        SetLifetime(pollen, 2f, 3.2f);
        SetSize(pollen, 0.05f, 0.12f);
        SetSpeed(pollen, 0.3f, 0.6f);
        SetRate(pollen, 8f);
        SetShapeBox(pollen, new Vector2(9f, 2f));
        SetFade(pollen, true);
        main = pollen.main;
        main.loop = true;
        return root;
    }

    private GameObject Autumn()
    {
        var root = new GameObject("Minoriko_Autumn");
        var grain = AddEmitter(root.transform, "scattered_grain",
            _assets.AlphaMat("Grain", "grain", Color.white), "Overlay", 60);
        SetLifetime(grain, 0.9f, 1.6f);
        SetSize(grain, 0.15f, 0.28f);
        SetSpeed(grain, 1.8f, 4.2f);
        SetBurst(grain, 0f, 32);
        SetShapeCircle(grain, 0.3f);
        SetRotation(grain, -160f, 160f);
        SetFade(grain);

        var grapes = AddEmitter(root.transform, "scattered_grapes",
            _assets.AlphaMat("Grapes", "grapes", Color.white), "Overlay", 61);
        SetLifetime(grapes, 0.8f, 1.4f);
        SetSize(grapes, 0.45f, 0.65f);
        SetSpeed(grapes, 1.2f, 2.6f);
        SetBurst(grapes, 0f, 5);
        SetShapeCircle(grapes, 0.5f);
        SetRotation(grapes, -45f, 45f);
        SetFade(grapes);
        return root;
    }

    private GameObject Sated()
    {
        var root = new GameObject("Minoriko_Sated");
        var spark = AddEmitter(root.transform, "sated_glimmer",
            _assets.AdditiveMat("Spark", "spark", Cream), "EffectOverlay", 65);
        SetLifetime(spark, 0.3f, 0.65f);
        SetSize(spark, 0.18f, 0.4f);
        SetSpeed(spark, 0.3f, 0.9f);
        SetBurst(spark, 0f, 8);
        SetShapeCircle(spark, 0.12f);
        SetFade(spark);
        SetSizeCurve(spark, (0f, 0.35f), (0.25f, 1f), (1f, 0f));
        return root;
    }
}
