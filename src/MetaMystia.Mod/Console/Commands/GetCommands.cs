using System.CommandLine;
using System.CommandLine.Invocation;

using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class GetCommands
{
    public static void Register(RootCommand root)
    {
        var getCmd = new Command("get", "Query game state fields");

        var fieldArg = new Argument<string>("field", "Field to query")
            .FromAmong("currentactivemaplabel", "pos");
        getCmd.AddArgument(fieldArg);

        getCmd.SetHandler(ctx =>
        {
            string field = ctx.ParseResult.GetValueForArgument(fieldArg);
            switch (field)
            {
                case "currentactivemaplabel":
                    ctx.Log(TextId.CurrentMapLabel.Get(PlayerManager.LocalMapLabel.GetDisplayName()));
                    break;
                case "pos":
                    ctx.Log(TextId.MystiaPosition.Get(PlayerManager.LocalPosition));
                    break;
            }
        });

        root.AddCommand(getCmd);

        CommandRegistry.RegisterCompletions("get", 0, "currentactivemaplabel", "pos");
    }
}
