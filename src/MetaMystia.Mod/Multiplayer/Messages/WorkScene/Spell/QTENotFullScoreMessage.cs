using MemoryPack;

using NightScene.CookingUtility;

using MetaMystia.Multiplayer.Spells;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 客机 → 主机：本端 QTE 未满分。主机触发符卡惩罚（灵乌路空、苏我屠自古）并广播，
/// 客机在已排队的符卡之后触发。
/// </summary>
[MemoryPackable]
public partial class QTENotFullScoreMessage : MultiplayerMessage
{
    public int GridIndex { get; set; }

    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        if (!GameSession.IsRoomHost)
        {
            SpellRegistry.Defer(Trigger);
            return;
        }
        Send(GridIndex);
        Trigger();
    }

    private void Trigger()
    {
        var cooker = CookManager.GetCookerControllerByIndex(GridIndex);
        if (cooker != null) CookSystemManager.Instance.OnQTENotFullScoreCallback?.Invoke(cooker);
    }

    public static void Send(int gridIndex) => new QTENotFullScoreMessage { GridIndex = gridIndex }.Enqueue();
}
