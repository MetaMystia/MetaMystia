using System.CommandLine;

using MetaMystia.Network.Services;
using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class SkinCommands
{
    public static void Register(RootCommand root)
    {
        var skinCmd = new Command("skin", "Character skin management");

        // /skin set <characterId> <type> <skinIndex>
        var setCmd = new Command("set", "Set character skin");
        var charIdArg = new Argument<int>("characterId", "Character ID");
        var typeArg = new Argument<string>("type", "Skin type: Default, Explicit, or DLC")
            .FromAmong("Default", "Explicit", "DLC");
        var skinIndexArg = new Argument<int>("skinIndex", "Skin index");
        setCmd.AddArgument(charIdArg);
        setCmd.AddArgument(typeArg);
        setCmd.AddArgument(skinIndexArg);
        setCmd.SetHandler(ctx =>
        {
            int characterId = ctx.ParseResult.GetValueForArgument(charIdArg);
            string typeStr = ctx.ParseResult.GetValueForArgument(typeArg);
            int skinIndex = ctx.ParseResult.GetValueForArgument(skinIndexArg);

            if (!System.Enum.TryParse<GameData.Core.Collections.CharacterUtility.CharacterSkinSets.SelectedType>(
                    typeStr, true, out var selectedType))
            {
                ctx.Log(TextId.SkinMsgInvalidType.Get(typeStr));
                return;
            }

            PlayerManager.Local.Skin.SetSkin(characterId, selectedType, skinIndex);
            PlayerManager.Local.IsCustomSkinOverride = true;
            PlayerManager.Local.UpdateCharacterSprite();
            if (MpManager.CanSeeOnlinePlayers)
                CommonServices.SendPlayerChangeSkin(PlayerManager.Local.Skin);
            PlayerManager.RefreshPortrait();
            ctx.Log(TextId.SkinMsgSetOk.Get(characterId, selectedType, skinIndex));
        });
        skinCmd.AddCommand(setCmd);

        // /skin off
        var offCmd = new Command("off", "Reset skin to game default");
        offCmd.SetHandler(ctx =>
        {
            PlayerManager.Local.IsCustomSkinOverride = false;
            PlayerManager.InitLocalSkin();
            PlayerManager.Local.UpdateCharacterSprite();
            if (MpManager.CanSeeOnlinePlayers)
                CommonServices.SendPlayerChangeSkin(PlayerManager.Local.Skin);
            PlayerManager.RefreshPortrait();
            ctx.Log(TextId.SkinMsgResetOk.Get());
        });
        skinCmd.AddCommand(offCmd);

        // /skin net <name> | /skin net off
        var netCmd = new Command("net", "Use a network skin from the skin station");
        var netNameArg = new Argument<string>("name", "Net skin name (or 'off' to clear)");
        netCmd.AddArgument(netNameArg);
        netCmd.SetHandler(ctx =>
        {
            var name = ctx.ParseResult.GetValueForArgument(netNameArg);
            if (string.Equals(name, "off", System.StringComparison.OrdinalIgnoreCase))
            {
                PlayerManager.Local.Skin.SetNetSkin(null);
                PlayerManager.Local.IsCustomSkinOverride = false;
                PlayerManager.InitLocalSkin();
                PlayerManager.Local.UpdateCharacterSprite();
                if (MpManager.CanSeeOnlinePlayers)
                    CommonServices.SendPlayerChangeSkin(PlayerManager.Local.Skin);
                PlayerManager.RefreshPortrait();
                ctx.Log(TextId.SkinMsgNetClearOk.Get());
                return;
            }

            if (string.Equals(name, "refresh", System.StringComparison.OrdinalIgnoreCase))
            {
                var current = PlayerManager.Local.Skin.NetSkinName;
                if (string.IsNullOrEmpty(current))
                {
                    ctx.Log(TextId.SkinMsgNetRefreshNoSkin.Get());
                    return;
                }
                NetSkinManager.Invalidate(current);
                NetSkinManager.RequestSkin(current, ok =>
                {
                    ctx.Log(ok ? TextId.SkinMsgNetLoaded.Get(current) : TextId.SkinMsgNetFailed.Get(current));
                });
                PlayerManager.Local.UpdateCharacterSprite();
                PlayerManager.RefreshPortrait();
                ctx.Log(TextId.SkinMsgNetRefreshing.Get(current));
                return;
            }

            if (!NetSkinManager.IsValidName(name))
            {
                ctx.Log(TextId.SkinMsgInvalidName.Get(name));
                return;
            }

            PlayerManager.Local.Skin.SetNetSkin(name);
            PlayerManager.Local.IsCustomSkinOverride = true;
            // 先应用 Fallback 占位，下载完成后 NetSkinManager 会自动重新刷新
            PlayerManager.Local.UpdateCharacterSprite();
            if (MpManager.CanSeeOnlinePlayers)
                CommonServices.SendPlayerChangeSkin(PlayerManager.Local.Skin);
            PlayerManager.RefreshPortrait();

            NetSkinManager.RequestSkin(name, ok =>
            {
                ctx.Log(ok ? TextId.SkinMsgNetLoaded.Get(name) : TextId.SkinMsgNetFailed.Get(name));
            });
            ctx.Log(TextId.SkinMsgNetRequesting.Get(name));
        });
        skinCmd.AddCommand(netCmd);

        // /skin list
        var listCmd = new Command("list", "List all available skins");
        listCmd.SetHandler(ctx =>
        {
            ctx.Log(PlayerSkin.GetAllSkinsTable());
        });
        skinCmd.AddCommand(listCmd);

        // Default handler
        skinCmd.SetHandler(ctx =>
        {
            ctx.Log(ConsoleFormat.Header(TextId.SkinHelpHeader.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/skin set", "<charId> <Default|Explicit|DLC> <skinIdx>", TextId.SkinDescSet.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/skin net", "<name|off|refresh>", TextId.SkinDescNet.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/skin off", null, TextId.SkinDescOff.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/skin list", null, TextId.SkinDescList.Get()));
            ctx.Log(ConsoleFormat.Line);
        });

        root.AddCommand(skinCmd);

        CommandRegistry.RegisterCompletions("skin", 0, "set", "net", "off", "list");
        CommandRegistry.RegisterCompletions("skin set", 1, "Default", "Explicit", "DLC");
        CommandRegistry.RegisterHint("skin set", 0, "<characterId>");
        CommandRegistry.RegisterHint("skin set", 2, "<skinIndex>");
        CommandRegistry.RegisterHint("skin net", 0, "<name|off|refresh>");
    }
}
