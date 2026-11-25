using System;
using System.Collections.Generic;
using System.Linq;

using DayScene.UI;

using SgrYuki.Utils;

namespace MetaMystia.UI;

internal static class DaySceneSelectionMenu
{
    internal const string BackButtonKey = "DLC5_LUNARCAPITALCONSOLE_REPEATCHALLENGE_BACK";
    internal const string CloseButtonKey = "KIZUNA_REQUEST_END";

    // 同时只有一个模组菜单，各菜单按序号共用。
    private static readonly List<DaySceneSelectionItem> Items = [];

    internal static List<DaySceneChatSelectionPannel.GetSelectionConfigurationCallback> BuildSelectionItems<T>(
        IEnumerable<T> items,
        Func<T, string> getTitle,
        Func<T, bool> isAvailable,
        Action<T> onSelected) =>
        items.Select((item, index) =>
        {
            if (index == Items.Count) Items.Add(new DaySceneSelectionItem());
            return Items[index].Set(() => getTitle(item), () => isAvailable(item), () => onSelected(item));
        }).ToList();

    internal static void OpenSelectionMenu(
        List<DaySceneChatSelectionPannel.GetSelectionConfigurationCallback> callbacks,
        Action<Il2CppSystem.Action> endButton,
        string endButtonTitleKey = CloseButtonKey)
    {
        if (callbacks.Count == 0) return;

        DayScene.UI.UIManager.Instance.OpenAfterChatMenu(
            callbacks.ToIl2CppReferenceArray(),
            endButtonTitleKey,
            endButton,
            null);
    }

    internal static Action<Il2CppSystem.Action> BackTo(Action reopen) => closeCallback =>
    {
        closeCallback.Invoke();
        reopen();
    };

    internal static void CloseEndButton(Il2CppSystem.Action closeCallback) => closeCallback.Invoke();
}
