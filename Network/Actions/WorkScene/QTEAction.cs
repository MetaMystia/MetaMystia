using MemoryPack;

using MetaMystia.Patch;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：通告某个厨具的 QTE 结果以启动料理倒计时，总是在 CookAction 之后触发
/// QTE(Quick Time Event): 夜雀之歌
/// </summary>
[MemoryPackable]
[AutoLog]
[RoomRelay]
public partial class QTEAction : Action
{
    public int GridIndex { get; set; }
    public float QTEScore { get; set; }

    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var cookerController = CookManager.GetCookerControllerByIndex(GridIndex);
        if (cookerController == null)
        {
            Log.LogWarning($"Failed to find CookerController with GridIndex={GridIndex}");
            return;
        }
        CookControllerPatch.StartCookCountDown_ReversePatch(cookerController, QTEScore, false);
    }

    public static void Send(int gridIndex, float qteScore) =>
        new QTEAction { GridIndex = gridIndex, QTEScore = qteScore }.Enqueue();
}
