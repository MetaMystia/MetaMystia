using System;
using System.CommandLine;

using MetaMystia.Multiplayer.Messages;
using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class EmoteCommands
{
    private static readonly string[] Names = ["fire", "shake", "jump", "henshin", "sparkle", "smoke"];

    public static void Register(RootCommand root)
    {
        var emoteCmd = new Command("emote", TextId.CmdDescEmote.Get());
        var nameArg = new Argument<string>("name").FromAmong(Names);
        emoteCmd.AddArgument(nameArg);
        emoteCmd.SetHandler(ctx =>
        {
            var emote = Enum.Parse<EmoteType>(ctx.ParseResult.GetValueForArgument(nameArg), true);
            var unit = PlayerManager.Local.GetCharacterUnit();
            if (unit == null || GameFlow.InStory)
            {
                ctx.Log(ConsoleFormat.Err(TextId.EmoteNoCharacter.Get()));
                return;
            }
            if (!Emote.IsAvailable(emote))
            {
                ctx.Log(ConsoleFormat.Err(TextId.EmoteUnavailable.Get()));
                return;
            }
            if (Emote.Play(unit, emote)) EmoteMessage.Send(emote);
        });
        root.AddCommand(emoteCmd);

        CommandRegistry.RegisterCompletions("emote", 0, Names);
    }
}
