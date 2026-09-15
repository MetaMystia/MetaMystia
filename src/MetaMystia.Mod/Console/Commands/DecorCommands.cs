using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;

using Il2CppSystem.Collections.Generic;

using GameData.Core.Collections;
using GameData.RunTime.Common;

using MetaMystia.UI;
using SgrYuki.Utils;

namespace MetaMystia.ConsoleSystem.Commands;

/// <summary>
/// 装饰品测试命令：解锁（进入物品范围）、勾选使用、列出已注册装饰。
/// 仅用于开发期验证展示柜注册链路，不写入存档长期逻辑。
/// </summary>
public static class DecorCommands
{
    /// <summary>
    /// 注册装饰品测试命令及其子命令。
    /// </summary>
    /// <param name="root">根命令。</param>
    public static void Register(RootCommand root)
    {
        var decorCmd = new Command("decor", "Decoration test commands (dev only)");

        // /decor unlock <id>  —— 仅进入玩家物品范围，使装饰在展示柜可见
        var unlockCmd = new Command("unlock", "Add a decoration to player's item range (visible in showcase)");
        var unlockIdArg = new Argument<int>("id", "Decoration ID");
        unlockCmd.AddArgument(unlockIdArg);
        unlockCmd.SetHandler(ctx =>
        {
            int id = ctx.ParseResult.GetValueForArgument(unlockIdArg);
            if (!id.IsDecoration())
            {
                ctx.Log(ConsoleFormat.Err($"Decoration {id} is not registered."));
                return;
            }
            var idList = new List<int>(1);
            idList.Add(id);
            RunTimeStorage.ItemInRange(idList.ToIEnumerable());
            ctx.Log(ConsoleFormat.Ok($"Decoration {id} unlocked (now visible in showcase)."));
        });
        decorCmd.AddCommand(unlockCmd);

        // /decor use <id>  —— 解锁并勾选为已使用（夜间会触发其效果）
        var useCmd = new Command("use", "Unlock and mark a decoration as used (active at night)");
        var useIdArg = new Argument<int>("id", "Decoration ID");
        useCmd.AddArgument(useIdArg);
        useCmd.SetHandler(ctx =>
        {
            int id = ctx.ParseResult.GetValueForArgument(useIdArg);
            if (!id.IsDecoration())
            {
                ctx.Log(ConsoleFormat.Err($"Decoration {id} is not registered."));
                return;
            }
            var idList = new List<int>(1);
            idList.Add(id);
            RunTimeStorage.ItemInRange(idList.ToIEnumerable());
            RunTimeAlbum.TryRecordUsedDecoration(id);
            ctx.Log(ConsoleFormat.Ok($"Decoration {id} unlocked and marked as used."));
        });
        decorCmd.AddCommand(useCmd);

        // /decor list  —— 列出所有已注册装饰 id 与名称
        var listCmd = new Command("list", "List all registered decorations");
        listCmd.SetHandler(ListHandler);
        decorCmd.AddCommand(listCmd);

        decorCmd.SetHandler(ctx =>
        {
            ctx.Log(ConsoleFormat.Cmd("/decor") + " " + ConsoleFormat.Dim("unlock|use|list <id>"));
        });
        root.AddCommand(decorCmd);

        CommandRegistry.RegisterCompletions("decor", 0, "unlock", "use", "list");
    }

    /// <summary>
    /// 列出所有已注册装饰及其持有与装备状态。
    /// </summary>
    /// <param name="ctx">命令调用上下文。</param>
    private static void ListHandler(InvocationContext ctx)
    {
        var ids = DataBaseCore.GetAllDecorations().ToIl2CppList();
        var decorations = new System.Collections.Generic.List<Decoration>();
        for (int i = 0; i < ids.Count; i++)
        {
            decorations.Add(ids[i].RefDecorations());
        }
        if (decorations.Count == 0)
        {
            ctx.Log(ConsoleFormat.Warn("No decorations registered."));
            return;
        }
        ctx.Log(ConsoleFormat.Header($"Registered decorations ({decorations.Count})"));
        foreach (var deco in decorations.OrderBy(d => d.Id))
        {
            bool owned = RunTimeStorage.ContainsItem(deco.Id);
            bool used = RunTimeAlbum.HasDecorationUsing(deco.Id);
            string status = used ? ConsoleFormat.Ok("equipped")
                : owned ? ConsoleFormat.Dim("owned")
                : ConsoleFormat.Dim("locked");
            ctx.Log($"  #{deco.Id}  {status}  {deco.GetType().Name}");
        }
        ctx.Log(ConsoleFormat.Line);
    }
}
