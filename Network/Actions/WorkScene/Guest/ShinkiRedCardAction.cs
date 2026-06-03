// 这里是描述神绮红卡【魔神降临】网络同步的代码
using MemoryPack;

using MetaMystia.ResourceEx.SpellCollection;

namespace MetaMystia.Network;

/// <summary>
/// 神绮红卡：【魔神降临】
/// 主机广播传送门开启状态，客机在本地创建传送门视觉。
/// 后续的客人召唤通过现有的 GuestSpawnAction 管道自动同步。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class ShinkiRedCardAction : Action
{
    public override ActionType Type => ActionType.ShinkiRedCardAction;

    /// <summary>
    /// 传送门是否已经开启过（true = 跳过开门动画，直接召唤）
    /// </summary>
    public bool PortalAlreadyOpen { get; set; }

    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        if (MpManager.IsConnectedHost) return;

        Spell_Shinki.ReplayRedCard(PortalAlreadyOpen);
    }

    public static void Send(bool portalAlreadyOpen)
    {
        var action = new ShinkiRedCardAction
        {
            PortalAlreadyOpen = portalAlreadyOpen,
        };
        action.SendToHostOrBroadcast();
    }
}
