using System.Linq;

using Common.UI;
using DayScene.UI;
using GameData;

using MetaMystia.ResourceEx.Registries;
using MetaMystia.UI;
using SgrYuki.Utils;

using static MetaMystia.UI.DaySceneSelectionMenu;

namespace MetaMystia;

public static class StoryReplayManager
{
    private static MultiLanguageTextMesh.LoadLanguageType CurrentLanguage =>
        Common.UI.EscapeUtility.EscConfigPannel.CurrentSettings.CurrentLanguage;

    private static string CollabMenuTitle => CurrentLanguage switch
    {
        MultiLanguageTextMesh.LoadLanguageType.Chinese => "剧情回放(MetaMystia)",
        MultiLanguageTextMesh.LoadLanguageType.CNT => "劇情回放(MetaMystia)",
        MultiLanguageTextMesh.LoadLanguageType.Japanese => "ストーリー再生(MetaMystia)",
        MultiLanguageTextMesh.LoadLanguageType.Korean => "스토리 리플레이(MetaMystia)",
        _ => "Story Replay (MetaMystia)",
    };

    private static string RecentPackTitle => CurrentLanguage switch
    {
        MultiLanguageTextMesh.LoadLanguageType.Chinese => "最近阅读",
        MultiLanguageTextMesh.LoadLanguageType.CNT => "最近閱讀",
        MultiLanguageTextMesh.LoadLanguageType.Japanese => "最近読んだ会話",
        MultiLanguageTextMesh.LoadLanguageType.Korean => "최근 읽은 대화",
        _ => "Recently Read",
    };

    public static void Test() => OpenReplayMenu();

    public static void OpenReplayMenu()
    {
        StoryReplayIndex.Rebuild();
        if (StoryReplayIndex.Packs.Count == 0)
        {
            Log.Warning("No dialog to replay");
            return;
        }

        OpenPackMenu();
    }

    private static readonly DaySceneSelectionItem CollabMenuItem = new();

    internal static DaySceneChatSelectionPannel.GetSelectionConfigurationCallback CollabMenuSelection => CollabMenuItem.Set(
        () => CollabMenuTitle,
        () =>
        {
            StoryReplayIndex.Rebuild();
            return StoryReplayIndex.Packs.Count > 0;
        },
        OpenReplayMenu);

    private static void OpenPackMenu()
    {
        OpenSelectionMenu(
            BuildSelectionItems(
                StoryReplayIndex.Packs,
                GetPackTitle,
                IsPackAvailable,
                OpenPackContent),
            CloseEndButton);
    }

    private static string GetPackTitle(string pack) =>
        pack == StoryReplayIndex.RecentPack ? RecentPackTitle : pack;

    private static bool IsPackAvailable(string pack) => pack switch
    {
        StoryReplayIndex.RecentPack => StoryReplayRecentHistory.Dialogs.Count > 0,
        "ResourceEx" => StoryReplayIndex.GetCategories("ResourceEx").Any(pkg =>
            StoryReplayIndex.GetDialogs("ResourceEx", pkg).Any(StoryReplayIndex.IsDialogAvailable)),
        _ => StoryReplayIndex.GetCategories(pack).Count > 0,
    };

    private static void OpenPackContent(string pack)
    {
        if (pack == StoryReplayIndex.RecentPack)
            OpenRecentDialogMenu();
        else
            OpenCategoryMenu(pack);
    }

    private static void OpenRecentDialogMenu()
    {
        var dialogs = StoryReplayIndex.GetRecentDialogs();
        if (dialogs.Count == 0)
        {
            Log.Warning("No recently read dialog");
            return;
        }

        OpenSelectionMenu(
            BuildSelectionItems(
                dialogs,
                StoryReplayIndex.GetDialogDisplayTitle,
                _ => true,
                PlayDialog),
            BackTo(() => OpenPackMenu()));
    }

    private static void OpenCategoryMenu(string pack)
    {
        if (pack == "ResourceEx")
        {
            OpenResourceExPackageMenu();
            return;
        }

        var categories = StoryReplayIndex.GetCategories(pack);
        if (categories.Count == 0)
        {
            Log.Warning($"[{pack}] No category available");
            return;
        }

        OpenSelectionMenu(
            BuildSelectionItems(
                categories,
                title => title,
                _ => true,
                category => OpenGroupMenu(pack, category)),
            BackTo(() => OpenPackMenu()));
    }

    private static void OpenResourceExPackageMenu()
    {
        var packages = StoryReplayIndex.GetCategories("ResourceEx");
        if (packages.Count == 0)
        {
            Log.Warning("No ResourceEx dialog");
            return;
        }

        OpenSelectionMenu(
            BuildSelectionItems(
                packages,
                title => title,
                pkg => StoryReplayIndex.GetDialogs("ResourceEx", pkg).Any(StoryReplayIndex.IsDialogAvailable),
                pkg => OpenResourceExDialogMenu(pkg)),
            BackTo(() => OpenPackMenu()));
    }

    private static void OpenResourceExDialogMenu(string package)
    {
        var dialogs = StoryReplayIndex.GetDialogs("ResourceEx", package);
        if (dialogs.Count == 0)
        {
            Log.Warning($"ResourceEx package {package} has no dialog");
            return;
        }

        OpenSelectionMenu(
            BuildSelectionItems(
                dialogs,
                StoryReplayIndex.GetDialogDisplayTitle,
                StoryReplayIndex.IsDialogAvailable,
                PlayDialog),
            BackTo(() => OpenResourceExPackageMenu()));
    }

    private static void OpenGroupMenu(string pack, string category)
    {
        var groups = StoryReplayIndex.GetGroups(pack, category);
        if (groups.Count == 0)
        {
            OpenDialogMenu(pack, category, "(ungrouped)");
            return;
        }

        OpenSelectionMenu(
            BuildSelectionItems(
                groups,
                title => title,
                group => StoryReplayIndex.GetDialogs(pack, category, group).Any(StoryReplayIndex.IsDialogAvailable),
                group => OpenDialogMenu(pack, category, group)),
            BackTo(() => OpenCategoryMenu(pack)));
    }

    private static void OpenDialogMenu(string pack, string category, string group)
    {
        var dialogs = StoryReplayIndex.GetDialogs(pack, category, group);
        if (dialogs.Count == 0)
        {
            Log.Warning($"[{pack}/{category}/{group}] No dialog");
            return;
        }

        OpenSelectionMenu(
            BuildSelectionItems(
                dialogs,
                title => title,
                StoryReplayIndex.IsDialogAvailable,
                PlayDialog),
            BackTo(() => OpenGroupMenu(pack, category)));
    }

    private static void PlayDialog(string dialogName)
    {
        if (!StoryReplayRecentHistory.TryResolvePackage(dialogName, out var package))
        {
            Log.Warning($"Dialog package not found: {dialogName}");
            return;
        }

        Log.Info($"Playing dialog: {dialogName}");
        UniversalGameManager.OpenDialogMenu(
            package,
            onFinishCallback: null,
            overrideReplaceTextCallback: DialogRegistry.GetOverrideReplaceTextCallback(package));
    }
}
