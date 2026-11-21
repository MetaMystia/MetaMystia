using System;
using System.Linq;

using HarmonyLib;
using Il2CppSystem.Linq;
using UnityEngine;

using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using GameData.RunTime.NightSceneUtility;
using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Guests;
using MetaMystia.Multiplayer.Messages;
using SgrYuki.Utils;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.GuestManagementUtility.GuestsManager))]
[TracePatch(nameof(GuestsManager.SpawnSpecialGuestGroup))]
[TracePatch(nameof(GuestsManager.SpawnNormalGuestGroup), new[] {
    typeof(Il2CppSystem.Collections.Generic.IEnumerable<NormalGuest>),
    typeof(Il2CppSystem.Nullable<UnityEngine.Vector3>),
    typeof(GuestGroupController.LeaveType),
    typeof(int),
    typeof(bool),
}, DisplayName = "GuestsManager.SpawnNormalGuestGroup_WithArgs")]
[TracePatch(nameof(GuestsManager.SpawnNormalGuestGroup), new Type[0], DisplayName = "GuestsManager.SpawnNormalGuestGroup")]
[TracePatch(nameof(GuestsManager.SpawnManualControlledSpecialGuestGroup))]
[TracePatch(nameof(GuestsManager.SpawnGuest))]
[TracePatch(nameof(GuestsManager.PostInitializeGuestGroup))]
[TracePatch(nameof(GuestsManager.TrySendToSeat))]
[TracePatch(nameof(GuestsManager.CheckAndSendFromQueue))]
[TracePatch(nameof(GuestsManager.FirstOrder))]
[TracePatch(nameof(GuestsManager.GenerateOrderSession))]
[TracePatch(nameof(GuestsManager.ExcuteEventAtCorodinate))]
[TracePatch(nameof(GuestsManager.ShowOrder))]
[TracePatch(nameof(GuestsManager.EvaluateOrder))]
[TracePatch(nameof(GuestsManager.EvaulateManualOrder))]
[TracePatch(nameof(GuestsManager.MainOrderCycle))]
[TracePatch(nameof(GuestsManager.LackMoneyEvaluate))]
[TracePatch(nameof(GuestsManager.AddToPatientCountdown))]
[TracePatch(nameof(GuestsManager.RemoveFromPatientCountdown))]
[TracePatch(nameof(GuestsManager.PatientDepletedLeave))]
[TracePatch(nameof(GuestsManager.RepellInternal))]
[TracePatch(nameof(GuestsManager.PlayerRepell))]
[TracePatch(nameof(GuestsManager.TryRepellAllQueuedGuestControllers))]
[TracePatch(nameof(GuestsManager.PayAndLeave))]
[TracePatch(nameof(GuestsManager.GuestPay))]
[TracePatch(nameof(GuestsManager.PayByMood))]
[TracePatch(nameof(GuestsManager.LeaveFromDesk))]
[TracePatch(nameof(GuestsManager.TryCloseIzakaya))]
[TracePatch(nameof(GuestsManager.ShowEvaluationDialog))]
[AutoLog]
public partial class GuestsManagerPatch
{
    // 幽幽子挑战：手动订单与剧情回调。
    /// <summary>
    /// 暂存本体的原版手动订单和完成回调，等待同步条件就绪后安装。
    /// 同步管理器主动安装时放行，其他手动顾客仍使用原版流程。
    /// </summary>
    [HarmonyPatch(nameof(GuestsManager.SetManualControllerOrderInternal))]
    [HarmonyPrefix]
    public static bool SetManualControllerOrderInternal_Prefix(GuestGroupController manualControlled,
        Il2CppSystem.Action<GuestGroupController.EvaluationResult> onEvaluate, GuestsManager.OrderBase order)
    {
        if (!YuyukoGuestSync.IsBody(manualControlled) || YuyukoGuestSync.IsInstalling) return RunOriginal;
        YuyukoGuestSync.QueueOrder(order, onEvaluate);
        return SkipOriginal;
    }

    /// <summary>
    /// 本体仅允许主机发起评价或客机重放主机结果，并要求仍处于等待上菜状态。
    /// 手动评价不经过普通 EvaluateOrder 入口，需在此单独限制；其他实体放行。
    /// </summary>
    [HarmonyPatch(nameof(GuestsManager.EvaulateManualOrder))]
    [HarmonyPrefix]
    public static bool EvaulateManualOrder_Prefix(GuestGroupController toEvaluate) =>
        YuyukoGuestSync.CanEvaluate(toEvaluate) ? RunOriginal : SkipOriginal;

    /// <summary>原版清理订单显示后，取消本体的同步订单及旧回调；主机同时广播取消序号。</summary>
    [HarmonyPatch(nameof(GuestsManager.CleanOrderInfo))]
    [HarmonyPostfix]
    public static void CleanOrderInfo_Postfix(GuestGroupController guestGroup) => YuyukoGuestSync.OnClean(guestGroup);

    /// <summary>原版手动离场前取消本体订单，避免离场期间迟到的上菜或完成回调继续推进旧订单。</summary>
    [HarmonyPatch(nameof(GuestsManager.SetManualControlledLeave))]
    [HarmonyPrefix]
    public static void SetManualControlledLeave_Prefix(GuestGroupController manualControlled) =>
        YuyukoGuestSync.OnClean(manualControlled);

    private const int MaxNormalGuestRerollAttempts = 32;
    private const int ReimuProtectionGuestId = 7;

    private static PendingSpawnArgs? _pendingNormalSpawnArgs;
    private static PendingSpawnArgs? _pendingSpecialSpawnArgs;

    public readonly struct PendingSpawnArgs
    {
        public bool HasOverrideSpawnPosition { get; init; }
        public Vector3 OverrideSpawnPosition { get; init; }
        public GuestGroupController.LeaveType LeaveType { get; init; }
        public int TargetDeskCode { get; init; }
        public bool ShouldFade { get; init; }
        public SpecialGuestsController.GuestSpawnType GuestSpawnType { get; init; }
    }

    private static bool IsReimuProtectionGuest(int id)
        => RunTimeSchedulerPatch.IsDuringReimuProtection && id == ReimuProtectionGuestId;

    internal static bool IsReimuProtectionGuest(GuestGroupController controller)
    {
        if (!RunTimeSchedulerPatch.IsDuringReimuProtection ||
            controller == null ||
            controller.ControllType != GuestsManager.GuestType.Special)
        {
            return false;
        }

        var guests = controller.GetAllGuests().ToArray();
        return guests.Length == 1 && guests[0].Id == ReimuProtectionGuestId;
    }

    private static bool IsNormalGuestGroupAvailable(NormalGuest[] guests)
        => guests.Length > 0 && guests.All(guest => PlayerManager.NormalGuestAvailable(guest.id));

    private static bool TryGetFallbackNormalGuest(out NormalGuest guest)
    {
        var candidates = DataBaseCharacter.GetAllNormalGuests()
            .ToArray()
            .Where(candidate => PlayerManager.NormalGuestAvailable(candidate.id))
            .ToArray();
        if (candidates.Length <= 0)
        {
            guest = null;
            return false;
        }

        guest = candidates[UnityEngine.Random.Range(0, candidates.Length)];
        return true;
    }

    /// <summary>
    /// 替换非法普客组中的不可用普客，若无法找到可用的替换则返回 false 以跳过生成
    /// </summary>
    /// <param name="normalGuests"></param>
    /// <returns></returns>
    private static bool TryReplaceUnavailableNormalGuests(
        ref Il2CppSystem.Collections.Generic.IEnumerable<NormalGuest> normalGuests)
    {
        var guests = normalGuests.ToArray();
        if (IsNormalGuestGroupAvailable(guests)) return true;

        var changed = false;
        for (var i = 0; i < guests.Length; i++)
        {
            if (PlayerManager.NormalGuestAvailable(guests[i].id)) continue;
            if (!TryGetFallbackNormalGuest(out var replacement)) return false;

            Log.Warning($"Normal guest {guests[i].id} is not available for all players, replacing with {replacement.id}.");
            guests[i] = replacement;
            changed = true;
        }

        if (changed)
        {
            var list = new Il2CppSystem.Collections.Generic.List<NormalGuest>();
            foreach (var guest in guests)
            {
                list.Add(guest);
            }
            normalGuests = list.ToIEnumerable();
        }

        return true;
    }

    /// <summary>
    /// 无参普通客人是原版的自动生成循环。客机不自行生成；主机在剧情外重抽全员可用的顾客组，剧情中按原版生成本端顾客。
    /// </summary>
    [HarmonyPatch(nameof(GuestsManager.SpawnNormalGuestGroup), [])]
    [HarmonyPrefix]
    public static bool SpawnNormalGuestGroup_Prefix()
    {
        if (!GameSession.HasRoomPeers) return RunOriginal;
        if (GameSession.IsRoomClient) return SkipOriginal;
        if (GameFlow.InStory) return RunOriginal;

        var cook = NightScene.CookingUtility.CookSystemManager.Instance;
        for (var attempt = 0; attempt < MaxNormalGuestRerollAttempts; attempt++)
        {
            var guestGroups = cook?.GetRandomNormalGuestGroups();
            if (guestGroups == null)
            {
                Log.Error("CookSystemManager failed to GetRandomNormalGuestGroups.");
                return RunOriginal;
            }

            var guests = guestGroups.ToArray();
            if (!IsNormalGuestGroupAvailable(guests)) continue;

            GuestsManager.Instance.SpawnNormalGuestGroup(
                guestGroups,
                new Il2CppSystem.Nullable<Vector3>(),
                GuestGroupController.LeaveType.Move,
                -1,
                true);
            return SkipOriginal;
        }

        Log.Warning("No globally available normal guest group found after reroll, skipping spawn.");
        return SkipOriginal;
    }

    /// <summary>
    /// 主机生成带参普通客人时，暂存额外生成参数；客机不自行生成。剧情中两端按原版生成本端顾客。
    /// </summary>
    [HarmonyPatch(nameof(GuestsManager.SpawnNormalGuestGroup), [
        typeof(Il2CppSystem.Collections.Generic.IEnumerable<NormalGuest>),
        typeof(Il2CppSystem.Nullable<UnityEngine.Vector3>),
        typeof(GuestGroupController.LeaveType),
        typeof(int),
        typeof(bool),
    ])]
    [HarmonyPrefix]
    public static bool SpawnNormalGuestGroup_WithArgs_Prefix(
        ref Il2CppSystem.Collections.Generic.IEnumerable<NormalGuest> normalGuests,
        Il2CppSystem.Nullable<Vector3> overrideSpawnPosition,
        GuestGroupController.LeaveType leaveType,
        int targetDeskCode,
        bool shouldFade,
        ref NormalGuestsController __result)
    {
        if (!GameSession.HasRoomPeers || GameFlow.InStory) return RunOriginal;
        if (GameSession.IsRoomClient)
        {
            __result = null;
            return SkipOriginal;
        }

        if (!TryReplaceUnavailableNormalGuests(ref normalGuests))
        {
            Log.Warning("No globally available normal guest fallback found, skipping spawn.");
            __result = null;
            return SkipOriginal;
        }

        _pendingNormalSpawnArgs = new PendingSpawnArgs
        {
            HasOverrideSpawnPosition = overrideSpawnPosition.HasValue,
            OverrideSpawnPosition = overrideSpawnPosition.GetValueOrDefault(),
            LeaveType = leaveType,
            TargetDeskCode = targetDeskCode,
            ShouldFade = shouldFade,
        };
        return RunOriginal;
    }

    /// <summary>
    /// 带参普通客人可能因游戏内部判定直接返回 null，此处兜底清理暂存参数。
    /// </summary>
    [HarmonyPatch(nameof(GuestsManager.SpawnNormalGuestGroup), [
        typeof(Il2CppSystem.Collections.Generic.IEnumerable<NormalGuest>),
        typeof(Il2CppSystem.Nullable<UnityEngine.Vector3>),
        typeof(GuestGroupController.LeaveType),
        typeof(int),
        typeof(bool),
    ])]
    [HarmonyPostfix]
    public static void SpawnNormalGuestGroup_WithArgs_Postfix()
    {
        _pendingNormalSpawnArgs = null;
    }

    private static PendingSpawnArgs? ConsumePendingNormalSpawnArgs()
    {
        var args = _pendingNormalSpawnArgs;
        _pendingNormalSpawnArgs = null;
        return args;
    }

    /// <summary>
    /// 主机生成稀客，房间内有人不可用时重抽；客机不自行生成。剧情中两端按原版生成本端顾客。
    /// </summary>
    [HarmonyPatch(nameof(GuestsManager.SpawnSpecialGuestGroup))]
    [HarmonyPrefix]
    public static bool SpawnSpecialGuestGroup_Prefix(
        ref int id,
        SpecialGuestsController.GuestSpawnType guestSpawnType,
        Il2CppSystem.Nullable<Vector3> overrideSpawnPosition,
        GuestGroupController.LeaveType leaveType,
        int targetDeskCode,
        bool shouldFade,
        ref SpecialGuestsController __result)
    {
        if (IsReimuProtectionGuest(id)) return RunOriginal;
        if (!GameSession.HasRoomPeers || GameFlow.InStory) return RunOriginal;
        if (GameSession.IsRoomClient || !TryResolveAvailableSpecialGuest(ref id))
        {
            __result = null;
            return SkipOriginal;
        }
        _pendingSpecialSpawnArgs = new PendingSpawnArgs
        {
            HasOverrideSpawnPosition = overrideSpawnPosition.HasValue,
            OverrideSpawnPosition = overrideSpawnPosition.GetValueOrDefault(),
            LeaveType = leaveType,
            TargetDeskCode = targetDeskCode,
            ShouldFade = shouldFade,
            GuestSpawnType = guestSpawnType,
        };
        return RunOriginal;
    }

    [HarmonyPatch(nameof(GuestsManager.SpawnSpecialGuestGroup))]
    [HarmonyPostfix]
    public static void SpawnSpecialGuestGroup_Postfix()
    {
        // 游戏可能提前返回 null，清理未被消费的出生参数。
        _pendingSpecialSpawnArgs = null;
    }

    private static PendingSpawnArgs? ConsumePendingSpecialSpawnArgs()
    {
        var args = _pendingSpecialSpawnArgs;
        _pendingSpecialSpawnArgs = null;
        return args;
    }

    /// <summary>
    /// 当主机生成的稀客在联机房间全局不可用时，重新生成新的稀客
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    private static bool TryResolveAvailableSpecialGuest(ref int id)
    {
        var configure = IzakayaConfigure.Instance;
        var maxAttempts = (configure.SpecialGuestPoolIdentityData?.Length ?? 0) + 1;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (id != -1)
            {
                if (PlayerManager.SpecialGuestAvailable(id)) return true;

                configure.SetThisGuestHasSpawned(id);
                Log.Warning($"Special guest {id} is not available for all players, rerolling.");
            }

            if (!configure.CanGacha) return false;
            id = configure.Gacha();
        }
        return false;
    }


    /// <summary>
    /// 主机在剧情外生成的顾客登记并广播，此后进入同步；剧情中生成的顾客和灵梦保护不登记，两端各按原版运行。
    /// </summary>
    [HarmonyPatch(nameof(GuestsManager.PostInitializeGuestGroup))]
    [HarmonyPrefix]
    public static void PostInitializeGuestGroup_Prefix(GuestGroupController initializedController)
    {
        var spawnArgs = initializedController.ControllType switch
        {
            GuestsManager.GuestType.Normal => ConsumePendingNormalSpawnArgs(),
            GuestsManager.GuestType.Special => ConsumePendingSpecialSpawnArgs(),
            _ => null,
        };
        if (!GameSession.HasRoomPeers || !GameSession.IsRoomHost || GameFlow.InStory) return;
        if (IsReimuProtectionGuest(initializedController) || GuestRegistry.Of(initializedController) != null) return;
        GuestSpawnMessage.Detect(initializedController, spawnArgs);
    }

    /// <summary>客机入座由主机决定：生成时视为已处理，出队时视为没有空位。</summary>
    [HarmonyPatch(nameof(GuestsManager.TrySendToSeat))]
    [HarmonyPrefix]
    public static bool TrySendToSeat_Prefix(GuestGroupController toTry, bool firstSpawn, ref bool __result)
    {
        if (GuestRegistry.Mode(toTry) != GuestMode.Client) return RunOriginal;
        __result = firstSpawn;
        return SkipOriginal;
    }

    /// <summary>主机入座收口：生成即入座与出队入座。</summary>
    [HarmonyPatch(nameof(GuestsManager.TrySendToSeat))]
    [HarmonyPostfix]
    public static void TrySendToSeat_Postfix(GuestGroupController toTry, bool __result)
    {
        if (__result && GuestRegistry.Mode(toTry) == GuestMode.Host) GuestSeatMessage.Detect(toTry);
    }

    /// <summary>客机点单只在重放主机订单时执行。</summary>
    [HarmonyPatch(nameof(GuestsManager.GenerateOrderSession))]
    [HarmonyPrefix]
    public static bool GenerateOrderSession_Prefix(GuestGroupController guestGroup) =>
        GuestRegistry.Mode(guestGroup) != GuestMode.Client || GuestRegistry.Of(guestGroup).PendingOrder.HasValue
            ? RunOriginal : SkipOriginal;

    /// <summary>吃完进入续单判定，记为已吃完；客机的续单与否由主机决定。</summary>
    [HarmonyPatch(nameof(GuestsManager.MainOrderCycle))]
    [HarmonyPrefix]
    public static bool MainOrderCycle_Prefix(GuestGroupController toCycle)
    {
        var mode = GuestRegistry.Mode(toCycle);
        if (mode is GuestMode.Host or GuestMode.Client && GuestRegistry.Of(toCycle) is { State: GuestPhase.Evaluating } guest)
            guest.State = GuestPhase.Eaten;
        return mode == GuestMode.Client ? SkipOriginal : RunOriginal;
    }

    /// <summary>客机只重放主机评价；本地上齐触发的评价跳过。</summary>
    [HarmonyPatch(nameof(GuestsManager.EvaluateOrder))]
    [HarmonyPrefix]
    public static bool EvaluateOrder_Prefix(GuestGroupController toEvaluate)
    {
        if (GuestRegistry.Mode(toEvaluate) != GuestMode.Client) return RunOriginal;
        var guest = GuestRegistry.Of(toEvaluate);
        return guest.State == GuestPhase.Evaluating && guest.FinalEval != GuestGroupController.EvaluationResult.Null
            ? RunOriginal : SkipOriginal;
    }

    /// <summary>
    /// 评价台词显示时结果已经过彩蛋与改判回调。主机在此广播最终结果；客机显示主机结果。
    /// </summary>
    [HarmonyPatch(nameof(GuestsManager.ShowEvaluationDialog))]
    [HarmonyPrefix]
    public static void ShowEvaluationDialog_Prefix(GuestGroupController __0, ref GuestGroupController.EvaluationResult __2)
    {
        switch (GuestRegistry.Mode(__0))
        {
            case GuestMode.Host:
                EvaluateOrderMessage.Detect(__0, __2);
                break;
            case GuestMode.Client when GuestRegistry.Of(__0).FinalEval is var final && final != GuestGroupController.EvaluationResult.Null:
                __2 = final;
                break;
        }
    }

    /// <summary>
    /// 客机玩家赶客：本地立即执行原版并记下分支，离桌时上报。主机本地赶客照常执行。
    /// </summary>
    [HarmonyPatch(nameof(GuestsManager.PlayerRepell))]
    [HarmonyPrefix]
    public static bool PlayerRepell_Prefix(int deskCode)
    {
        var manager = GuestsManager.Instance;
        var guest = manager.GetInDeskGuest(deskCode);
        switch (GuestRegistry.Mode(guest))
        {
            case GuestMode.Client:
                if (GuestRegistry.Of(guest).IsLeft) return SkipOriginal;
                PlayerRepellMessage.Local(guest);
                return RunOriginal;
            case GuestMode.Host:
                return guest.HaveNotLeft() && manager.CheckCanPlayerRepelGuest(deskCode) ? RunOriginal : SkipOriginal;
            default:
                return RunOriginal;
        }
    }

    /// <summary>
    /// 主机标记离场原因，由离桌或未入座离开的收口广播。客机只执行本端赶客，其余等待主机。
    /// </summary>
    [HarmonyPatch(nameof(GuestsManager.RepellInternal))]
    [HarmonyPrefix]
    public static bool RepellInternal_Prefix(GuestGroupController guestGroupController)
    {
        if (Bypass.Active(guestGroupController)) return RunOriginal;
        switch (GuestRegistry.Mode(guestGroupController))
        {
            case GuestMode.Host:
                GuestRegistry.Of(guestGroupController).LeaveReason = LeaveReason.Repel;
                return RunOriginal;
            case GuestMode.Client:
                var guest = GuestRegistry.Of(guestGroupController);
                if (guest.LocalRepel == null || guest.IsLeft) return SkipOriginal;
                PlayerRepellMessage.Report(guest);
                // 放行其中的离桌。
                Bypass.Grant(guestGroupController);
                return RunOriginal;
            default:
                return RunOriginal;
        }
    }

    /// <summary>桌上耐心耗尽：主机标记原因；客机等待主机。</summary>
    [HarmonyPatch(nameof(GuestsManager.PatientDepletedLeave))]
    [HarmonyPrefix]
    public static bool PatientDepletedLeave_Prefix(GuestGroupController toPatientDepletedLeave)
    {
        if (Bypass.Active(toPatientDepletedLeave)) return RunOriginal;
        switch (GuestRegistry.Mode(toPatientDepletedLeave))
        {
            case GuestMode.Host:
                GuestRegistry.Of(toPatientDepletedLeave).LeaveReason = LeaveReason.PatientDepleted;
                return RunOriginal;
            case GuestMode.Client:
                return SkipOriginal;
            default:
                return RunOriginal;
        }
    }

    /// <summary>
    /// 离桌收口：主机广播；客机只执行本端赶客与主机事件重放。
    /// 单机不暂停时客人可能在上菜面板打开期间离桌，先关闭面板并退回物品。
    /// </summary>
    [HarmonyPatch(nameof(GuestsManager.LeaveFromDesk))]
    [HarmonyPrefix]
    public static bool LeaveFromDesk_Prefix(GuestGroupController toLeave, GuestGroupController.LeaveType leaveType)
    {
        if (GameTimeManagerPatch.NoPanelPause) ServeSlots.ClosePanel(toLeave.DeskCode, null, returnLocal: true);
        if (Bypass.Active(toLeave)) return RunOriginal;
        switch (GuestRegistry.Mode(toLeave))
        {
            case GuestMode.Host:
                GuestLeaveMessage.DetectDesk(toLeave, leaveType);
                return RunOriginal;
            case GuestMode.Client:
                // 吃完付款离开被截住：断线时据此补上离桌。
                if (GuestRegistry.Of(toLeave) is { State: GuestPhase.Evaluating or GuestPhase.Eaten } guest)
                    guest.State = GuestPhase.Leaving;
                return SkipOriginal;
            default:
                return RunOriginal;
        }
    }

    /// <summary>
    /// 主机打烊同步。客机跳过。
    /// </summary>
    /// <returns></returns>
    [HarmonyPatch(nameof(GuestsManager.TryCloseIzakaya))]
    [HarmonyPrefix]
    public static bool TryCloseIzakaya_Prefix()
    {
        if (GameFlow.ShouldSkipAction || !GameSession.HasRoomPeers) return RunOriginal;
        if (NightSceneEventManagerPatch.IsHostCloseReplay) return RunOriginal;
        if (GameSession.IsRoomHost)
        {
            IzakayaCloseMessage.Send();
            return RunOriginal;
        }

        if (GameSession.IsRoomClient)
        {
            return SkipOriginal;
        }

        return RunOriginal;
    }


    /// <summary>
    /// TryCloseIzakaya 的反向补丁，用于客机重放。
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPatch(nameof(GuestsManager.TryCloseIzakaya))]
    [HarmonyReversePatch]
    public static void TryCloseIzakaya_ReversePatch(GuestsManager __instance) { }


    [HarmonyPatch(nameof(GuestsManager.AddToPatientCountdown))]
    [HarmonyPostfix]
    public static void AddToPatientCountdown_Postfix(GuestGroupController toCountDown)
    {
        ExtendYuyukoPhase3Patient(toCountDown);
    }

    private static void ExtendYuyukoPhase3Patient(GuestGroupController controller)
    {
        if (!GameSession.HasRoomPeers || !PrepSceneManager.IsYuyukoChallenge
            || PrepSceneManager.YuyukoPrepRound != 3 || PrepSceneManager.IsYuyukoPrepActive) return;
        if (!controller.GetAllGuests().ToArray().Any(guest => guest.Id is 23 or 40)) return;

        // 仅扩展 AddToPatientCountdown 已初始化的倒计时；手动订单不重置实际耐心，不能复用此处理。
        int originalPatient = controller.CurrentPatient;
        controller.MaxPatient *= 3;
        GuestsManager.Instance.SetManualControlledPatient(controller, originalPatient * 3);
        Log.Info($"幽幽子三阶段耐心：{originalPatient} → {controller.CurrentPatient}");
    }

    /// <summary>LeaveFromDesk 的反向补丁，用于强制清理。</summary>
    [HarmonyPatch(nameof(GuestsManager.LeaveFromDesk))]
    [HarmonyReversePatch]
    public static void LeaveFromDesk_ReversePatch(
        GuestsManager __instance,
        GuestGroupController toLeave,
        GuestGroupController.LeaveType leaveType,
        Il2CppSystem.Action leaveAction,
        bool triggerLeaveBuff)
    { }
}
