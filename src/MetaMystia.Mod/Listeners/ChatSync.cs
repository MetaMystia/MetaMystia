using System.Collections.Generic;

using DayScene.UI;
using GameData.Core.Collections.DaySceneUtility;

using Mystia.Listeners;

using MetaMystia.ResourceEx.Registries;
using SgrYuki.Utils;

namespace MetaMystia.Listeners;

/// <summary>
/// 聊天菜单监听，取代原 <c>CollabBehaviourComponentPatch</c>、<c>DaySceneUIManagerPatch</c> 与
/// <c>DaySceneChatSelectionPannel__c__DisplayClass17_0Patch</c>：
/// 互动来源识别与菜单项追加由桥接的聊天菜单管线承担（模组只提供菜单项），
/// 资源包商人的闲聊/商品项可用性修正改走 <see cref="IChatOptionListener"/>。
/// </summary>
[AutoLog]
public sealed partial class ChatSync : IChatOptionListener, IChatMenuProvider
{
    /// <summary>
    /// 原 <c>DaySceneUIManagerPatch.OpenAfterChatMenu_Prefix</c>：只有与合作对象互动得到的通用菜单
    /// 才追加剧情回放与礼物信箱（原 <c>PendingCollabMenu</c> 的作用等价于来源为
    /// <see cref="ChatMenuOrigin.Collab"/>）。
    /// </summary>
    public void ProvideChatMenuEntries(in ChatMenuContext context, IList<ChatMenuEntry> entries)
    {
        if (context.Origin != ChatMenuOrigin.Collab) return;

        entries.Add(StoryReplayManager.CreateCollabMenuEntry());
        entries.Add(GiftMailboxManager.CreateCollabMenuEntry());
    }

    /// <summary>原 <c>DaySceneChatSelectionPannel__c__DisplayClass17_0Patch</c> 的两个后缀。</summary>
    public void OnChatOptionAvailability(in ChatOptionContext context, ref bool available)
    {
        var label = context.CharacterLabel;
        if (string.IsNullOrEmpty(label) || !label.IsResourceExSpecialMerchant()) return;

        switch (context.Option)
        {
            case ChatOptionKind.FreeChat when !available && context.HasChatData:
                // 资源包商人自带对话数据时，移除 MerchantData 对 FreeChat 的屏蔽。
                available = DataBaseDay.DaySceneCheckSpecialGuestNotSkipGreeting(context.CharacterId);
                Log.Info($"ExMerchant {label} has chat data, set free chat availability to {available}");
                break;
            case ChatOptionKind.Shop when available && context.ProductCount == 0:
                // 当日没有剩余商品（含无商品记录）时隐藏商品项。
                available = false;
                Log.Info($"ExMerchant {label} has no products, hide merchant selection");
                break;
        }
    }

    /// <summary>
    /// 关闭聊天选择面板。原菜单项拿到的 <c>closeChatSelectionPannelCallback</c> 就是面板自身的
    /// <c>ClosePanel</c>；桥接给模组菜单项的选中回调不携带面板数据，因此从面板栈取当前面板。
    /// </summary>
    internal static void CloseChatSelectionPanel()
    {
        if (Panel.TopPanel?.ControlledPanel is DaySceneChatSelectionPannel panel)
        {
            panel.ClosePanel();
            return;
        }
        Log.Warning("Chat selection panel is not on top, mod chat entry cannot close it.");
    }
}
