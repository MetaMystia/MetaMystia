// 这里是描述神绮黑卡【绮符：环游魔界80天】网络同步的代码
using MemoryPack;
using UnityEngine;

using MetaMystia.ResourceEx.SpellCollection;

namespace MetaMystia.Network;

/// <summary>
/// 神绮黑卡：绮符【环游魔界80天】
/// 主机广播所有受影响客人的 runtime ID 和传送门位置，客机在本地重放走向传送门动画。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class ShinkiBlackCardAction : Action
{
    public override ActionType Type => ActionType.ShinkiBlackCardAction;

    public int[] AffectedRuntimeIds { get; set; } = [];
    public int ShinkiRuntimeId { get; set; } = -1;
    public float PortalX { get; set; }
    public float PortalY { get; set; }
    public float PortalZ { get; set; }

    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        if (MpManager.IsConnectedHost) return;

        var ids = AffectedRuntimeIds;
        var shinkiRid = ShinkiRuntimeId;
        var portalPos = new Vector3(PortalX, PortalY, PortalZ);

        PluginManager.Instance.RunOnMainThread(() =>
        {
            Spell_Shinki.ReplayBlackCard(ids, shinkiRid, portalPos);
        });
    }

    public static void Send(int[] affectedRuntimeIds, int shinkiRuntimeId, Vector3 portalPos)
    {
        var action = new ShinkiBlackCardAction
        {
            AffectedRuntimeIds = affectedRuntimeIds,
            ShinkiRuntimeId = shinkiRuntimeId,
            PortalX = portalPos.x,
            PortalY = portalPos.y,
            PortalZ = portalPos.z,
        };
        action.SendToHostOrBroadcast();
    }
}
