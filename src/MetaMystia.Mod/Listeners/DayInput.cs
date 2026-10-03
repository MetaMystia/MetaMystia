using UnityEngine;

using Common.CharacterUtility;
using Common.UI;

using Mystia.Listeners;

using MetaMystia.Multiplayer;
using MetaMystia.UI;

namespace MetaMystia.Listeners;

/// <summary>
/// 白天场景的本地输入（原 <c>CharacterControllerInputGeneratorComponentPatch</c> 与
/// <c>DayScenePlayerInputPatch</c>）。跳过原版由 <see cref="DaySync"/> 在
/// <c>Update</c> 里按同一条件操作输入开关完成，这里只做通知带来的状态更新。
/// </summary>
[AutoLog]
public sealed partial class DayInput : IDayInputListener
{
    /// <summary>原 <c>CharacterControllerInputGeneratorComponentPatch.UpdateInputDirection_Prefix</c>。</summary>
    public void OnMoveInput(CharacterControllerUnit unit, Vector2 direction)
    {
        if (!GameSession.IsOnline)
        {
            return;
        }

        if (GameFlow.LocalScene != Scene.DayScene && GameFlow.LocalScene != Scene.WorkScene)
        {
            return;
        }

        if (unit is null)
        {
            return;
        }

        var characterCollection = Common.SceneDirector.Instance.characterCollection;
        if (characterCollection is null || !characterCollection.ContainsKey("Self"))
        {
            Log.Warning("characterCollection does not contain 'Self' key");
            return;
        }

        if (unit.name != characterCollection["Self"].name)
        {
            return;
        }

        PlayerManager.LocalInputDirection = direction;
        PlayerProfile.SendMotion();
    }

    /// <summary>原 <c>DayScenePlayerInputPatch.OnSprintPerformed_Prefix</c> 的状态更新部分。</summary>
    public void OnSprintStarted()
    {
        if (InGameConsole.IsOpen)
        {
            return;
        }
        PlayerManager.LocalIsSprinting = true;
        PlayerProfile.SendMotion();
    }

    /// <summary>原 <c>DayScenePlayerInputPatch.OnSprintCanceled_Prefix</c>。</summary>
    public void OnSprintStopped()
    {
        PlayerManager.LocalIsSprinting = false;
        PlayerProfile.SendMotion();
    }
}
