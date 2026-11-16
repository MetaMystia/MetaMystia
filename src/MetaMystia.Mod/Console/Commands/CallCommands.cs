using System.CommandLine;
using System.CommandLine.Invocation;
using UnityEngine;

using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class CallCommands
{
    public static void Register(RootCommand root)
    {
        var callCmd = new Command("call", TextId.CmdDescCall.Get());

        // /call getmapsnpcs [mapLabel]
        var getNpcsCmd = new Command("getmapsnpcs", TextId.CallDescGetmapsnpcs.Get());
        var mapKeyArg = new Argument<string>("mapKey", TextId.ArgDescMapKey.Get()) { Arity = System.CommandLine.ArgumentArity.ZeroOrOne };
        mapKeyArg.SetDefaultValue("");
        getNpcsCmd.AddArgument(mapKeyArg);
        getNpcsCmd.SetHandler(ctx =>
        {
            try
            {
                string mapKeyInput = ctx.ParseResult.GetValueForArgument(mapKeyArg);
                string mapKey;
                if (string.IsNullOrEmpty(mapKeyInput))
                {
                    mapKey = PlayerManager.LocalMapLabel.ToMapKey();
                }
                else if (!MapLabelExtensions.TryFromMapKey(mapKeyInput, out _))
                {
                    ctx.Log(TextId.InvalidMapKey.Get(mapKeyInput));
                    return;
                }
                else
                {
                    mapKey = mapKeyInput;
                }

                var npcs = GameData.RunTime.DaySceneUtility.RunTimeDayScene.GetMapNPCs(mapKey);
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
        var moveCharCmd = new Command("movecharacter", TextId.CallDescMovecharacter.Get());
        var charKeyArg = new Argument<string>("characterKey", TextId.ArgDescCharacterKey.Get());
        var moveMapArg = new Argument<string>("mapKey", TextId.ArgDescMapKey.Get());
        var moveXArg = new Argument<float>("x", TextId.ArgDescX.Get());
        var moveYArg = new Argument<float>("y", TextId.ArgDescY.Get());
        var moveRotArg = new Argument<int>("rot", TextId.ArgDescRotation.Get());
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
                string mapKeyInput = ctx.ParseResult.GetValueForArgument(moveMapArg);
                if (!MapLabelExtensions.TryFromMapKey(mapKeyInput, out var mapLabel))
                {
                    ctx.Log(TextId.InvalidMapKey.Get(mapKeyInput));
                    return;
                }

                float x = ctx.ParseResult.GetValueForArgument(moveXArg);
                float y = ctx.ParseResult.GetValueForArgument(moveYArg);
                int rot = ctx.ParseResult.GetValueForArgument(moveRotArg);
                var mapKey = mapLabel.ToMapKey();
                GameData.RunTime.DaySceneUtility.RunTimeDayScene.MoveCharacter(
                    characterKey, mapKey, new Vector2(x, y), rot, out _);
                ctx.Log(TextId.CharacterMoved.Get(characterKey, x, y, rot, mapLabel.GetDisplayName()));
            }
            catch (System.Exception e)
            {
                ctx.Log(TextId.ErrorMovecharacter.Get(e.Message));
            }
        });
        callCmd.AddCommand(moveCharCmd);

        // /call scene_move <characterKey> <x> <y>
        var sceneMoveCmd = new Command("scene_move", TextId.CallDescSceneMove.Get());
        var sceneCharArg = new Argument<string>("characterKey", TextId.ArgDescCharacterKey.Get());
        var sceneXArg = new Argument<float>("x", TextId.ArgDescX.Get());
        var sceneYArg = new Argument<float>("y", TextId.ArgDescY.Get());
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

        // /call try_close_izakaya
        var tryCloseCmd = new Command("try_close_izakaya", TextId.CallDescTryCloseIzakaya.Get());
        tryCloseCmd.SetHandler(ctx =>
        {
            try
            {
                if (!Multiplayer.Messages.IzakayaCloseMessage.TryForceLocalClose())
                {
                    ctx.Log(TextId.NotInWorkScene.Get());
                    return;
                }

                ctx.Log(TextId.CalledTryCloseIzakaya.Get());
            }
            catch (System.Exception e)
            {
                ctx.Log(ConsoleFormat.Err(e.Message));
            }
        });
        callCmd.AddCommand(tryCloseCmd);

        root.AddCommand(callCmd);
    }
}
