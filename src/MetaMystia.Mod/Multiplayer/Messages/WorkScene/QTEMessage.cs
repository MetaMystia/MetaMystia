using System;

using MemoryPack;

using MetaMystia.Patch;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 任何玩家 → 全体玩家：通告某个厨具的 QTE 结果以启动料理倒计时，总是在 NightCookMessage 之后触发
/// QTE(Quick Time Event): 夜雀之歌
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class QTEMessage : MultiplayerMessage
{
    public int GridIndex { get; set; }
    public float QTEScore { get; set; }
    public Guid OperationId { get; set; }
    public bool AllowInterrupt { get; set; }

    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        if (YuyukoGuestSync.IsSwallowedCooker(GridIndex)) return;
        var cookerController = CookManager.GetCookerControllerByIndex(GridIndex);
        if (cookerController == null)
        {
            Log.LogWarning($"Failed to find CookerController with GridIndex={GridIndex}");
            return;
        }
        if (!CookerSync.StartRemote(cookerController, OperationId, SenderUid)) return;
        CookControllerPatch.StartCookCountDown_ReversePatch(cookerController, QTEScore, AllowInterrupt);
    }

    public static void Send(int gridIndex, float qteScore, Guid operationId, bool allowInterrupt) =>
        new QTEMessage { GridIndex = gridIndex, QTEScore = qteScore, OperationId = operationId, AllowInterrupt = allowInterrupt }.Enqueue();
}
