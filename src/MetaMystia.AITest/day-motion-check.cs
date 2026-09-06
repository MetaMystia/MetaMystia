using UnityEngine;

using DayScene.Input;

public static class Payload
{
    public static object Execute()
    {
        var p = Object.FindObjectOfType<DayScenePlayerInputGenerator>();
        if (p == null) return "No player";
        var c = p.Character;
        var rows = new System.Collections.Generic.List<string>
        {
            $"position={p.transform.position} moving={p.Moving} deltaInputAvailability={p.deltaInputAvailability} velocity={c.inputDirection} speed={c.CurrentMoveSpeed}",
            $"enabled={p.enabled}/{c.enabled} rigidbody={c.rb2d.simulated} constraints={c.rb2d.constraints} timeScale={Time.timeScale}"
        };
        foreach (var hit in Physics2D.OverlapCircleAll(p.transform.position, 1.5f))
            rows.Add($"COLLIDER | {hit.name} | trigger={hit.isTrigger} | bounds={hit.bounds}");
        return string.Join("\n", rows);
    }
}
