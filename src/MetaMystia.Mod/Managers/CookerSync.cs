using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Il2CppSystem.IO;

using Common.UI;
using DEYU.Utils;
using GameData.RunTime.Common;
using NightScene.CookingUtility;
using NightScene.UI.CookingUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Patch;

namespace MetaMystia;

public static class CookerSync
{
    private static readonly CookerOperations operations = new();
    private static readonly Dictionary<int, Guid> localStarts = new();
    private static readonly Dictionary<Guid, int[]> ingredients = new();
    private static int[] selectedIngredients;
    private static int selectedGrid = -1;
    private static int generation;
    public static bool Active => GameSession.IsInRoom && GameFlow.LocalScene == Scene.WorkScene;
    public static CookerOperations.Operation Get(int grid) => operations.Get(grid);

    public static void CaptureIngredients(WorkSceneCookingSelectionPannel panel)
    {
        selectedGrid = panel.CookerParent.GridIndex;
        selectedIngredients = Enumerable.Range(0, panel.ExtraCostIng)
            .SelectMany(_ => panel.selectedIngredients.ToArray()).ToArray();
    }

    public static CookerOperations.Operation Commit(CookController cooker, bool returnIngredients)
    {
        int grid = cooker.GridIndex;
        var operation = new CookerOperations.Operation(Guid.NewGuid(), operations.Revision(grid), PlayerManager.Local.Uid);
        operations.Accept(grid, operation);
        localStarts[grid] = operation.Id;
        if (returnIngredients && selectedGrid == grid && selectedIngredients != null) ingredients[operation.Id] = selectedIngredients;
        selectedIngredients = null;
        Watch(cooker, operation);
        return operation;
    }

    public static bool ReceiveCook(CookController cooker, Guid id, int revision, int owner)
    {
        var previous = Get(cooker.GridIndex);
        var next = new CookerOperations.Operation(id, revision, owner);
        if (!operations.Accept(cooker.GridIndex, next)) return false;
        StopTimer(cooker);
        if (previous != null && ingredients.Remove(previous.Id, out var refund))
        {
            var values = new Il2CppSystem.Collections.Generic.List<int>();
            foreach (int ingredient in refund) values.Add(ingredient);
            RunTimeStorage.IngredientInRange(values.Cast<Il2CppSystem.Collections.Generic.IEnumerable<int>>(), true);
        }
        Watch(cooker, next);
        return true;
    }

    public static bool StartLocal(CookController cooker, out Guid id)
    {
        id = Guid.Empty;
        if (!localStarts.Remove(cooker.GridIndex, out id)) return !Active;
        return CanStart(cooker) && operations.Start(cooker.GridIndex, id, PlayerManager.Local.Uid);
    }

    public static bool StartRemote(CookController cooker, Guid id, int owner) =>
        CanStart(cooker) && operations.Start(cooker.GridIndex, id, owner);

    private static bool CanStart(CookController cooker) =>
        cooker.Phase == CookController.CookPhase.Loaded && cooker.Result != null;

    public static bool Complete(CookController cooker, Guid id, int revision)
    {
        if (!operations.Complete(cooker.GridIndex, id, revision)) return false;
        ingredients.Remove(id);
        StopTimer(cooker);
        return true;
    }

    public static void StopTimer(CookController cooker)
    {
        if (cooker.onCookingCoroutine == null) return;
        cooker.StopCoroutine(cooker.onCookingCoroutine);
        cooker.onCookingCoroutine = null;
    }

    private static void Watch(CookController cooker, CookerOperations.Operation operation) =>
        PluginHost.Instance.StartManagedCoroutine(RecoverDisconnected(cooker, operation, generation));

    private static IEnumerator RecoverDisconnected(CookController cooker, CookerOperations.Operation operation, int capturedGeneration)
    {
        // SetCook 的接收路径在此协程启动后才写入原版状态。
        yield return null;
        while (capturedGeneration == generation && cooker != null && Get(cooker.GridIndex)?.Id == operation.Id)
        {
            if (Get(cooker.GridIndex).Started) yield break;
            if (!GameSession.IsInRoom || !GameSession.Room.Members.Any(p => p.Uid == operation.Owner))
            {
                if (StartRemote(cooker, operation.Id, operation.Owner))
                    CookControllerPatch.StartCookCountDown_ReversePatch(cooker, -1f, false);
                yield break;
            }
            yield return null;
        }
    }

    public static void Reset(bool resume = false)
    {
        if (resume && GameFlow.LocalScene == Scene.WorkScene)
        {
            var cookers = CookSystemManager.Instance?.AllCookerControllers;
            if (cookers != null)
                foreach (var cooker in cookers.ToArray())
                {
                    var operation = Get(cooker.GridIndex);
                    if (operation != null && StartRemote(cooker, operation.Id, operation.Owner))
                        CookControllerPatch.StartCookCountDown_ReversePatch(cooker, -1f, false);
                }
        }
        generation++;
        operations.Clear();
        ingredients.Clear();
        selectedIngredients = null;
        // 退房时保留尚未返回的本地 QTE 标识，避免它再次启动已接管的倒计时。
        if (!resume) localStarts.Clear();
    }

    public static void ForgetLocal(int grid) => localStarts.Remove(grid);
}
