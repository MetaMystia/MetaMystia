using System.Collections;
using BepInEx.Unity.IL2CPP.Utils;
using DayScene.Input;

public static class AITestNav
{
    public static string Status = "idle";
    public static bool Busy;

    public static string Go(float x, float y)
    {
        var p = UnityEngine.Object.FindObjectOfType<DayScenePlayerInputGenerator>();
        if (Busy || p == null || !Common.UI.UniversalGameManager.IsInputEnabled || !p.internalAvailability) return "unavailable";
        Busy = true;
        p.StartCoroutine(Run(p, new Vector2(x, y)));
        return Status;
    }

    static IEnumerator Run(DayScenePlayerInputGenerator p, Vector2 target)
    {
        var start = p.transform.position;
        var map = DayScene.SceneManager.Instance.CurrentActiveMapLabel;
        var deadline = Time.realtimeSinceStartup + 4f;
        var checkpoint = start;
        var lastProgress = Time.realtimeSinceStartup;
        var reason = "timeout";
        Status = "moving toward " + target;
        while (p != null && Time.realtimeSinceStartup < deadline)
        {
            if (DayScene.SceneManager.Instance.IsMapSwapping || DayScene.SceneManager.Instance.CurrentActiveMapLabel != map) { reason = "map changed"; break; }
            if (!Common.UI.UniversalGameManager.IsInputEnabled || !p.internalAvailability) { reason = "input disabled"; break; }
            var delta = target - (Vector2)p.transform.position;
            if (delta.magnitude < 0.14f) { reason = "arrived"; break; }
            if (Vector3.Distance(checkpoint, p.transform.position) > 0.04f) { checkpoint = p.transform.position; lastProgress = Time.realtimeSinceStartup; }
            if (Time.realtimeSinceStartup - lastProgress > 0.4f) { reason = "blocked"; break; }
            p.UpdateInputDirection(delta.normalized * p.moveSpeed);
            yield return null;
        }
        if (p != null) { p.ExternalStop(); Status = reason + " | " + start + " -> " + p.transform.position; }
        else Status = "player destroyed";
        Busy = false;
    }
}

AITestNav.Status
