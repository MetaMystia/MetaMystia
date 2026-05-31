using MemoryPack;

using MetaMystia.Patch;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：通告某个厨具(包括空厨具)中的料理被取出
/// </summary>
[MemoryPackable]
[AutoLog]
[HostRelay]
public partial class ExtractAction : Action
{
    public override ActionType Type => ActionType.EXTRACT;
    public int GridIndex { get; set; }

    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var cookerController = CookManager.GetCookerControllerByIndex(GridIndex);
            if (cookerController == null)
            {
                Log.LogWarning($"Failed to find CookerController with GridIndex={GridIndex}");
                return;
            }
            CookControllerPatch.Extract_ReversePatch(cookerController, null);
        });
    }

    public static void Send(int gridIndex)
    {
        var action = new ExtractAction
        {
            GridIndex = gridIndex
        };
        action.SendToHostOrBroadcast();
    }
}
