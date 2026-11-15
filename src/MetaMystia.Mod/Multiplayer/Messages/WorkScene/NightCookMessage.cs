using System;

using MemoryPack;

using GameData.Core.Collections;

using MetaMystia.Patch;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 任何玩家 → 全体玩家：通告锁定某个厨具以准备烹饪某个料理，总是在 QTEMessage 之前触发。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class NightCookMessage : MultiplayerMessage
{
    public int GridIndex { get; set; }
    public int? RecipeId { get; set; }
    public Guid OperationId { get; set; }
    public int Revision { get; set; }
    public SellableFood Food { get; set; }

    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        if (YuyukoGuestSync.IsSwallowedCooker(GridIndex)) return;
        Log.LogInfo($"Received COOK: CookerIndex={GridIndex}, FoodId={Food.Id}, Modifiers=[{string.Join(",", Food.ModifierIds)}]");
        if (RecipeId.HasValue && !PlayerManager.RecipeAvailable(RecipeId.Value))
        {
            Log.Error($"RecipeId {RecipeId} not available!");
            return;
        }
        var recipe = RecipeId?.RefRecipe();
        if (RecipeId.HasValue && recipe == null)
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

        if (food == null || !CookerSync.ReceiveCook(cookerController, OperationId, Revision, SenderUid)) return;
        CookControllerPatch.SetCook_ReversePatch(cookerController, food, recipe, false);
    }

    public static void Send(int gridIndex, SellableFood food, int? recipeId, Guid operationId, int revision) =>
        new NightCookMessage { GridIndex = gridIndex, RecipeId = recipeId, Food = food, OperationId = operationId, Revision = revision }.Enqueue();
}
