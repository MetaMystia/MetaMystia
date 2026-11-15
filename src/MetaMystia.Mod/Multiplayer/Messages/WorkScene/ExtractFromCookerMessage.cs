using System;

using MemoryPack;

using GameData.Core.Collections;

using MetaMystia.Patch;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 任何玩家 → 全体玩家：通告某个厨具(包括空厨具)中的料理被取出
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class ExtractFromCookerMessage : MultiplayerMessage
{
    public int GridIndex { get; set; }
    public Guid OperationId { get; set; }
    public int Revision { get; set; }

    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var cookerController = CookManager.GetCookerControllerByIndex(GridIndex);
        if (cookerController == null)
        {
            Log.LogWarning($"Failed to find CookerController with GridIndex={GridIndex}");
            return;
        }
        if (cookerController.Cooker.Type != Cooker.CookerType.Empty
            && !CookerSync.Complete(cookerController, OperationId, Revision)) return;
        CookControllerPatch.Extract_ReversePatch(cookerController, null);
    }

    public static void Send(int gridIndex, Guid operationId, int revision) =>
        new ExtractFromCookerMessage { GridIndex = gridIndex, OperationId = operationId, Revision = revision }.Enqueue();
}
