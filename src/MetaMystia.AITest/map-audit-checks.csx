public static class MapAuditChecks
{
    public static string Report = "idle";
    public static string BehindPng;
    public static string FrontPng;
    public static string OverheadPng;

    public static System.Collections.IEnumerator Run()
    {
        if (DayScene.SceneManager.Instance.CurrentActiveMapLabel != MapAudit.Label || AITestNav.Busy)
        {
            Report = "unavailable";
            yield break;
        }
        Report = "running";
        var targets = new Vector2[] { new(6, 0), new(-8, 0), new(-8, 1), new(-8, -0.8f), new(-5.5f, 2.8f) };
        var output = new System.Text.StringBuilder();
        for (int i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            AITestNav.Go(target.x, target.y);
            while (AITestNav.Busy) yield return null;
            output.AppendLine(AITestNav.Status);
            if (!AITestNav.Status.StartsWith("arrived")) { Report = output.ToString(); yield break; }
            var sm = DayScene.SceneManager.Instance;
            var group = sm.Character.GetComponent<UnityEngine.Rendering.SortingGroup>();
            output.AppendLine($"playerOrder={group.sortingOrder} propOrder={sm.CurrentActiveMap.transform.Find("DepthProp").GetComponent<SpriteRenderer>().sortingOrder}");
            if (i < 2) continue;
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            var png = Convert.ToBase64String(ImageConversion.EncodeToPNG(texture));
            UnityEngine.Object.Destroy(texture);
            if (i == 2) BehindPng = png;
            if (i == 3) FrontPng = png;
            if (i == 4) OverheadPng = png;
        }
        Report = output.ToString();
    }
}

DayScene.SceneManager.Instance.Character.StartCoroutine(MapAuditChecks.Run());
"checks scheduled"
