using System.Collections;
using BepInEx.Unity.IL2CPP.Utils;
using Common.CharacterUtility;

// 依赖 day-map-test-init.csx 和 0.2.0 测试包。
// 先 goto ... SlopeUp / SlopeDown 并确认切图完成，再 Move(3)、等待 Result。
public static class DayMapHeightTest
{
    public static bool Busy;
    public static string Result = "idle";

    public static string Sample()
    {
        var sm = DayScene.SceneManager.Instance;
        if (sm == null || sm.Character == null || sm.IsMapSwapping) return "unavailable";
        var processor = sm.Character.Character.GetInputProcessor<HeightBlendedInputProcessorComponent>();
        if (processor == null) return "processor missing";
        return $"map={sm.CurrentActiveMapLabel} pos={sm.Character.transform.position} bound={processor.heightMap == sm.CurrentActiveMap.height} " +
            $"sample={processor.SampleColorAtCurrentCoordinate()} right={processor.OnInputPassed(Vector2.right)} left={processor.OnInputPassed(Vector2.left)} up={processor.OnInputPassed(Vector2.up)}";
    }

    public static string Move(float distance)
    {
        var sm = DayScene.SceneManager.Instance;
        if (Busy || sm == null || sm.CurrentActiveMapLabel != "_ResourceEx_Map_1073742000" || sm.IsMapSwapping ||
            !Common.UI.UniversalGameManager.IsInputEnabled || !sm.Character.internalAvailability || distance == 0 || Math.Abs(distance) > 5)
            return "unavailable";
        Busy = true;
        sm.Character.StartCoroutine(Walk(distance));
        return Result;
    }

    private static IEnumerator Walk(float distance)
    {
        var sm = DayScene.SceneManager.Instance;
        var player = sm.Character;
        var start = player.transform.position;
        var label = sm.CurrentActiveMapLabel;
        float sign = Math.Sign(distance);
        float deadline = Time.realtimeSinceStartup + 4;
        Result = "moving";
        while (Time.realtimeSinceStartup < deadline && (player.transform.position.x - start.x) * sign < Math.Abs(distance))
        {
            if (sm.IsMapSwapping || sm.CurrentActiveMapLabel != label || !Common.UI.UniversalGameManager.IsInputEnabled || !player.internalAvailability) break;
            player.UpdateInputDirection(new Vector2(sign * player.moveSpeed, 0));
            yield return null;
        }
        player.ExternalStop();
        var delta = player.transform.position - start;
        Result = $"start={start} end={player.transform.position} dx={delta.x:F4} dy={delta.y:F4} dy/dx={delta.y / delta.x:F6}";
        Busy = false;
    }
}

DayMapHeightTest.Sample()
