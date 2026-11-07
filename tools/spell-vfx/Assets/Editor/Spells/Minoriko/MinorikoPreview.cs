using System.IO;

using UnityEditor;
using UnityEngine;

/// <summary>在编辑器相机中渲染特效样张，不代表游戏运行时验证。</summary>
public static class MinorikoPreview
{
    public static void Run()
    {
        var cameraObject = new GameObject("PreviewCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 7.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.10f, 0.12f, 0.105f);
        var target = new RenderTexture(1600, 900, 24);
        camera.targetTexture = target;
        foreach (var name in new[] { "Minoriko_Cast", "Minoriko_Harvest", "Minoriko_Autumn", "Minoriko_Sated" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Spells/Minoriko/Prefabs/{name}.prefab");
            var instance = Object.Instantiate(prefab);
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>())
            {
                ps.useAutoRandomSeed = false;
                ps.randomSeed = 10001;
                ps.Simulate(name == "Minoriko_Harvest" ? 4f : name == "Minoriko_Cast" ? 0.8f : 0.3f, false, true);
            }
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            File.WriteAllBytes($"Build/Minoriko/{name}.png", image.EncodeToPNG());
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(instance);
        }
        // 局部近景：起手后进入持续结界，便于检查动态节奏。
        camera.orthographicSize = 4.5f;
        Directory.CreateDirectory("Build/Minoriko/frames");
        var cast = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Spells/Minoriko/Prefabs/Minoriko_Cast.prefab"));
        var harvest = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Spells/Minoriko/Prefabs/Minoriko_Harvest.prefab"));
        for (var frame = 0; frame < 72; frame++)
        {
            var time = frame / 12f;
            foreach (var instance in new[] { cast, harvest })
            {
                var age = instance == cast ? time : time - 1.2f;
                instance.SetActive(age >= 0f);
                if (age < 0f)
                    continue;
                foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>())
                {
                    ps.useAutoRandomSeed = false;
                    ps.randomSeed = 10001;
                    ps.Simulate(age, false, true);
                }
            }
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            File.WriteAllBytes($"Build/Minoriko/frames/red-{frame:D3}.png", image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        Object.DestroyImmediate(cast);
        Object.DestroyImmediate(harvest);
        RenderTexture.active = null;
        camera.targetTexture = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(cameraObject);
        Debug.Log("[Minoriko] Editor previews written");
    }
}
