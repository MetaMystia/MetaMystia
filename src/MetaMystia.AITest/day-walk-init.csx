using System.Collections;
using BepInEx.Unity.IL2CPP.Utils;
using DayScene.Input;

public static class AITestWalk
{
    public static string Status = "idle";
    public static bool Busy;

    public static string Start(float x, float y, float seconds)
    {
        var p = UnityEngine.Object.FindObjectOfType<DayScenePlayerInputGenerator>();
        if (Busy || p == null || !Common.UI.UniversalGameManager.IsInputEnabled || !p.internalAvailability) return "unavailable";
        Busy = true;
        p.StartCoroutine(Run(p, Vector2.ClampMagnitude(new Vector2(x, y), 1f), Mathf.Clamp(seconds, 0.02f, 2f)));
        return Status;
    }

    static IEnumerator Run(DayScenePlayerInputGenerator p, Vector2 direction, float seconds)
    {
        var start = p.transform.position;
        Status = "moving from " + start;
        var end = Time.realtimeSinceStartup + seconds;
        p.UpdateInputDirection(direction * p.moveSpeed);
        while (p != null && Time.realtimeSinceStartup < end && Common.UI.UniversalGameManager.IsInputEnabled && p.internalAvailability)
            yield return null;
        if (p != null)
        {
            p.ExternalStop();
            Status = "stopped: " + start + " -> " + p.transform.position;
        }
        else Status = "player destroyed";
        Busy = false;
    }
}

AITestWalk.Status
