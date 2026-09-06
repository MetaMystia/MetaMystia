using System.Collections;
using BepInEx.Unity.IL2CPP.Utils;

public static class AITestCapture
{
    public static string Png;
    public static IEnumerator Capture()
    {
        Png = null;
        yield return new WaitForEndOfFrame();
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        Png = Convert.ToBase64String(ImageConversion.EncodeToPNG(texture));
        UnityEngine.Object.Destroy(texture);
    }
}

UnityEngine.Object.FindObjectOfType<DayScene.Input.DayScenePlayerInputGenerator>().StartCoroutine(AITestCapture.Capture());
"Capture scheduled"
