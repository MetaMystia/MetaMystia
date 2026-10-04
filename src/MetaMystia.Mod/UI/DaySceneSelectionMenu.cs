using System;
using System.Collections.Generic;
using System.Linq;

using Mystia.Listeners;

namespace MetaMystia.UI;

/// <summary>
/// 白天的选择列表：把「条目 + 标题 + 可用性 + 选中动作」交给框架的聊天选择面板
/// （<c>IChatSelectionServices</c>）。游戏那套带 <c>out</c> 参数的回调与委托构造留在桥接，
/// 模组侧不再接触游戏回调，也不再自开原生 invoker。
/// </summary>
internal static class DaySceneSelectionMenu
{
    internal const string BackButtonKey = "DLC5_LUNARCAPITALCONSOLE_REPEATCHALLENGE_BACK";
    internal const string CloseButtonKey = "KIZUNA_REQUEST_END";

    /// <summary>
    /// 条目列表：可用性为假的条目照样交给面板（面板自己丢掉，与游戏自己的菜单一致）；
    /// 选中时先关掉当前面板再执行动作，等价原回调里先调 <c>closeChatSelectionPannelCallback</c>。
    /// </summary>
    internal static List<ChatMenuEntry> BuildSelectionItems<T>(
        IEnumerable<T> items,
        Func<T, string> getTitle,
        Func<T, bool> isAvailable,
        Action<T> onSelected) =>
        items.Select(item => new ChatMenuEntry(
            getTitle(item),
            isAvailable(item),
            () =>
            {
                if (!isAvailable(item)) return;
                ModRuntime.ChatSelection.Close();
                onSelected(item);
            })).ToList();

    /// <summary>
    /// 打开列表。<paramref name="onEndButton"/> 是结束项跑完之后要做的事（子菜单用它返回上级），
    /// null 表示只结束菜单；<paramref name="endButtonTitleKey"/> 是结束项的语言 key。
    /// </summary>
    internal static void OpenSelectionMenu(
        List<ChatMenuEntry> entries,
        Action onEndButton = null,
        string endButtonTitleKey = CloseButtonKey) =>
        ModRuntime.ChatSelection.Open(entries, endButtonTitleKey, onEndButton);
}
