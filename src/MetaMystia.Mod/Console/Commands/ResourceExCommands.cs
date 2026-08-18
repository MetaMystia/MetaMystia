using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;

using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class ResourceExCommands
{
    public static void Register(RootCommand root)
    {
        var resCmd = new Command("resourceex", "Resource pack management");

        // /resourceex list
        var listCmd = new Command("list", "List all loaded resource packs");
        listCmd.SetHandler(ListHandler);
        resCmd.AddCommand(listCmd);

        // /resourceex info <name>
        var infoCmd = new Command("info", "Show details of a resource pack");
        var nameArg = new Argument<string>("name", "Package file name or pack label");
        infoCmd.AddArgument(nameArg);
        infoCmd.SetHandler(ctx =>
        {
            var name = ctx.ParseResult.GetValueForArgument(nameArg);
            InfoHandler(ctx, name);
        });
        resCmd.AddCommand(infoCmd);

        // Default: show help
        resCmd.SetHandler(ResourceExHelpHandler);

        root.AddCommand(resCmd);

        // Register tab completions
        CommandRegistry.RegisterCompletions("resourceex", 0, "list", "info");

        // Register dynamic completions for info subcommand — package names
        var packageNames = ResourceExManager.LoadedPackages
            .Select(p => p.Config?.packInfo?.label ?? p.PackageName)
            .ToArray();
        if (packageNames.Length > 0)
            CommandRegistry.RegisterCompletions("resourceex info", 0, packageNames);
    }

    private static void ResourceExHelpHandler(InvocationContext ctx)
    {
        ctx.Log(ConsoleFormat.Header(TextId.ResourceExHelpHeader.Get()));
        ctx.Log(ConsoleFormat.SubCmd("list", "", TextId.ResourceExDescList.Get()));
        ctx.Log(ConsoleFormat.SubCmd("info", "<name>", TextId.ResourceExDescInfo.Get()));
        ctx.Log(ConsoleFormat.Line);
    }

    private static void ListHandler(InvocationContext ctx)
    {
        var packages = ResourceExManager.LoadedPackages;

        if (packages.Count == 0)
        {
            ctx.Log(ConsoleFormat.Warn(TextId.ResourceExListEmpty.Get()));
        }
        else
        {
            ctx.Log(ConsoleFormat.Header(TextId.ResourceExListHeader.Get(packages.Count)));
            foreach (var pkg in packages)
            {
                var info = pkg.Config?.packInfo;
                if (info != null)
                {
                    string name = info.name ?? pkg.PackageName;
                    string version = info.version ?? "?";
                    string authors = info.authors != null ? string.Join(", ", info.authors) : "Unknown";
                    ctx.Log($"  {ConsoleFormat.Ok("●")} {ConsoleFormat.Cmd(name)} {ConsoleFormat.Dim("v" + version)} {ConsoleFormat.Dim("by " + authors)}");
                }
                else
                {
                    ctx.Log($"  {ConsoleFormat.Ok("●")} {ConsoleFormat.Cmd(pkg.PackageName)} {ConsoleFormat.Dim("(no pack info)")}");
                }
            }
        }

        // Show rejected packages if any
        var rejected = ResourceExManager.RejectedPackages;
        if (rejected.Count > 0)
        {
            ctx.Log(ConsoleFormat.Header(TextId.ResourceExRejectedHeader.Get(rejected.Count)));
            foreach (var (name, reason) in rejected)
            {
                ctx.Log($"  {ConsoleFormat.Err("✗")} {ConsoleFormat.Err(name)} {ConsoleFormat.Dim("— " + reason)}");
            }
        }

        ctx.Log(ConsoleFormat.Line);
    }

    private static void InfoHandler(InvocationContext ctx, string name)
    {
        var pkg = ResourceExManager.LoadedPackages.FirstOrDefault(p =>
            string.Equals(p.PackageName, name, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Config?.packInfo?.label, name, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Config?.packInfo?.name, name, System.StringComparison.OrdinalIgnoreCase));

        if (pkg == null)
        {
            ctx.Log(ConsoleFormat.Warn(TextId.ResourceExInfoNotFound.Get(name)));
            return;
        }

        var info = pkg.Config?.packInfo;
        ctx.Log(ConsoleFormat.Header(TextId.ResourceExInfoHeader.Get()));

        ctx.Log($"  {TextId.ResourceExInfoName.Get(ConsoleFormat.Cmd(info?.name ?? pkg.PackageName))}");

        if (!string.IsNullOrEmpty(info?.label))
            ctx.Log($"  {TextId.ResourceExInfoLabel.Get(ConsoleFormat.Arg(info.label))}");

        if (!string.IsNullOrEmpty(info?.version))
            ctx.Log($"  {TextId.ResourceExInfoVersion.Get(info.version)}");

        string authors = info?.authors != null ? string.Join(", ", info.authors) : "Unknown";
        ctx.Log($"  {TextId.ResourceExInfoAuthors.Get(authors)}");

        if (!string.IsNullOrEmpty(info?.description))
            ctx.Log($"  {TextId.ResourceExInfoDescription.Get(info.description)}");

        if (!string.IsNullOrEmpty(info?.license))
            ctx.Log($"  {TextId.ResourceExInfoLicense.Get(info.license)}");

        if (info?.idRangeStart != null && info?.idRangeEnd != null)
            ctx.Log($"  {TextId.ResourceExInfoIdRange.Get(info.idRangeStart, info.idRangeEnd)}");

        // Show content summary
        var config = pkg.Config;
        var parts = new List<string>();
        if (config?.characters?.Count > 0) parts.Add($"Characters: {config.characters.Count}");
        if (config?.ingredients?.Count > 0) parts.Add($"Ingredients: {config.ingredients.Count}");
        if (config?.foods?.Count > 0) parts.Add($"Foods: {config.foods.Count}");
        if (config?.beverages?.Count > 0) parts.Add($"Beverages: {config.beverages.Count}");
        if (config?.recipes?.Count > 0) parts.Add($"Recipes: {config.recipes.Count}");
        if (config?.dialogPackages?.Count > 0) parts.Add($"Dialogs: {config.dialogPackages.Count}");
        if (config?.missionNodes?.Count > 0) parts.Add($"Missions: {config.missionNodes.Count}");
        if (config?.eventNodes?.Count > 0) parts.Add($"Events: {config.eventNodes.Count}");
        if (config?.merchants?.Count > 0) parts.Add($"Merchants: {config.merchants.Count}");
        if (config?.clothes?.Count > 0) parts.Add($"Clothes: {config.clothes.Count}");

        if (parts.Count > 0)
            ctx.Log($"  {TextId.ResourceExInfoContents.Get(ConsoleFormat.Dim(string.Join(", ", parts)))}");

        ctx.Log(ConsoleFormat.Line);
    }
}
