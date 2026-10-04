using System;

using System.CommandLine;

using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class LinkCommands
{
    private const string MetaMystiaUrl = "https://github.com/MetaMystia/MetaMystia";
    private const string IzakayaUrl = "https://izakaya.cc/";

    public static void Register(RootCommand root)
    {
        var linkCmd = new Command("link", "Open project links in browser");

        var metamystiaCmd = new Command("MetaMystia", "MetaMystia GitHub repository");
        metamystiaCmd.SetHandler(ctx =>
        {
            OpenUrl(MetaMystiaUrl);
            ctx.Log($"{ConsoleFormat.Ok(TextId.LinkDescMetaMystia.Get())} {ConsoleFormat.Dim(MetaMystiaUrl)}");
        });
        linkCmd.AddCommand(metamystiaCmd);

        var izakayaCmd = new Command("Izakaya", "Touhou Mystia Izakaya Helper");
        izakayaCmd.SetHandler(ctx =>
        {
            OpenUrl(IzakayaUrl);
            ctx.Log($"{ConsoleFormat.Ok(TextId.LinkDescIzakaya.Get())} {ConsoleFormat.Dim(IzakayaUrl)}");
        });
        linkCmd.AddCommand(izakayaCmd);

        // Default: show MetaMystia link
        linkCmd.SetHandler(ctx =>
        {
            OpenUrl(MetaMystiaUrl);
            ctx.Log($"{ConsoleFormat.Ok(TextId.LinkDescMetaMystia.Get())} {ConsoleFormat.Dim(MetaMystiaUrl)}");
        });

        root.AddCommand(linkCmd);

        CommandRegistry.RegisterCompletions("link", 0, "MetaMystia", "Izakaya");
    }

    /// <summary>经框架打开外链：框架只接受绝对 http/https，其余拒绝，因此这里把拒绝变成一条控制台报错。</summary>
    private static void OpenUrl(string url)
    {
        try
        {
            ModRuntime.CommonServices.OpenUrl(url);
        }
        catch (ArgumentException e)
        {
            InGameConsole.LogError(e.Message);
        }
    }
}
