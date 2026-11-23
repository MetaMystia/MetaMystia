using System.CommandLine;
using UnityEngine;

using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class LinkCommands
{
    private const string MetaMystiaUrl = "https://github.com/MetaMystia/MetaMystia";
    private const string IzakayaUrl = "https://izakaya.cc/";

    public static void Register(RootCommand root)
    {
        var linkCmd = new Command("link", TextId.CmdDescLink.Get());

        var metamystiaCmd = new Command("MetaMystia", TextId.LinkDescMetaMystia.Get());
        metamystiaCmd.SetHandler(ctx =>
        {
            OpenUrl(MetaMystiaUrl);
            ctx.Log($"{ConsoleFormat.Ok(TextId.LinkDescMetaMystia.Get())} {ConsoleFormat.Dim(MetaMystiaUrl)}");
        });
        linkCmd.AddCommand(metamystiaCmd);

        var izakayaCmd = new Command("Izakaya", TextId.LinkDescIzakaya.Get());
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
    }

    private static void OpenUrl(string url)
    {
        Application.OpenURL(url);
    }
}
