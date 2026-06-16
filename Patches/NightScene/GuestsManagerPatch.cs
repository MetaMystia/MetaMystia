using HarmonyLib;
using Il2CppSystem.Linq;
using System;
using System.Linq;
using UnityEngine;

using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using GameData.RunTime.NightSceneUtility;
using NightScene.GuestManagementUtility;

using MetaMystia.Network;
using MetaMystia.Network.Services;
using MetaMystia.Protocol.Enums;
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
[TracePatch(nameof(GuestsManager.Method_Private_Void_GuestGroupController_PDM_1),
    DisplayName = "GuestsManager.PostInitializeGuestGroup.OnPatientDepleted(OnPatiendDepleted)")]
[TracePatch(nameof(GuestsManager.TryCloseIzakaya))]
[AutoLog]
public partial class GuestsManagerPatch
{
    private const int MaxNormalGuestRerollAttempts = 32;
    private const int ReimuProtectionGuestId = 7;

    public static readonly PatchBypassToken SkipPlayerRepellPatch = new();
    public static readonly PatchBypassToken SkipRepellInternalPatch = new();
    public static readonly PatchBypassToken SkipLeaveFromDeskPatch = new();
    public static readonly PatchBypassToken SkipRepellInternalLeaveBroadcastPatch = new();
    public static readonly PatchBypassToken SkipLeaveFromDeskBroadcastPatch = new();

    private static PendingNormalSpawnArgs? _pendingNormalSpawnArgs;

    public readonly struct PendingNormalSpawnArgs
    {
        public bool HasOverrideSpawnPosition { get; init; }
        public Vector3 OverrideSpawnPosition { get; init; }
        public GuestGroupController.LeaveType LeaveType { get; init; }
        public int TargetDeskCode { get; init; }
        public bool ShouldFade { get; init; }
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
    /// 主机生成无参普通客人。
    /// </summary>
    /// <returns></returns>
    [HarmonyPatch(nameof(GuestsManager.SpawnNormalGuestGroup), [])]
    [HarmonyPrefix]
    public static bool SpawnNormalGuestGroup_Prefix()
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;

        if (MpManager.IsRoomHost)
        {
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

        if (MpManager.IsRoomClient)
        {
            // 客机不生成顾客，应由主机通过联机 OnSpawn 通知生成
            Log.Info("Skipping SpawnNormalGuestGroup on client");
            return SkipOriginal;
        }

        return RunOriginal;
    }

    /// <summary>
    /// 主机生成带参普通客人时，暂存额外生成参数；客机不允许本地自发生成。
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
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsRoomClient)
        {
            __result = null;
            return SkipOriginal;
        }

        if (MpManager.IsRoomHost)
        {
            if (!TryReplaceUnavailableNormalGuests(ref normalGuests))
            {
                Log.Warning("No globally available normal guest fallback found, skipping spawn.");
                __result = null;
                return SkipOriginal;
            }

            _pendingNormalSpawnArgs = new PendingNormalSpawnArgs
            {
                HasOverrideSpawnPosition = overrideSpawnPosition.HasValue,
                OverrideSpawnPosition = overrideSpawnPosition.GetValueOrDefault(),
                LeaveType = leaveType,
                TargetDeskCode = targetDeskCode,
                ShouldFade = shouldFade,
            };
        }

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

    private static PendingNormalSpawnArgs? ConsumePendingNormalSpawnArgs()
    {
        var args = _pendingNormalSpawnArgs;
        _pendingNormalSpawnArgs = null;
        return args;
    }

    /// <summary>
    /// 主机生成指定稀客，并判定
    /// </summary>
    /// <param name="id"></param>
    /// <param name="__result"></param>
    /// <returns></returns>
    [HarmonyPatch(nameof(GuestsManager.SpawnSpecialGuestGroup))]
    [HarmonyPrefix]
    public static bool SpawnSpecialGuestGroup_Prefix(ref int id, ref SpecialGuestsController __result)
    {
        if (IsReimuProtectionGuest(id)) return RunOriginal;
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsRoomClient)
        {
            __result = null;
            return SkipOriginal;
        }
        if (MpManager.IsRoomHost)
        {
            if (TryResolveAvailableSpecialGuest(ref id)) return RunOriginal;
            __result = null;
            return SkipOriginal;
        }

        return RunOriginal;
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


    [HarmonyPatch(nameof(GuestsManager.PostInitializeGuestGroup))]
    [HarmonyPrefix]
    public static void PostInitializeGuestGroup_Prefix(GuestGroupController initializedController)
    {
        if (IsReimuProtectionGuest(initializedController)) return;
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;
        if (MpManager.IsRoomHost)
        {
            // 将主机生成的顾客信息广播给客机
            var normalSpawnArgs = initializedController.ControllType == GuestsManager.GuestType.Normal
                ? ConsumePendingNormalSpawnArgs()
                : null;
            GuestFSM.OnSpawn(initializedController, normalSpawnArgs);
        }
    }

    /// <summary>
    /// 主机和客机玩家赶客
    /// </summary>
    /// <param name="deskCode"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    [HarmonyPatch(nameof(GuestsManager.PlayerRepell))]
    [HarmonyPrefix]
    public static bool PlayerRepell_Prefix(int deskCode)
    {
        if (SkipPlayerRepellPatch.TryConsume())
        {
            SkipRepellInternalPatch.Grant(); // TODO
            if (MpManager.IsRoomHost) SkipRepellInternalLeaveBroadcastPatch.Grant();
            return RunOriginal;
        }

        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;

        if (MpManager.IsRoomHost)
        {
            SkipRepellInternalLeaveBroadcastPatch.Grant();
            GuestFSM.OnPlayerRepell(deskCode);
            return RunOriginal;
        }
        if (MpManager.IsRoomClient)
        {
            GuestFSM.OnPlayerRepell(deskCode);
            // 客机会阻止 RepellInternal LeaveFromDesk 等方法的调用，因此不仅需要 return RunOriginal
            // 还需要逐级设置 Skip*Patch 以跳过客机 Prefix 中的跳过逻辑
            SkipRepellInternalPatch.Grant();
            return RunOriginal;
        }

        throw new InvalidOperationException("Unexpected network state in PlayerRepell_Prefix");
    }

    [HarmonyPatch(nameof(GuestsManager.PlayerRepell))]
    [HarmonyPostfix]
    public static void PlayerRepell_Postfix()
    {
        SkipRepellInternalLeaveBroadcastPatch.Reset();
        SkipLeaveFromDeskBroadcastPatch.Reset();
    }


    /// <summary>
    /// 顾客点单满足，触发订单评价。
    /// 主机正常执行，客机根据 OverrideEvalResult 判断是重放中(正常执行) 还是游戏主动调用(跳过)。
    /// </summary>
    /// <param name="toEvaluate"></param>
    /// <returns></returns>
    [HarmonyPatch(nameof(GuestsManager.EvaluateOrder))]
    [HarmonyPrefix]
    public static bool EvaluateOrder_Prefix(GuestGroupController toEvaluate)
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsRoomHost)
        {
            return RunOriginal;
        }
        if (MpManager.IsRoomClient)
        {
            var fsm = GuestsMap.GetGuestFsm(toEvaluate);
            return fsm.OverrideEvalResult != GuestGroupController.EvaluationResult.Null
                ? RunOriginal
                : SkipOriginal;
        }

        return RunOriginal;
    }

    /// <summary>
    /// 订单评价结束，推进 Evaluating -> EatingDelay
    /// </summary>
    /// <param name="toEvaluate"></param>
    /// <param name="isTriggerByPartner"></param>
    [HarmonyPatch(nameof(GuestsManager.EvaluateOrder))]
    [HarmonyPostfix]
    public static void EvaluateOrder_Postfix(GuestGroupController toEvaluate, bool isTriggerByPartner)
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;
        if (MpManager.IsRoomHost)
        {
            // 主机端直接记录评价结束，推进 Evaluating -> EatingDelay
            GuestFSM.OnEatingDelay(toEvaluate);
            return;
        }
        if (MpManager.IsRoomClient)
        {
            var fsm = GuestsMap.GetGuestFsm(toEvaluate);
            if (fsm?.CurrentState == GuestFsmState.Evaluating &&
                fsm.OverrideEvalResult != GuestGroupController.EvaluationResult.Null)
            {
                // 客机客人正处于 Evaluating 且存在 OverrideEvalResult 时才推进 Evaluating -> EatingDelay
                // 否则认为是游戏主动执行的 EvaluateOrder，不执行状态推进
                // TODO: 是否能移除
                GuestFSM.OnEatingDelay(toEvaluate);
            }
        }
    }


    /// <summary>
    /// 顾客离开
    /// </summary>
    /// <param name="guestGroupController"></param>
    /// <returns></returns>
    [HarmonyPatch(nameof(GuestsManager.RepellInternal))]
    [HarmonyPrefix]
    public static bool RepellInternal_Prefix(GuestGroupController guestGroupController)
    {
        if (IsReimuProtectionGuest(guestGroupController)) return RunOriginal;

        var skipLeaveBroadcast = SkipRepellInternalLeaveBroadcastPatch.TryConsume();
        if (SkipRepellInternalPatch.TryConsume())
        {
            // 如果 PlayerRepell 已经触发并设置了 SkipRepellInternalPatch
            // 则同样设置 SkipLeaveFromDeskPatch 以正常执行 LeaveFromDesk
            SkipLeaveFromDeskPatch.Grant();
        }
        if (skipLeaveBroadcast && MpManager.IsRoomHost)
        {
            SkipLeaveFromDeskBroadcastPatch.Grant();
        }

        return RunOriginal;
    }


    /// <summary>
    /// 尝试送顾客入座，客机需短路 PostInitializeGuestGroup
    /// </summary>
    /// <param name="__result"></param>
    /// <returns></returns>
    [HarmonyPatch(nameof(GuestsManager.TrySendToSeat))]
    [HarmonyPrefix]
    public static bool TrySendToSeat_Prefix(GuestGroupController toTry, ref bool __result)
    {
        if (IsReimuProtectionGuest(toTry)) return RunOriginal;
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsRoomClient)
        {
            // 客机需要返回 true 并跳过原逻辑以短路 PostInitializeGuestGroup
            __result = true;
            return SkipOriginal;
        }
        return RunOriginal;
    }


    /// <summary>
    /// 主机顾客生成点单会话，客机跳过。
    /// </summary>
    /// <param name="guestGroup"></param>
    /// <returns></returns>
    [HarmonyPatch(nameof(GuestsManager.GenerateOrderSession))]
    [HarmonyPrefix]
    public static bool GenerateOrderSession_Prefix(GuestGroupController guestGroup)
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsRoomClient)
        {
            // 客机在 DoGenerateOrderSession 中直接调用 GenerateOrderSession 或通过调用 FirstOrder 而间接调用 DoGenerateOrderSession 前
            // 将订单结果暂存至 PendingOrder，若此处检查到 PendingOrder 则认定为是重放状态
            if (GuestsMap.GetGuestFsm(guestGroup)?.PendingOrder.HasValue == true)
            {
                return RunOriginal;
            }
            return SkipOriginal;
        }

        return RunOriginal;
    }

    /// <summary>
    /// 主机顾客开始一轮点单，客机跳过。
    /// </summary>
    /// <param name="toCycle"></param>
    /// <returns></returns>
    [HarmonyPatch(nameof(GuestsManager.MainOrderCycle))]
    [HarmonyPrefix]
    public static bool MainOrderCycle_Prefix(GuestGroupController toCycle)
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsRoomHost)
        {
            return RunOriginal;
        }
        if (MpManager.IsRoomClient)
        {
            // 客机的 MainOrderCycle 当被跳过，只有主机有权主动推进
            return SkipOriginal;
        }

        return RunOriginal;
    }

    /// <summary>
    /// 主机顾客出队入座，劫持以获取顾客信息，客机跳过。
    /// </summary>
    /// <returns></returns>
    [HarmonyPatch(nameof(GuestsManager.CheckAndSendFromQueue))]
    [HarmonyPrefix]
    public static bool CheckAndSendFromQueue_Prefix()
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsRoomHost)
        {
            // 主机需要精准捕获成功出队入座的顾客，因此劫持到 HijackCheckAndSendFromQueue 进行精准捕获与同步
            GuestService.HijackCheckAndSendFromQueue();
            return SkipOriginal;
        }

        if (MpManager.IsRoomClient)
        {
            return SkipOriginal;
        }

        return RunOriginal;
    }

    /// <summary>
    /// 队内顾客耐心耗尽，主机同步，客机跳过。
    /// </summary>
    /// <param name="guest"></param>
    /// <returns></returns>
    [HarmonyPatch(nameof(GuestsManager.Method_Private_Void_GuestGroupController_PDM_1))]
    [HarmonyPrefix]
    public static bool OnPatientDepleted_Prefix(GuestGroupController guest)
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsRoomHost)
        {
            GuestFSM.OnPatientDepletedInQueue(guest);
        }

        if (MpManager.IsRoomClient)
        {
            return SkipOriginal;
        }

        return RunOriginal;
    }

    /// <summary>
    /// 主机打烊同步。客机跳过。
    /// </summary>
    /// <returns></returns>
    [HarmonyPatch(nameof(GuestsManager.TryCloseIzakaya))]
    [HarmonyPrefix]
    public static bool TryCloseIzakaya_Prefix()
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (NightSceneEventManagerPatch.IsHostCloseReplay) return RunOriginal;
        if (MpManager.IsRoomHost)
        {
            WorkSceneServices.SendIzakayaCloseBroadcast();
            return RunOriginal;
        }

        if (MpManager.IsRoomClient)
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


    /// <summary>
    /// 主机桌上客人耐心耗尽。
    /// </summary>
    /// <param name="toPatientDepletedLeave"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    [HarmonyPatch(nameof(GuestsManager.PatientDepletedLeave))]
    [HarmonyPrefix]
    public static bool PatientDepletedLeave_Prefix(GuestGroupController toPatientDepletedLeave)
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsRoomHost)
        {
            // 上游 PatientDepletedDeskAction 已会让客机完整重放 PatientDepletedLeave 链路
            // (含末端 LeaveFromDesk)，避免 LeaveFromDesk_Postfix 再发 GuestLeaveAction。
            SkipLeaveFromDeskPatch.Grant();
            GuestFSM.OnPatientDepletedAtDesk(toPatientDepletedLeave);
            return RunOriginal;
        }
        if (MpManager.IsRoomClient)
        {
            return SkipOriginal;
        }

        return RunOriginal;
    }

    /// <summary>
    /// 顾客离桌，主机同步，客机跳过。
    /// </summary>
    /// <param name="toLeave"></param>
    /// <param name="leaveType"></param>
    /// <param name="triggerLeaveBuff"></param>
    /// <returns></returns>
    [HarmonyPatch(nameof(GuestsManager.LeaveFromDesk))]
    [HarmonyPrefix]
    public static bool LeaveFromDesk_Prefix(
        GuestGroupController toLeave,
        GuestGroupController.LeaveType leaveType,
        bool triggerLeaveBuff)
    {
        if (IsReimuProtectionGuest(toLeave)) return RunOriginal;

        if (SkipLeaveFromDeskPatch.TryConsume())
        {
            return RunOriginal;
        }

        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsRoomHost)
        {
            GuestFSM.OnLeaveFromDesk(
                toLeave,
                leaveType,
                triggerLeaveBuff,
                broadcast: !SkipLeaveFromDeskBroadcastPatch.TryConsume());
            return RunOriginal;
        }

        if (MpManager.IsRoomClient)
        {
            return SkipOriginal;
        }

        return RunOriginal;
    }


    /// <summary>
    /// LeaveFromDesk 的反向补丁，用于客机重放。
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="toLeave"></param>
    /// <param name="leaveType"></param>
    /// <param name="leaveAction"></param>
    /// <param name="triggerLeaveBuff"></param>
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
