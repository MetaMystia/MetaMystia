using BepInEx.Configuration;
using System;
using UnityEngine;

namespace MetaMystia;

/// <summary>
/// 点单请求启用模式：强制关闭、跟随资源包、强制启用
/// </summary>
public enum RequestEnableMode
{
    ForceDisable,    // 强制关闭
    FollowPackage,   // 跟随资源包
    ForceEnable      // 强制启用
}



[AutoLog]
public static partial class ConfigManager
{
    public static ConfigFile Config => Plugin.Instance?.Config;
    public static ConfigEntry<bool> Debug;
    public static ConfigEntry<string> PlayerId;
    public static ConfigEntry<bool> SignatureCheck;
    public static ConfigEntry<RequestEnableMode> FoodRequestMode;
    public static ConfigEntry<RequestEnableMode> BevRequestMode;
    public static ConfigEntry<int> ConsoleHistorySize;
    public static ConfigEntry<string> ConsoleHistoryFile;
    public static ConfigEntry<int> MaxPlayers;
    public static ConfigEntry<int> DefaultPort;
    public static ConfigEntry<bool> EnableIPv6;
    public static ConfigEntry<string> LocaleOverride;
    public static ConfigEntry<bool> NoteBookSkinPortrait;
    public static ConfigEntry<string> SkinServerUrl;
    public static ConfigEntry<string> SkinServerToken;

    // Console layout
    public static ConfigEntry<float> ConsoleX;
    public static ConfigEntry<float> ConsoleY;
    public static ConfigEntry<float> ConsoleWidth;
    public static ConfigEntry<float> ConsoleHeight;
    public static ConfigEntry<int> ConsoleFontSize;

    // Player list layout
    public static ConfigEntry<float> PlayerListX;
    public static ConfigEntry<float> PlayerListY;
    public static ConfigEntry<int> PlayerListFontSize;

    // Keybinds
    public static ConfigEntry<KeyCode> KeyToggleLog;
    public static ConfigEntry<KeyCode> KeyToggleStatus;
    public static ConfigEntry<KeyCode> KeyOpenCommand;
    public static ConfigEntry<KeyCode> KeyOpenChat;

    public static void InitConfigs()
    {
        Debug = Config.Bind("General", "Debug", false, "Enable debug features and hotkeys\n启用调试功能和热键");

        PlayerId = Config.Bind("General", "PlayerId", "", "Player ID for multiplayer, empty to device name\n联机用玩家 ID，为空则默认为设备名称");

        SignatureCheck = Config.Bind("General", "SignatureCheck", true,
            "Enable RSA signature verification for resource pack ID ranges\n启用资源包 ID 段 RSA 签名校验");

        FoodRequestMode = Config.Bind("General", "FoodRequestMode", RequestEnableMode.FollowPackage,
            "Food request enable mode (FollowPackage by default)\n料理点单启用模式(默认跟随资源包)\n" +
            "ForceDisable: 强制关闭 | FollowPackage: 跟随资源包 | ForceEnable: 强制启用");

        BevRequestMode = Config.Bind("General", "BevRequestMode", RequestEnableMode.ForceDisable,
            "Beverage request enable mode (ForceDisable by default)\n酒水点单启用模式(默认关闭)\n" +
            "ForceDisable: 强制关闭 | FollowPackage: 跟随资源包 | ForceEnable: 强制启用");

        ConsoleHistorySize = Config.Bind("General", "ConsoleHistorySize", 200,
            "Max number of console commands to persist across sessions\n控制台命令历史记录最大保存条数");

        ConsoleHistoryFile = Config.Bind("General", "ConsoleHistoryFile", "MetaMystia_console_history.txt",
            "Filename for console command history (stored in BepInEx/config/)\n控制台命令历史文件名(存储于 BepInEx/config/)");

        MaxPlayers = Config.Bind("Multiplayer", "MaxPlayers", 2,
            new BepInEx.Configuration.ConfigDescription(
                "Maximum number of players allowed (including host)\n最大玩家数（含主机）",
                new BepInEx.Configuration.AcceptableValueRange<int>(2, int.MaxValue)));

        DefaultPort = Config.Bind("Multiplayer", "DefaultPort", 40815,
            new BepInEx.Configuration.ConfigDescription(
                "Default TCP port for hosting a server\n主机默认 TCP 端口",
                new BepInEx.Configuration.AcceptableValueRange<int>(1, 65535)));

        EnableIPv6 = Config.Bind("Multiplayer", "EnableIPv6", false,
            "Enable IPv6 dual-stack listening (IPv4 always works)\n" +
            "启用 IPv6 双栈监听（IPv4 始终可用）");

        LocaleOverride = Config.Bind("General", "LocaleOverride", "",
            "Path to a directory containing locale override JSON files (en.json, zh-CN.json).\n" +
            "Supports absolute or relative path (relative to BepInEx/plugins/).\n" +
            "翻译文件覆盖目录路径，支持绝对路径或相对路径(相对于 BepInEx/plugins/)");

        NoteBookSkinPortrait = Config.Bind("Experimental", "NoteBookSkinPortrait", false,
            "(Experimental) Enable portrait replacement for Skin System in NoteBook\n" +
            "(实验性)是否在笔记本中为皮肤系统启用立绘替换功能");

        SkinServerUrl = Config.Bind("Skin", "SkinServerUrl", "https://skin.metamystia.net",
            "Base URL of the skin distribution server. Skins are fetched from <url>/skins/<name>.png\n" +
            "皮肤分发服务器地址，皮肤会从 <url>/skins/<name>.png 拉取");

        SkinServerToken = Config.Bind("Skin", "SkinServerToken", "57443d024afa430adba6017d3a0215d4",
            "Static bearer token sent as `Authorization: Bearer <token>` to the skin server. Leave empty to disable.\n" +
            "发送给皮肤服务器的静态令牌（`Authorization: Bearer <token>`）。留空则不发送");

        // Console layout
        ConsoleX = Config.Bind("Console", "X", 8f,
            "Console panel X position (pixels from left)\n控制台面板X坐标（左侧像素偏移）");
        ConsoleY = Config.Bind("Console", "Y", -1f,
            "Console panel Y position (pixels from top, -1 = auto bottom)\n控制台面板Y坐标（顶部像素偏移，-1=自动底部）");
        ConsoleWidth = Config.Bind("Console", "Width", 800f,
            "Console panel width in pixels\n控制台面板宽度（像素）");
        ConsoleHeight = Config.Bind("Console", "Height", 350f,
            "Console panel height in pixels (log area)\n控制台面板高度（像素，日志区域）");

        ConsoleFontSize = Config.Bind("Console", "FontSize", 0,
            "Console font size (0 = auto based on screen height)\n控制台字体大小（0=根据屏幕高度自动）");

        // Player list
        PlayerListX = Config.Bind("PlayerList", "X", 8f,
            "Player list panel X position (pixels from left)\n玩家列表面板X坐标（左侧像素偏移）");
        PlayerListY = Config.Bind("PlayerList", "Y", 8f,
            "Player list panel Y position (pixels from top)\n玩家列表面板Y坐标（顶部像素偏移）");
        PlayerListFontSize = Config.Bind("PlayerList", "FontSize", 0,
            "Player list font size (0 = auto based on screen height)\n玩家列表字体大小（0=根据屏幕高度自动）");

        // Keybinds
        KeyToggleLog = Config.Bind("Keybinds", "ToggleLog", KeyCode.RightShift,
            "Key to print debug log\n打印调试日志的按键");
        KeyToggleStatus = Config.Bind("Keybinds", "ToggleStatus", KeyCode.Backslash,
            "Key to toggle status text visibility\n切换状态栏可见性的按键");
        KeyOpenCommand = Config.Bind("Keybinds", "OpenCommand", KeyCode.Slash,
            "Key to open command console (with '/' prefix)\n打开命令控制台的按键（带 '/' 前缀）");
        KeyOpenChat = Config.Bind("Keybinds", "OpenChat", KeyCode.T,
            "Key to open chat\n打开聊天的按键");
    }

    public static string GetPlayerId()
    {
        if (string.IsNullOrEmpty(PlayerId.Value))
        {
            return MpManager.SanitizePlayerId(Environment.MachineName);
        }
        if (!MpManager.IsValidPlayerId(PlayerId.Value))
        {
            var sanitized = MpManager.SanitizePlayerId(PlayerId.Value);
            Log.Warning($"PlayerId '{PlayerId.Value}' contains illegal characters, sanitized to '{sanitized}'");
            PlayerId.Value = sanitized;
        }
        return PlayerId.Value;
    }
    public static void SetPlayerId(string id)
    {
        Log.Message($"Player ID {PlayerId.Value} set to: {id}");
        PlayerId.Value = id;
    }
}
