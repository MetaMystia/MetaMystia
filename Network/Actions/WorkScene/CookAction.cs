using MemoryPack;

using GameData.Core.Collections;

using MetaMystia.Patch;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：通告锁定某个厨具以准备烹饪某个料理，总是在 QTEAction 之前触发。
/// </summary>
[MemoryPackable]
[AutoLog]
[HostRelay]
public partial class CookAction : Action
{
    public override ActionType Type => ActionType.COOK;
    public int GridIndex { get; set; }
    public int RecipeId { get; set; }
    public SellableFood Food { get; set; }

    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        Log.LogInfo($"Received COOK: CookerIndex={GridIndex}, FoodId={Food.Id}, Modifiers=[{string.Join(",", Food.ModifierIds)}]");
        PluginManager.Instance.RunOnMainThread(() =>
        {
            if (!PlayerManager.RecipeAvailable(RecipeId))
            {
                Log.Error($"RecipeId {RecipeId} not available!");
                return;
            }
            var recipe = RecipeId.RefRecipe();
            if (recipe == null)
            {
                Log.LogWarning($"Failed to create recipe");
                return;
            }

            var food = Food.ToSellable();

            var cookerController = CookManager.GetCookerControllerByIndex(GridIndex);
            if (cookerController == null)
            {
                Log.LogWarning($"Failed to find CookerController with GridIndex={GridIndex}");
                return;
            }

            CookControllerPatch.SetCook_ReversePatch(cookerController, food, recipe, false);
        });
    }

    public static void Send(int gridIndex, SellableFood food, int recipeId)
    {
        var action = new CookAction
        {
            GridIndex = gridIndex,
            RecipeId = recipeId,
            Food = food
        };
        action.SendToHostOrBroadcast();
    }
}
