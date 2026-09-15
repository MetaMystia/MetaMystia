using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;

using Common;
using Common.UI;
using GameData.Core.Collections.DaySceneUtility;

using MetaMystia.Multiplayer;
using MetaMystia.ResourceEx.Registries;
using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class DayMapCommands
{
    private static DayScene.SceneManager returnScene;
    private static string returnLabel;
    private static string returnMarker;

    public static void Register(Command resourceEx)
    {
        var map = new Command("map", "Isolated resource-pack maps");
        var list = new Command("list", "List registered map IDs");
        list.SetHandler(ctx => ctx.Log(TextId.DayMapList.Get(string.Join(", ", DayMapRegistry.Ids.OrderBy(id => id)))));
        var enter = new Command("goto", "Enter a map by ID and optional spawn marker");
        var id = new Argument<int>("id");
        var marker = new Argument<string>("marker", () => null);
        enter.AddArgument(id);
        enter.AddArgument(marker);
        enter.SetHandler(ctx => Enter(ctx, ctx.ParseResult.GetValueForArgument(id), ctx.ParseResult.GetValueForArgument(marker)));
        var back = new Command("back", "Return to the original map spawn marker");
        back.SetHandler(Back);
        map.AddCommand(list);
        map.AddCommand(enter);
        map.AddCommand(back);
        map.SetHandler(ctx => ctx.Log(TextId.DayMapHelp.Get()));
        resourceEx.AddCommand(map);
        CommandRegistry.RegisterCompletions("resourceex map", 0, "list", "goto", "back");
        CommandRegistry.RegisterDynamicCompletions("resourceex map goto", 0, () => DayMapRegistry.Ids.Select(id => id.ToString()).ToArray());
    }

    private static bool Ready(InvocationContext ctx)
    {
        var scene = DayScene.SceneManager.Instance;
        if (GameFlow.LocalScene == Scene.DayScene && !GameSession.HasPeers && scene != null &&
            scene.CurrentActiveMap != null && !scene.IsMapSwapping && !SceneDirector.IsInEvent && !PlayerManager.LocalIsDayOver)
            return true;
        ctx.Log(ConsoleFormat.Warn(TextId.DayMapUnavailable.Get()));
        return false;
    }

    private static void Enter(InvocationContext ctx, int id, string marker)
    {
        if (!Ready(ctx)) return;
        if (!DayMapRegistry.TryGetDestination(id, marker, out var label, out var spawn))
        {
            ctx.Log(ConsoleFormat.Warn(TextId.DayMapInvalid.Get()));
            return;
        }
        var scene = DayScene.SceneManager.Instance;
        if (returnScene != scene || returnLabel == null)
        {
            string originMarker = null;
            foreach (var pair in scene.CurrentActiveMap.AllSpawnMarkers) { originMarker = pair.Key; break; }
            if (originMarker == null) { ctx.Log(ConsoleFormat.Warn(TextId.DayMapNoReturn.Get())); return; }
            returnScene = scene;
            returnLabel = scene.CurrentActiveMapLabel;
            returnMarker = originMarker;
        }
        scene.SwapMap(label, spawn, 0, true, true, false, null);
        ctx.Log(TextId.DayMapRequested.Get(label));
        ctx.RequestCloseConsole();
    }

    private static void Back(InvocationContext ctx)
    {
        if (!Ready(ctx)) return;
        var scene = DayScene.SceneManager.Instance;
        if (returnScene != scene || returnLabel == null || !DataBaseDay.mapReference.ContainsKey(returnLabel) ||
            !DataBaseDay.allSpawnMarkerLabels.TryGetValue(returnLabel, out var markers) || !markers.Contains(returnMarker))
        {
            ctx.Log(ConsoleFormat.Warn(TextId.DayMapNoReturn.Get()));
            return;
        }
        var label = returnLabel;
        scene.SwapMap(label, returnMarker, 0, true, true, false, (Il2CppSystem.Action)(() =>
        {
            returnScene = null;
            returnLabel = returnMarker = null;
        }));
        ctx.Log(TextId.DayMapRequested.Get(label));
        ctx.RequestCloseConsole();
    }
}
