using MemoryPack;

using NightScene.EventUtility;

using MetaMystia.Patch;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 任何玩家 → 全体玩家: NightScene.EventUtility.EventManager.TipEdit 的网络同步
/// </summary>
[MemoryPackable]
[AutoLog]

public partial class TipEditMessage : MultiplayerMessage
{

    public int IntValue { get; set; }
    public EventManager.ServeType ServeType { get; set; }
    public float ComboBuff { get; set; }
    public float MoodBuff { get; set; }
    public float ExtraBuff { get; set; }

    [ClientOnlyReceive]
    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var em = EventManager.Instance;
        if (em == null) return;
        NightSceneEventManagerPatch.TipEdit_ReversePatch(em, IntValue, ServeType, ComboBuff, MoodBuff, ExtraBuff);
    }

    public static void Send(int value, EventManager.ServeType serveType, float comboBuff, float moodBuff, float extraBuff) =>
        new TipEditMessage
        {
            IntValue = value,
            ServeType = serveType,
            ComboBuff = comboBuff,
            MoodBuff = moodBuff,
            ExtraBuff = extraBuff
        }.Enqueue();
}
