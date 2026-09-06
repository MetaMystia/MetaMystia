using System;
using System.Collections.Generic;

using Il2CppInterop.Runtime;

using DayScene.UI;

using SgrYuki.Utils;

namespace MetaMystia.UI;

internal static class DaySceneSelectionMenu
{
    internal const string BackButtonKey = "DLC5_LUNARCAPITALCONSOLE_REPEATCHALLENGE_BACK";
    internal const string CloseButtonKey = "KIZUNA_REQUEST_END";

    internal static List<DaySceneChatSelectionPannel.GetSelectionConfigurationCallback> BuildSelectionItems<T>(
        IEnumerable<T> items,
        Func<T, string> getTitle,
        Func<T, bool> isAvailable,
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
                        if (!isAvailable(captured)) return;
                        data.closeChatSelectionPannelCallback?.Invoke();
                        onSelected(captured);
                    });
                }));
        }
        return callbacks;
    }

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
