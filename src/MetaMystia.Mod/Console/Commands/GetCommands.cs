using System.CommandLine;

using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class GetCommands
{
    public static void Register(RootCommand root)
    {
        var getCmd = new Command("get", TextId.CmdDescGet.Get());

        var fieldArg = new Argument<string>("field", TextId.ArgDescField.Get())
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
    }
}
