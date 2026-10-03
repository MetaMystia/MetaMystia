using System.Linq;

using GameData.CoreLanguage.Collections;

using Mystia.Listeners;

using MetaMystia.Multiplayer;
using MetaMystia.UI;

namespace MetaMystia.Listeners;

/// <summary>
/// 选菜提交监听，取代原 <c>WorkSceneCookingSelectionPannel__c__DisplayClass79_0Patch</c>：
/// 提交前复查对端是否拥有本次菜谱与所选食材，缺任意一项就拦下这次提交。
/// <para>桥接从提交闭包读到的是面板的 <c>selectedIngredients</c>（玩家实际选中的食材，
/// 迁移前复查的 <c>solved.Modifiers</c> 是其中不属于菜谱的那部分），因此现在整份选中清单都会被复查。</para>
/// </summary>
[AutoLog]
public sealed partial class CookSelectionSync : ICookSelectionListener
{
    public void OnPreCookingSubmit(ref CookingRequest request, ref bool cancelInvocation)
    {
        if (!GameSession.HasRoomPeers) return;

        var recipe = request.Recipe;
        if (recipe is null) return;

        if (!PlayerManager.RecipeAvailable(recipe.Id))
        {
            Log.Warning($"Peer does not have recipe {recipe.Id}, blocking OnSubmit");
            InGameConsole.ShowPassive(TextId.DLCPeerRecipeNotAvailable.Get(recipe.Id));
            cancelInvocation = true;
            return;
        }

        if (request.IngredientIds is not { Length: > 0 } ingredients) return;

        var unavailable = ingredients.Where(id => !PlayerManager.IngredientAvailable(id)).ToList();
        if (unavailable.Count == 0) return;

        var ingredientNames = unavailable.Select(id => $"{DataBaseLanguage.Ingredients[id]?.Name ?? "Unknown"}({id})");
        var ingredientList = string.Join(", ", ingredientNames);
        Log.Warning($"Peer does not have modifier ingredient {ingredientList}, blocking OnSubmit");
        InGameConsole.ShowPassive(TextId.DLCPeerIngredientNotAvailable.Get(ingredientList));
        cancelInvocation = true;
    }
}
