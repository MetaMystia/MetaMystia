using System.Collections.Generic;
using System.Linq;

using GameData.RunTime.NightSceneUtility;
using NightScene;

using MetaMystia.Network;
using MetaMystia.Patch;

namespace MetaMystia;

public static partial class PrepSceneManager
{
    public static bool IsYuyukoChallenge => NightSceneDirector.ChallengeMode is
        NightSceneDirector.ChallengeType.Story_Yuyuko or NightSceneDirector.ChallengeType.Challenge_Yuyuko;

    public static int YuyukoPrepRound { get; private set; }
    public static bool IsYuyukoPrepActive { get; private set; }
    private static bool yuyukoPrepConfirmed;
    private static readonly Dictionary<int, int> yuyukoReadyRounds = new();
    private static readonly Dictionary<int, UpdatePrepAction.Table> nextYuyukoPrepTables = new();

    public static void ResetYuyukoPrep()
    {
        YuyukoPrepRound = 0;
        IsYuyukoPrepActive = false;
        yuyukoPrepConfirmed = false;
        yuyukoReadyRounds.Clear();
        nextYuyukoPrepTables.Clear();
    }

    public static void BeginYuyukoPrep()
    {
        if (!MpManager.IsConnected || !IsYuyukoChallenge) return;

        YuyukoPrepRound++;
        IsYuyukoPrepActive = true;
        yuyukoPrepConfirmed = false;
        PlayerManager.LocalIsPrepOver = false;

        // OnPanelOpen 已完成：保留本阶段原有配置，实际编辑的时间戳优先于基线。
        var configure = IzakayaConfigure.Instance;
        localPrepTable = new UpdatePrepAction.Table();
        foreach (var recipe in configure.DailyRecipes)
            localPrepTable.RecipeAdditions[recipe.Id] = 1;
        foreach (var beverage in configure.DailyBeverages)
            localPrepTable.BeverageAdditions[beverage.Id] = 1;
        for (int i = 0; i < configure.CookerConfigure.Length && i < localPrepTable.Cookers.Length; i++)
        {
            localPrepTable.Cookers[i].Id = configure.CookerConfigure[i];
            localPrepTable.Cookers[i].Timestamp = 1;
        }

        foreach (var table in nextYuyukoPrepTables.Values)
            MergeFromPeer(table);
        nextYuyukoPrepTables.Clear();
        UpdatePrepAction.Send(localPrepTable);
        Log.Info($"Yuyuko prep round {YuyukoPrepRound} opened");
    }

    public static void ReceiveYuyukoPrepTable(int senderUid, int round, UpdatePrepAction.Table table)
    {
        if (!IsYuyukoChallenge || !PlayerManager.Peers.ContainsKey(senderUid)) return;
        if (round == YuyukoPrepRound && IsYuyukoPrepActive && !yuyukoPrepConfirmed)
            MergeFromPeer(table);
        else if (round == YuyukoPrepRound + 1)
            nextYuyukoPrepTables[senderUid] = table;
    }

    public static void ReceiveYuyukoPrepReady(int senderUid, int round)
    {
        if (!IsYuyukoChallenge || !PlayerManager.Peers.ContainsKey(senderUid)) return;
        if (round < YuyukoPrepRound || round > YuyukoPrepRound + 1) return;
        // 对端可能先播完剧情；打开本地面板时不能清掉已收到的本轮就绪。
        if (!yuyukoReadyRounds.TryGetValue(senderUid, out var previous) || round > previous)
            yuyukoReadyRounds[senderUid] = round;
        TryConfirmYuyukoPrep();
    }

    public static void TryConfirmYuyukoPrep()
    {
        if (!MpManager.IsConnectedServer || !IsYuyukoChallenge || !IsYuyukoPrepActive
            || yuyukoPrepConfirmed || !PlayerManager.LocalIsPrepOver) return;
        if (!PlayerManager.Peers.Keys.All(uid =>
                yuyukoReadyRounds.TryGetValue(uid, out var round) && round == YuyukoPrepRound)) return;

        yuyukoPrepConfirmed = true;
        PrepAllReadyAction.Send();
        int confirmedRound = YuyukoPrepRound;
        // 离开当前按钮 Hook 后再重放提交，避免在同一次调用中重入面板。
        PluginManager.RunOnMainThread(() =>
        {
            if (IsYuyukoChallenge && IsYuyukoPrepActive && YuyukoPrepRound == confirmedRound)
                IzakayaConfigPannelPatch.PrepOver();
        });
    }

    public static bool CanFinishYuyukoPrep(int round) =>
        IsYuyukoChallenge && IsYuyukoPrepActive && round == YuyukoPrepRound
        && PlayerManager.LocalIsPrepOver;

    public static bool IsYuyukoPrepReady(int uid) =>
        IsYuyukoPrepActive && (uid == PlayerManager.Local.Uid
            ? PlayerManager.LocalIsPrepOver
            : yuyukoReadyRounds.TryGetValue(uid, out var round) && round == YuyukoPrepRound);

    public static void EndYuyukoPrep()
    {
        IsYuyukoPrepActive = false;
        PlayerManager.LocalIsPrepOver = false;
        Log.Info($"Yuyuko prep round {YuyukoPrepRound} confirmed");
    }
}
