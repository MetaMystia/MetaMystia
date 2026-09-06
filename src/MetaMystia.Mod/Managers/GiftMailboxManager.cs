using System.Linq;

using Il2CppInterop.Runtime;

using Common.UI;
using DayScene.UI;
using GameData.RunTime.Common;

using MetaMystia.ResourceEx.Registries;
using MetaMystia.UI;
using SgrYuki.Utils;

using static MetaMystia.UI.DaySceneSelectionMenu;

namespace MetaMystia;

[AutoLog]
public static partial class GiftMailboxManager
{
    public static DaySceneChatSelectionPannel.GetSelectionConfigurationCallback CreateCollabMenuSelection() =>
        Il2CppOutDelegate.CreateGetSelectionConfigurationCallback(
            (data, out title, out availability, out onInteract) =>
            {
                title = TextId.GiftMailboxTitle.Get();
                availability = GiftRegistry.Mailboxes.Count > 0;
                onInteract = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(() =>
                {
                    if (GiftRegistry.Mailboxes.Count == 0) return;
                    data.closeChatSelectionPannelCallback?.Invoke();
                    OpenMailboxMenu();
                });
            });

    public static void OpenMailboxMenu() => OpenSelectionMenu(
        BuildSelectionItems(
            GiftRegistry.Mailboxes,
            mailbox => string.IsNullOrWhiteSpace(mailbox.Package.Config.packInfo?.name)
                ? mailbox.Package.PackageName
                : mailbox.Package.Config.packInfo.name,
            _ => true,
            OpenGiftMenu),
        CloseEndButton);

    private static void OpenGiftMenu(GiftRegistry.Mailbox mailbox) => OpenSelectionMenu(
        BuildSelectionItems(
            Enumerable.Range(0, mailbox.Gifts.Count),
            index => string.IsNullOrWhiteSpace(mailbox.Gifts[index]?.title)
                ? TextId.GiftMailboxInvalidGift.Get()
                : mailbox.Gifts[index].title,
            index => mailbox.Available[index] && GiftRegistry.TryResolveGift(mailbox, index, out _),
            index => PlayGiftDialog(mailbox, index)),
        BackTo(OpenMailboxMenu),
        BackButtonKey);

    private static void PlayGiftDialog(GiftRegistry.Mailbox mailbox, int index)
    {
        if (!GiftRegistry.TryResolveGift(mailbox, index, out var dialog)) return;

        var gift = mailbox.Gifts[index];
        int itemId = gift.itemId.Value;
        bool finished = false;
        UniversalGameManager.OpenDialogMenu(
            dialog,
            onFinishCallback: DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(() =>
            {
                if (finished) return;
                finished = true;

                if (!gift.allowRepeat && RunTimeStorage.ContainsItem(itemId)) return;

                var itemIds = new Il2CppSystem.Collections.Generic.List<int>();
                itemIds.Add(itemId);
                RunTimeStorage.ItemInRange(itemIds.ToIEnumerable(), suppressCallbacks: false);
                Log.Info($"[{mailbox.Package.PackageName}] 已处理礼物发放: {gift.title}, Item={itemId}");
            }),
            overrideReplaceTextCallback: DialogRegistry.GetOverrideReplaceTextCallback(dialog));
    }
}
