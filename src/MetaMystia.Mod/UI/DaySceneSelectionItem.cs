using System;

using Il2CppInterop.Runtime;

using DayScene.UI;

namespace MetaMystia.UI;

/// <summary>
/// 白天选项面板的一项。原生委托只在构造时生成一次，内容由 <see cref="Set"/> 替换。
/// </summary>
internal sealed class DaySceneSelectionItem
{
    private readonly DaySceneChatSelectionPannel.GetSelectionConfigurationCallback _callback;
    private readonly Il2CppSystem.Action _interact;

    private Func<string> _getTitle;
    private Func<bool> _isAvailable;
    private Action _onSelected;
    private DaySceneChatSelectionPannel.BaseInteractData _data;

    internal DaySceneSelectionItem()
    {
        _callback = SelectionCallback.Create(Configure);
        _interact = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((Action)Interact);
    }

    internal DaySceneChatSelectionPannel.GetSelectionConfigurationCallback Set(
        Func<string> getTitle, Func<bool> isAvailable, Action onSelected)
    {
        _getTitle = getTitle;
        _isAvailable = isAvailable;
        _onSelected = onSelected;
        _data = null;
        return _callback;
    }

    private void Configure(
        DaySceneChatSelectionPannel.BaseInteractData data,
        out string title, out bool availability, out Il2CppSystem.Action onInteract)
    {
        _data = data;
        title = _getTitle();
        availability = _isAvailable();
        onInteract = _interact;
    }

    private void Interact()
    {
        // 内容替换后、新面板取得配置前，旧面板的点击无效。
        if (_data is not { } data || !_isAvailable()) return;
        _data = null;
        data.closeChatSelectionPannelCallback?.Invoke();
        _onSelected();
    }
}
