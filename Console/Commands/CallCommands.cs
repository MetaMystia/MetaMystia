using System.CommandLine;
using System.CommandLine.Invocation;
using UnityEngine;

using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class CallCommands
{
    public static void Register(RootCommand root)
    {
        var callCmd = new Command("call", "Call game methods");

        // /call getmapsnpcs [mapLabel]
        var getNpcsCmd = new Command("getmapsnpcs", "List NPCs on a map");
        var mapLabelArg = new Argument<string>("mapLabel") { Arity = System.CommandLine.ArgumentArity.ZeroOrOne };
        mapLabelArg.SetDefaultValue("");
        getNpcsCmd.AddArgument(mapLabelArg);
        getNpcsCmd.SetHandler(ctx =>
        {
            try
            {
                string mapLabel = ctx.ParseResult.GetValueForArgument(mapLabelArg);
                if (string.IsNullOrEmpty(mapLabel))
                    mapLabel = PlayerManager.LocalMapLabel;
                var npcs = GameData.RunTime.DaySceneUtility.RunTimeDayScene.GetMapNPCs(mapLabel);
                foreach (var npc in npcs)
                    ctx.Log(TextId.NPCListItem.Get(npc.Key));
                ctx.Log(TextId.TotalNPCsFound.Get(npcs.Count));
            }
            catch (System.Exception e)
            {
                ctx.Log(TextId.ErrorGetMapsnpcs.Get(e.Message));
            }
        });
        callCmd.AddCommand(getNpcsCmd);

        // /call movecharacter <characterKey> <mapLabel> <x> <y> <rot>
        var moveCharCmd = new Command("movecharacter", "Move a character on the map");
        var charKeyArg = new Argument<string>("characterKey", "Character key");
        var moveMapArg = new Argument<string>("mapLabel", "Target map label");
        var moveXArg = new Argument<float>("x", "X coordinate");
        var moveYArg = new Argument<float>("y", "Y coordinate");
        var moveRotArg = new Argument<int>("rot", "Rotation");
        moveCharCmd.AddArgument(charKeyArg);
        moveCharCmd.AddArgument(moveMapArg);
        moveCharCmd.AddArgument(moveXArg);
        moveCharCmd.AddArgument(moveYArg);
        moveCharCmd.AddArgument(moveRotArg);
        moveCharCmd.SetHandler(ctx =>
        {
            try
            {
                string characterKey = ctx.ParseResult.GetValueForArgument(charKeyArg);
                string mapLabel = ctx.ParseResult.GetValueForArgument(moveMapArg);
                float x = ctx.ParseResult.GetValueForArgument(moveXArg);
                float y = ctx.ParseResult.GetValueForArgument(moveYArg);
                int rot = ctx.ParseResult.GetValueForArgument(moveRotArg);
                GameData.RunTime.DaySceneUtility.RunTimeDayScene.MoveCharacter(
                    characterKey, mapLabel, new Vector2(x, y), rot, out _);
                ctx.Log(TextId.CharacterMoved.Get(characterKey, x, y, rot, mapLabel));
            }
            catch (System.Exception e)
            {
                ctx.Log(TextId.ErrorMovecharacter.Get(e.Message));
            }
        });
        callCmd.AddCommand(moveCharCmd);

        // /call scene_move <characterKey> <x> <y>
        var sceneMoveCmd = new Command("scene_move", "Move a character in the scene");
        var sceneCharArg = new Argument<string>("characterKey", "Character key");
        var sceneXArg = new Argument<float>("x", "X coordinate");
        var sceneYArg = new Argument<float>("y", "Y coordinate");
        sceneMoveCmd.AddArgument(sceneCharArg);
        sceneMoveCmd.AddArgument(sceneXArg);
        sceneMoveCmd.AddArgument(sceneYArg);
        sceneMoveCmd.SetHandler(ctx =>
        {
            try
            {
                string characterKey = ctx.ParseResult.GetValueForArgument(sceneCharArg);
                float x = ctx.ParseResult.GetValueForArgument(sceneXArg);
                float y = ctx.ParseResult.GetValueForArgument(sceneYArg);
                var arr = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector2>(1);
                arr[0] = new Vector2(x, y);
                Common.SceneDirector.Instance.MoveCharacter(characterKey, arr, 1f, new System.Action(() => { }));
                ctx.Log(TextId.CharacterMovedScene.Get(characterKey, x, y));
            }
            catch (System.Exception e)
            {
                ctx.Log(TextId.ErrorSceneMove.Get(e.Message));
            }
        });
        callCmd.AddCommand(sceneMoveCmd);
        
        // Default handler
        callCmd.SetHandler(ctx =>
        {
            ctx.Log(ConsoleFormat.Header(TextId.CallHelpHeader.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/call getmapsnpcs", "[mapLabel]", TextId.CallDescGetmapsnpcs.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/call movecharacter", "<key> <map> <x> <y> <rot>", TextId.CallDescMovecharacter.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/call scene_move", "<key> <x> <y>", TextId.CallDescSceneMove.Get()));
            ctx.Log(ConsoleFormat.Line);
        });

        root.AddCommand(callCmd);

        CommandRegistry.RegisterCompletions("call", 0, "getmapsnpcs", "movecharacter", "scene_move");
        CommandRegistry.RegisterHint("call getmapsnpcs", 0, "[mapLabel]");
        CommandRegistry.RegisterHint("call movecharacter", 0, "<characterKey>");
        CommandRegistry.RegisterHint("call movecharacter", 1, "<mapLabel>");
        CommandRegistry.RegisterHint("call movecharacter", 2, "<x>");
        CommandRegistry.RegisterHint("call movecharacter", 3, "<y>");
        CommandRegistry.RegisterHint("call movecharacter", 4, "<rot>");
        CommandRegistry.RegisterHint("call scene_move", 0, "<characterKey>");
        CommandRegistry.RegisterHint("call scene_move", 1, "<x>");
        CommandRegistry.RegisterHint("call scene_move", 2, "<y>");
    }
}
