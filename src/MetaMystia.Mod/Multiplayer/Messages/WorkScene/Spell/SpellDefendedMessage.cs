using MemoryPack;

using NightScene.EventUtility;

using MetaMystia.Multiplayer.Spells;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>主机：一张惩罚符卡被博丽护盾抵消。客机在已排队的符卡之后扣除本地护盾。</summary>
[MemoryPackable]
[AutoLog]
public partial class SpellDefendedMessage : MultiplayerMessage
{
    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => SpellRegistry.Defer(() =>
        EventManager.Instance.TryDeductCountedBuffValue(EventManager.BuffType.Skill_Reimu_Shield, true));

    public static void Send() => new SpellDefendedMessage().Enqueue();
}
