using System;
using System.Collections.Generic;
using System.Linq;
using Common.UI;
using DayScene.UI;
using GameData;
using GameData.Core.Collections.DaySceneUtility;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using SgrYuki.Utils;

namespace MetaMystia;

[AutoLog]
public static partial class StoryReplayManager
{
    private const string BackButtonKey = "DLC5_LUNARCAPITALCONSOLE_REPEATCHALLENGE_BACK";
    private const string CloseButtonKey = "KIZUNA_REQUEST_END";

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
            Log.Warning("没有可回放的对话");
            return;
        }

        OpenPackMenu();
    }

    public static DaySceneChatSelectionPannel.GetSelectionConfigurationCallback CreateCollabMenuSelection() =>
        Il2CppOutDelegate.CreateGetSelectionConfigurationCallback(
            (data, out title, out availability, out onInteract) =>
            {
                title = CollabMenuTitle;
                StoryReplayIndex.Rebuild();
                availability = StoryReplayIndex.Packs.Count > 0;
                onInteract = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(() =>
                {
                    data.closeChatSelectionPannelCallback?.Invoke();
                    OpenReplayMenu();
                });
            });

    private static void OpenPackMenu()
    {
        OpenSelectionMenu(
            BuildSelectionItems(
                StoryReplayIndex.Packs,
                GetPackTitle,
                IsPackAvailable,
                (_, data) => data.closeChatSelectionPannelCallback?.Invoke(),
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
            Log.Warning("没有最近阅读的对话");
            return;
        }

        OpenSelectionMenu(
            BuildSelectionItems(
                dialogs,
                StoryReplayIndex.GetDialogDisplayTitle,
                _ => true,
                (dialog, data) => data.closeChatSelectionPannelCallback?.Invoke(),
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
            Log.Warning($"[{pack}] 没有可用分类");
            return;
        }

        OpenSelectionMenu(
            BuildSelectionItems(
                categories,
                title => title,
                _ => true,
                (_, data) => data.closeChatSelectionPannelCallback?.Invoke(),
                category => OpenGroupMenu(pack, category)),
            BackTo(() => OpenPackMenu()));
    }

    private static void OpenResourceExPackageMenu()
    {
        var packages = StoryReplayIndex.GetCategories("ResourceEx");
        if (packages.Count == 0)
        {
            Log.Warning("没有 ResourceEx 对话");
            return;
        }

        OpenSelectionMenu(
            BuildSelectionItems(
                packages,
                title => title,
                pkg => StoryReplayIndex.GetDialogs("ResourceEx", pkg).Any(StoryReplayIndex.IsDialogAvailable),
                (_, data) => data.closeChatSelectionPannelCallback?.Invoke(),
                pkg => OpenResourceExDialogMenu(pkg)),
            BackTo(() => OpenPackMenu()));
    }

    private static void OpenResourceExDialogMenu(string package)
    {
        var dialogs = StoryReplayIndex.GetDialogs("ResourceEx", package);
        if (dialogs.Count == 0)
        {
            Log.Warning($"ResourceEx 包 {package} 没有对话");
            return;
        }

        OpenSelectionMenu(
            BuildSelectionItems(
                dialogs,
                StoryReplayIndex.GetDialogDisplayTitle,
                StoryReplayIndex.IsDialogAvailable,
                (dialog, data) => data.closeChatSelectionPannelCallback?.Invoke(),
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
                (_, data) => data.closeChatSelectionPannelCallback?.Invoke(),
                group => OpenDialogMenu(pack, category, group)),
            BackTo(() => OpenCategoryMenu(pack)));
    }

    private static void OpenDialogMenu(string pack, string category, string group)
    {
        var dialogs = StoryReplayIndex.GetDialogs(pack, category, group);
        if (dialogs.Count == 0)
        {
            Log.Warning($"[{pack}/{category}/{group}] 没有对话");
            return;
        }

        OpenSelectionMenu(
            BuildSelectionItems(
                dialogs,
                title => title,
                StoryReplayIndex.IsDialogAvailable,
                (dialog, data) => data.closeChatSelectionPannelCallback?.Invoke(),
                PlayDialog),
            BackTo(() => OpenGroupMenu(pack, category)));
    }

    private static List<DaySceneChatSelectionPannel.GetSelectionConfigurationCallback> BuildSelectionItems<T>(
        IEnumerable<T> items,
        Func<T, string> getTitle,
        Func<T, bool> isAvailable,
        Action<T, DaySceneChatSelectionPannel.BaseInteractData> onBeforeAction,
        Action<T> onSelected)
    {
        var callbacks = new List<DaySceneChatSelectionPannel.GetSelectionConfigurationCallback>();
        foreach (var item in items)
        {
            var captured = item;
            callbacks.Add(Il2CppOutDelegate.CreateGetSelectionConfigurationCallback(
                (data, out title, out availability, out onInteract) =>
                {
                    title = getTitle(captured);
                    availability = isAvailable(captured);
                    onInteract = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(() =>
                    {
                        onBeforeAction(captured, data);
                        onSelected(captured);
                    });
                }));
        }
        return callbacks;
    }

    private static void PlayDialog(string dialogName)
    {
        if (!StoryReplayRecentHistory.TryResolvePackage(dialogName, out var package))
        {
            Log.Warning($"找不到对话包: {dialogName}");
            return;
        }

        Log.Info($"播放对话: {dialogName}");
        UniversalGameManager.OpenDialogMenu(
            package,
            onFinishCallback: null,
            overrideReplaceTextCallback: ResourceExManager.GetOverrideReplaceTextCallback(package));
    }

    private static void OpenSelectionMenu(
        List<DaySceneChatSelectionPannel.GetSelectionConfigurationCallback> callbacks,
        Action<Il2CppSystem.Action> endButton,
        string endButtonTitleKey = CloseButtonKey)
    {
        if (callbacks.Count == 0)
        {
            Log.Warning("选项列表为空");
            return;
        }

        DayScene.UI.UIManager.Instance.OpenAfterChatMenu(
            callbacks.ToIl2CppReferenceArray(),
            endButtonTitleKey,
            endButton,
            null);
    }

    private static Action<Il2CppSystem.Action> BackTo(Action reopen) => closeCallback =>
    {
        closeCallback.Invoke();
        reopen();
    };

    private static void CloseEndButton(Il2CppSystem.Action closeCallback) => closeCallback.Invoke();
}
