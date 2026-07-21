using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using UnityEngine.UI;

namespace MetaMystia.UI;

public enum Language
{
    ChineseSimplified,
    English,
}

public enum TextId
{
    // Multiplayer Connection & Commands
    ConnectCommand,
    ConnectCommandConnected,
    ConnectCommandFail,

    // Multiplayer Usage & Help
    MpUsageHelp,
    MpUsageRoot,
    MpSubcommandHelp,
    MpUsageSetId,

    // Multiplayer Status & Responses
    MpAlreadyStarted,
    MpStartedAsHost,
    MpSwitchingToHost,
    MpStopped,
    MpRestarted,
    MpPlayerIdSet,
    MpPlayerIdInvalid,
    PeerPlayerIdChanged,
    MpNoActiveConnection,
    MpConnecting,
    MpDisconnected,
    MpUnknownSubcommand,

    // Network Error Messages
    ModVersionMismatch,
    GameVersionMismatch,
    GameResourcesNotLoaded,
    MpMainSceneRequired,
    SceneMismatchDisconnected,

    // Connection Status Notifications
    MultiplayerConnected,
    MultiplayerDisconnected,
    MpConnected,
    PeerJoined,
    PeerLeft,
    ChallengeWarning,

    // Peer Disconnect & Continue
    PeerDisconnectedAllReady,
    PeerDisconnectedWaiting,
    MpContinueHostOnly,
    MpKickHostOnly,
    MpKickNoTarget,
    MpKickSelf,
    MpKickNotFound,
    MpKickSuccess,
    MpContinueUsage,
    MpContinueSuccess,
    MpContinueFailed,
    PrepWorkReconnectBlocked,

    // Business/Shop Related
    TodayBusinessHours,
    PeerAlreadyInScene,
    SelectedIzakaya,
    SelectedIzakayaMismatch,
    WaitingForHostConfirm,
    PeerSelectedIzakaya,
    MystiaReadyForWork,
    ReadyForWork,
    AllReadyTransition,
    PeerClosedIzakaya,

    // Chat System
    ChatMessagePeer,
    ChatMessageSelf,

    // Version & Update Info
    ModVersionUnavailable,
    ModVersionLatest,
    ModVersionOutdated,

    // Resource Management
    SignatureVerificationDisabled,
    ResourcePackageValidationFailed,
    ResourcePackageLoaded,

    // DLC Availability Checks
    DLCPeerRecipeNotAvailable,
    DLCPeerBeverageNotAvailable,
    DLCPeerFoodNotAvailable,
    DLCPeerIngredientNotAvailable,
    DLCPeerCookerNotAvailable,

    // System Messages
    ModPatchFailure,

    // Console UI & Formatting
    PeerMessagePrefix,
    CommandPrompt,
    BepInExConsoleEnabled,

    // Console Command Help & Usage
    AvailableCommands,
    GetUsage,
    AvailableFields,
    CallUsage,
    AvailableMethods,
    MoveCharacterUsage,
    SceneMoveUsage,
    WebDebuggerUsage,

    // Console Status & Queries
    NotInDayScene,
    NotInWorkScene,
    MapInfoDisplay,
    CurrentMapLabel,
    MystiaPosition,
    UnknownField,

    // Map labels
    MapLabel_Unknown,
    MapLabel_Home,
    MapLabel_Basement,
    MapLabel_BeastForest,
    MapLabel_HumanVillage,
    MapLabel_HakureiShrine,
    MapLabel_ScarletMansion,
    MapLabel_BambooForest,
    MapLabel_PartyStage,
    MapLabel_Hakugyokurou,
    MapLabel_DLC1_MagicForest,
    MapLabel_DLC1_YoukaiMountain,
    MapLabel_DLC2_FormerHell,
    MapLabel_DLC2_EarthSpiritsPalace,
    MapLabel_DLC3_MyourenTemple,
    MapLabel_DLC3_DivineSpiritMausoleum,
    MapLabel_DLC3_HakureiFestival,
    MapLabel_DLC4_GardenOfTheSun,
    MapLabel_DLC4_ShiningNeedleCastle,
    MapLabel_DLC4_ScarletMansionBasement,
    MapLabel_DLC5_Makai,
    MapLabel_DLC5_LunarCapital,

    // Izakaya map levels
    MapLevel_Cart,
    MapLevel_Cabin,
    MapLevel_Izakaya,
    MapLevel_Unknown,
    PeerIzakayaNotSelected,
    InvalidMapKey,

    // Console Error Messages
    UnknownCommand,
    UnknownMethod,
    ErrorGetMapsnpcs,
    ErrorMovecharacter,
    ErrorSceneMove,
    InvalidWebDebuggerKey,

    // Console Results & Feedback
    MessageSent,
    NPCListItem,
    TotalNPCsFound,
    CharacterMoved,
    CharacterMovedScene,
    CalledTryCloseIzakaya,
    WebDebuggerStarted,

    // Skin Commands
    SkinUsage,
    SkinEnabled,
    SkinDisabled,
    SkinAlreadyDisabled,
    SkinSetMystia,
    SkinSetNpc,
    SkinInvalidType,
    SkinInvalidIndex,
    SkinNpcNotFound,
    SkinListHeader,
    SkinListItem,
    SkinListNpcHint,
    SkinStatus,
    SkinStatusDisabled,

    // Help & Command Descriptions (L10N)
    HelpHeader,
    CmdDescHelp,
    CmdDescClear,
    CmdDescGet,
    CmdDescMp,
    CmdDescCall,
    CmdDescSkin,
    CmdDescDebug,
    CmdDescWebdebug,
    CmdDescWhereami,
    CmdDescEnableBepinConsole,

    // Subcommand help headers
    MpHelpHeader,
    CallHelpHeader,
    SkinHelpHeader,

    // Subcommand descriptions (short)
    MpDescStart,
    MpDescStop,
    MpDescRestart,
    MpDescStatus,
    MpDescId,
    MpDescConnect,
    MpDescDisconnect,
    MpDescKick,
    MpDescKickId,
    MpDescKickUid,
    MpDescContinue,
    CallDescGetmapsnpcs,
    CallDescMovecharacter,
    CallDescSceneMove,
    CallDescTryCloseIzakaya,
    SkinDescSet,
    SkinDescNet,
    SkinDescRot,
    SkinDescOff,
    SkinDescList,

    // Skin command runtime messages
    SkinMsgInvalidType,
    SkinMsgSetOk,
    SkinMsgResetOk,
    SkinMsgNetClearOk,
    SkinMsgInvalidName,
    SkinMsgNetRequesting,
    SkinMsgNetRefreshing,
    SkinMsgNetRefreshNoSkin,
    SkinMsgNetLoaded,
    SkinMsgNetFailed,
    SkinMsgRotOn,
    SkinMsgRotOff,
    SkinMsgRotClear,

    // Console startup & link
    ConsoleStarPrompt,
    ConsoleHelpHint,
    CmdDescLink,
    LinkDescMetaMystia,
    LinkDescIzakaya,

    // ResourceEx Console Messages & Commands
    ResourceExConsoleLoaded,
    ResourceExConsoleLoadedNoInfo,
    ResourceExConsoleRejected,
    CmdDescResourceEx,
    ResourceExHelpHeader,
    ResourceExDescList,
    ResourceExDescInfo,
    ResourceExListHeader,
    ResourceExListItem,
    ResourceExListEmpty,
    ResourceExInfoHeader,
    ResourceExInfoName,
    ResourceExInfoLabel,
    ResourceExInfoVersion,
    ResourceExInfoAuthors,
    ResourceExInfoDescription,
    ResourceExInfoLicense,
    ResourceExInfoIdRange,
    ResourceExInfoContents,
    ResourceExInfoNotFound,
    ResourceExRejectedHeader,
    ResourceExRejectedItem,

    // Version Mismatch
    GameVersionMismatchNotify,

    // Il2CppInterop Patch
    Il2CppInteropPatchedRestartRequired,

    // Max Players / Reject
    RoomFull,
    RoomFullHostNotify,
    DuplicatePeerId,
    DuplicatePeerIdHostNotify,
    MpMaxPlayersCurrent,
    MpMaxPlayersSet,
    MpMaxPlayersHostOnly,
    MpMaxPlayersRange,
    MpDescMaxPlayers,
    MpStartDeprecated,
    MpStartedOnPort,
    MpPortRange,
    MpConnectInProgress,

    // IPv6
    MpIpv6Enabled,
    MpIpv6Disabled,
    MpIpv6Restarted,
    MpIpv6RejectConnected,
    MpDescIpv6,

    // Live streaming mode
    CmdDescLive,
    LiveUsage,
    LiveEmergencyFull,
    LiveInvalidMode,
    LiveModeOff,
    LiveModePartial,
    LiveModeFull,
    LivePartialReminder,
}

public static class L10n
{
    private static Dictionary<TextId, Dictionary<Language, string>> Table = new();

    /// <summary>
    /// Load translations from embedded JSON resources.
    /// Call once during plugin initialization.
    /// </summary>
    public static void Initialize()
    {
        var asm = Assembly.GetExecutingAssembly();
        LoadLanguageFromResource(asm, Language.English, "MetaMystia.UI.Locales.en.json");
        LoadLanguageFromResource(asm, Language.ChineseSimplified, "MetaMystia.UI.Locales.zh-CN.json");
    }

    private static void LoadLanguageFromResource(Assembly asm, Language lang, string resourceName)
    {
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Plugin.Instance?.Log.LogWarning($"L10n resource not found: {resourceName}");
            return;
        }
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        MergeJson(lang, reader.ReadToEnd());
    }

    private static void LoadLanguageFromFile(Language lang, string path, bool warnIfMissing = false)
    {
        if (!File.Exists(path))
        {
            if (warnIfMissing)
                Plugin.Instance?.Log.LogWarning($"L10n override not found: {path}");
            return;
        }
        try
        {
            MergeJson(lang, File.ReadAllText(path, System.Text.Encoding.UTF8));
            Plugin.Instance?.Log.LogInfo($"L10n override loaded ({lang}): {path}");
        }
        catch (Exception ex)
        {
            Plugin.Instance?.Log.LogWarning($"L10n override failed: {path} — {ex.Message}");
        }
    }

    private static string ResolveLocalePath(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var path = raw.Trim().Trim('"');
        if (!Path.IsPathRooted(path))
        {
            var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            path = Path.Combine(pluginDir, path);
        }
        return path;
    }

    private static void LoadLocaleOverride()
    {
        var path = ResolveLocalePath(ConfigManager.LocaleOverride?.Value);
        if (path == null) return;

        if (File.Exists(path))
        {
            LoadLanguageFromFile(Language, path, warnIfMissing: true);
            return;
        }

        if (Directory.Exists(path))
        {
            LoadLanguageFromFile(Language.English, Path.Combine(path, "en.json"));
            LoadLanguageFromFile(Language.ChineseSimplified, Path.Combine(path, "zh-CN.json"));
            return;
        }

        Plugin.Instance?.Log.LogWarning($"L10n override path not found: {path}");
    }

    private static void MergeJson(Language lang, string json)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (dict == null) return;

        foreach (var (key, text) in dict)
        {
            if (!Enum.TryParse<TextId>(key, out var textId)) continue;
            if (!Table.ContainsKey(textId))
                Table[textId] = new Dictionary<Language, string>();
            Table[textId][lang] = text;
        }
    }

    public static void PostInitializeTable()
    {
        PostInitialized = true;
        LoadLocaleOverride();
    }

    public static string Get(this TextId key, params object[] args)
    {
        if (!Table.TryGetValue(key, out var langMap))
            return $"[L10N_MISSING:{key}]";

        if (!langMap.TryGetValue(Language, out var text) || string.IsNullOrEmpty(text))
            text = langMap.GetValueOrDefault(Language.ChineseSimplified);
        if (string.IsNullOrEmpty(text))
            text = langMap.GetValueOrDefault(Language.English);
        if (string.IsNullOrEmpty(text))
            return $"[L10N_MISSING:{key}]";

        return args.Length > 0
            ? string.Format(text, args)
            : text;
    }

    public static Language GetLanguage(this GameData.MultiLanguageTextMesh.LoadLanguageType loadLanguageType) => loadLanguageType switch
    {
        GameData.MultiLanguageTextMesh.LoadLanguageType.Chinese => Language.ChineseSimplified,
        GameData.MultiLanguageTextMesh.LoadLanguageType.English => Language.English,
        GameData.MultiLanguageTextMesh.LoadLanguageType.CNT => Language.ChineseSimplified,
        _ => Language.English,
    };

    public static Language Language
    {
        get
        {
            return PostInitialized ? Common.UI.EscapeUtility.EscConfigPannel.CurrentSettings.CurrentLanguage.GetLanguage() : Language.English;
        }
    }
    private static bool PostInitialized = false;
}
