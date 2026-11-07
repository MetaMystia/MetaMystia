// 依赖 day-map-test-init.csx、day-nav-init.csx；仅适用测试包的既定坐标。
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils;

public static class DayMapWalk
{
    public static string Report = "idle";
    public static string BehindPng;
    public static string FrontPng;
    public static string OverlayPng;

    public static IEnumerator Run()
    {
        if (DayScene.SceneManager.Instance.CurrentActiveMapLabel != "_ResourceEx_Map_1073742000" || AITestNav.Busy)
        {
            Report = "unavailable";
            yield break;
        }
        var targets = new Vector2[] { new(-7, 0), new(-7, 4), new(-7, 2), new(-4, 0), new(0, 1) };
        var report = new StringBuilder();
        Report = "running";
        for (int i = 0; i < targets.Length; i++)
        {
            var started = AITestNav.Go(targets[i].x, targets[i].y);
            if (!AITestNav.Busy) { Report = report + started; yield break; }
            while (AITestNav.Busy) yield return null;
            report.AppendLine(AITestNav.Status);
            if (!AITestNav.Status.StartsWith("arrived")) { Report = report.ToString(); yield break; }
            if (i != 1 && i != 2 && i != 4) continue;
            report.AppendLine(DayMapTest.Snapshot());
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            var png = Convert.ToBase64String(ImageConversion.EncodeToPNG(texture));
            UnityEngine.Object.Destroy(texture);
            if (i == 1) BehindPng = png;
            if (i == 2) FrontPng = png;
            if (i == 4) OverlayPng = png;
        }
        Report = report.ToString();
    }
}

DayScene.SceneManager.Instance.Character.StartCoroutine(DayMapWalk.Run());
"walk scheduled"
