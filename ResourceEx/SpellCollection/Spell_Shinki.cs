// 这里是描述神绮符卡的具体实施情况的代码
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.UI;

using Common.CharacterUtility;
using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;
using SgrYuki;

using MetaMystia.Network;
using MetaMystia.Patch;

namespace MetaMystia.ResourceEx.SpellCollection;

[AutoLog]
public partial class Spell_Shinki : SpellBase
{
    // ================================================================================
    // 日志辅助
    // ================================================================================
    private static void DiagLog(string message)
    {
        Log.Info(message);
        UnityEngine.Debug.Log($"[MetaMystia] {message}");
    }

    // ================================================================================
    // 静态状态
    // ================================================================================
    private static bool _portalActive;
    private static string _portalTimerId = "ShinkiPortal";
    private static int _shinkiCharacterId = -1;
    private static int _shinkiResourceExId = -1;
    private static readonly List<int> _makaiSpecialGuestIds = [];
    private static readonly List<int> _makaiNormalGuestIds = [];
    private static Vector3 _portalPosition;
    private static GameObject _portalVisual;

    // 位置参数 --------------------------------------------------------------------------
    private const float PortalScreenXRatio = 0.50f;
    private const float PortalScreenYRatio = 0.25f;
    private const float PortalWorldOffsetX = 1.5f; // 世界坐标 X 轴右移
    // -----------------------------------------------------------------------------------

    private const float BlackCardPortalDisplayDuration = 1.5f;

    // ================================================================================
    // SpellBase 重写
    // ================================================================================

    public override string OnGettingSpellOwnerIdentifier()
    {
        return _shinkiLabel;
    }

    public override Il2CppSystem.Collections.IEnumerator OnPositiveBuffExecute(
        SpellExecutionContext spellExecutionContext)
        => PositiveBuffRoutine(spellExecutionContext).WrapToIl2Cpp();

    public override Il2CppSystem.Collections.IEnumerator OnNegativeBuffExecute(
        SpellExecutionContext spellExecutionContext)
        => NegativeBuffRoutine(spellExecutionContext).WrapToIl2Cpp();

    // ================================================================================
    // 黑卡：绮符【环游魔界80天】
    // ================================================================================

    // 神绮待机位置偏移（相对于 portalPos，偏左）
    private static readonly Vector3 ShinkiStandOffset = new Vector3(1.5f, 0f, 0f);

    [HideFromIl2Cpp]
    private IEnumerator NegativeBuffRoutine(SpellExecutionContext ctx)
    {
        DiagLog("Shinki Black Card: 绮符【环游魔界80天】 activated!");

        // 1. 收集所有活跃客人，分离神绮和其他客人
        var allGuests = GuestsMap.GetAllGuestsSnapshot();
        DiagLog($"Black Card: GuestsMap snapshot count={allGuests.Count}");

        (int rid, GuestFSM fsm, bool wasSeated)? shinkiGuest = null;
        var affectedGuests = new List<(int runtimeId, GuestFSM fsm, bool wasSeated)>();

        foreach (var (rid, fsm) in allGuests)
        {
            var state = fsm.CurrentState;
            var ctrlNull = fsm.Controller == null;
            var ids = fsm.Ids != null ? string.Join(",", fsm.Ids) : "null";
            DiagLog($"  Guest rid={rid}: state={state}, ctrlNull={ctrlNull}, ids=[{ids}], shinkiId={_shinkiCharacterId}");

            if (ctrlNull) continue;
            if (state == GuestFSM.State.Left || state == GuestFSM.State.Dead || state == GuestFSM.State.None) continue;

            var isShinki = fsm.Ids != null &&
                (fsm.Ids.Contains(_shinkiCharacterId) ||
                 (_shinkiResourceExId > 0 && fsm.Ids.Contains(_shinkiResourceExId)));
            var wasSeated = fsm.Controller.DeskCode != -1;

            if (isShinki)
                shinkiGuest = (rid, fsm, wasSeated);
            else
                affectedGuests.Add((rid, fsm, wasSeated));
        }

        DiagLog($"Black Card: affected={affectedGuests.Count}, shinkiGuest={(shinkiGuest != null ? shinkiGuest.Value.rid.ToString() : "null")}");

        if (affectedGuests.Count == 0 && shinkiGuest == null)
        {
            DiagLog("Black Card: no guests at all, skipping");
            yield break;
        }

        // 2. 计算位置
        _portalPosition = DeterminePortalPosition();
        var shinkiStandPos = _portalPosition + ShinkiStandOffset; // 神绮待机位置：传送门偏左
        DiagLog($"Black Card: portalPos={_portalPosition}, shinkiStand={shinkiStandPos}");

        // 3. Phase 1 清理（所有客人 + 神绮：清理订单/面板/队列，保留桌位）
        foreach (var (rid, fsm, _) in affectedGuests)
        {
            DiagLog($"  Phase1: cleaning guest rid={rid}");
            PartialCleanupForBlackCard(fsm.Controller);
        }
        if (shinkiGuest != null)
        {
            DiagLog($"  Phase1: cleaning Shinki rid={shinkiGuest.Value.rid}");
            PartialCleanupForBlackCard(shinkiGuest.Value.fsm.Controller);
        }

        // === 4. 神绮移动到待机位置（偏左），用回调检测移动完成 ===
        var shinkiArrived = false;
        if (shinkiGuest != null && shinkiGuest.Value.fsm.Controller != null)
        {
            DiagLog($"  Shinki walking to stand pos, rid={shinkiGuest.Value.rid}, pos={shinkiStandPos}");
            System.Action<GuestGroupController> onShinkiStandArrive = _ => { shinkiArrived = true; DiagLog("  Shinki: arrived at standby pos"); };
            shinkiGuest.Value.fsm.Controller.MoveToTargetPosition(
                -1, new Il2CppSystem.Nullable<Vector3>(shinkiStandPos), Vector3Int.zero, false,
                Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GuestGroupController>>(onShinkiStandArrive));
        }
        else
        {
            shinkiArrived = true;
        }

        // 逐帧等待神绮到达
        while (!shinkiArrived) yield return null;

        // === 5. 神绮就位后，开启传送门 ===
        DiagLog("Black Card: creating portal");
        CreatePortalVisual(_portalPosition);

        // === 6. 广播网络同步 ===
        var affectedIds = affectedGuests.Select(g => g.runtimeId).ToArray();
        int shinkiRid = shinkiGuest?.rid ?? -1;
        if (MpManager.IsConnected)
        {
            ShinkiBlackCardAction.Send(affectedIds, shinkiRid, _portalPosition);
        }

        // === 7. 其他客人零资金结算 + 走向传送门，每人独立回调 ===
        var guestsArrivedFlags = new bool[affectedGuests.Count];
        for (int i = 0; i < affectedGuests.Count; i++)
        {
            var (rid, fsm, _) = affectedGuests[i];
            if (fsm.Controller == null)
            {
                guestsArrivedFlags[i] = true;
                continue;
            }

            // 零资金结算
            fsm.Controller.GetFund = 0;
            DiagLog($"  Guest rid={rid}: zero fund → MoveToTargetPosition(portal)");

            int idx = i;
            System.Action<GuestGroupController> onGuestPortalArrive = _ =>
            {
                guestsArrivedFlags[idx] = true;
                DiagLog($"  Guest rid={rid}: arrived at portal");
            };
            fsm.Controller.MoveToTargetPosition(
                -1, new Il2CppSystem.Nullable<Vector3>(_portalPosition), Vector3Int.zero, false,
                Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GuestGroupController>>(onGuestPortalArrive));
        }

        // === 8. 逐帧检测：每位客人到达后依次淡出（每次只处理一个，间隔小延迟） ===
        var guestsFaded = new bool[affectedGuests.Count];
        int fadedCount = 0;
        while (fadedCount < affectedGuests.Count)
        {
            yield return null;
            // 每次只淡出一个到达的客人
            for (int i = 0; i < affectedGuests.Count; i++)
            {
                if (!guestsArrivedFlags[i] || guestsFaded[i]) continue;

                var (rid, fsm, _) = affectedGuests[i];
                if (fsm.Controller != null)
                {
                    DiagLog($"  Guest rid={rid}: arrived at portal, fading out");
                    GuestsManagerPatch.SkipLeaveFromDeskPatch.Grant();
                    GuestsManagerPatch.SkipLeaveFromDeskBroadcastPatch.Grant();
                    GuestsManagerPatch.LeaveFromDesk_ReversePatch(
                        GuestsManager.Instance, fsm.Controller, GuestGroupController.LeaveType.Fading, null, false);
                }
                GuestsMap.Remove(rid);
                guestsFaded[i] = true;
                fadedCount++;
                yield return new WaitForSeconds(0.25f); // 淡出间隔
                break; // 每帧只处理一个
            }
        }
        DiagLog("Black Card: all affected guests faded out");

        // === 9. 等待 1s 后，神绮走到传送门并淡出 ===
        yield return new WaitForSeconds(1f);
        var shinkiArrivedAtPortal = false;
        if (shinkiGuest != null && shinkiGuest.Value.fsm.Controller != null)
        {
            DiagLog($"  Shinki walking to portal, rid={shinkiGuest.Value.rid}, pos={_portalPosition}");
            System.Action<GuestGroupController> onShinkiPortalArrive = _ =>
            {
                shinkiArrivedAtPortal = true;
                DiagLog("  Shinki: arrived at portal");
            };
            shinkiGuest.Value.fsm.Controller.MoveToTargetPosition(
                -1, new Il2CppSystem.Nullable<Vector3>(_portalPosition), Vector3Int.zero, false,
                Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GuestGroupController>>(onShinkiPortalArrive));
        }
        else
        {
            shinkiArrivedAtPortal = true;
        }

        while (!shinkiArrivedAtPortal) yield return null;

        // === 10. 神绮淡出 ===
        if (shinkiGuest != null)
        {
            DiagLog($"  Shinki fading out, rid={shinkiGuest.Value.rid}");
            if (shinkiGuest.Value.fsm.Controller != null)
            {
                GuestsManagerPatch.SkipLeaveFromDeskPatch.Grant();
                GuestsManagerPatch.SkipLeaveFromDeskBroadcastPatch.Grant();
                GuestsManagerPatch.LeaveFromDesk_ReversePatch(
                    GuestsManager.Instance, shinkiGuest.Value.fsm.Controller,
                    GuestGroupController.LeaveType.Fading, null, false);
            }
            GuestsMap.Remove(shinkiGuest.Value.rid);
        }

        // === 11. 展示传送门后销毁 ===
        yield return new WaitForSeconds(BlackCardPortalDisplayDuration);
        DestroyPortalVisual();

        DiagLog("Black Card: all guests banished to Makai!");
    }

    /// <summary>
    /// 切换神绮为举旗像素精灵（如果素材可用）
    /// </summary>
    private static void SwitchShinkiToFlagSprite(GuestGroupController ctrl)
    {
        if (ctrl == null || _flagSpriteSet == null) return;
        DiagLog("SwitchShinkiToFlagSprite: switching to flag sprite");
        ApplySpriteSetToGuest(ctrl, _flagSpriteSet);
    }

    /// <summary>
    /// 恢复神绮原始像素精灵
    /// </summary>
    private static void SwitchShinkiToOriginalSprite(GuestGroupController ctrl)
    {
        if (ctrl == null || _originalSpriteSet == null) return;
        DiagLog("SwitchShinkiToOriginalSprite: restoring original sprite");
        ApplySpriteSetToGuest(ctrl, _originalSpriteSet);
    }

    private static void ApplySpriteSetToGuest(GuestGroupController ctrl, CharacterSpriteSetCompact spriteSet)
    {
        // TODO: 运行时验证 GuestGroupController 到 CharacterControllerUnit 的访问路径
        // 可能的路径：ctrl.guestInstances 或 SceneDirector.characterCollection
        // 暂留为占位，待运行时验证后补充
        DiagLog($"ApplySpriteSetToGuest: called (spriteSet={spriteSet?.name ?? "null"})");
    }

    private static CharacterSpriteSetCompact _flagSpriteSet;
    private static CharacterSpriteSetCompact _originalSpriteSet;

    /// <summary>
    /// 阶段一清理：清理订单/面板/队列，但保留桌位（LeaveFromDesk 留到走到传送门后再触发）
    /// </summary>
    private static void PartialCleanupForBlackCard(GuestGroupController ctrl)
    {
        if (ctrl == null) return;

        if (ctrl.DeskCode != -1)
        {
            GuestsManager.Instance.RemoveFromPatientCountdown(ctrl);
            GuestFSM.TryCloseServePanel(ctrl.DeskCode);
        }
        else if (ctrl.queued)
        {
            ctrl.RemoveFromQueue();
            GuestsManager.Instance.RemoveFromPatientCountdown(ctrl);
        }
    }

    /// <summary>
    /// 客机重放黑卡效果（与主机新流程一致：回调检测移动完成，逐一淡出）
    /// </summary>
    public static void ReplayBlackCard(int[] affectedRuntimeIds, int shinkiRid, Vector3 portalPos)
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            _portalPosition = portalPos;
            var shinkiStandPos = portalPos + ShinkiStandOffset;

            // Phase 1 清理
            foreach (var rid in affectedRuntimeIds)
            {
                var fsm = GuestsMap.GetGuestFsm(rid);
                if (fsm?.Controller == null) continue;
                PartialCleanupForBlackCard(fsm.Controller);
            }
            if (shinkiRid > 0)
            {
                var shinkiFsm = GuestsMap.GetGuestFsm(shinkiRid);
                if (shinkiFsm?.Controller != null)
                    PartialCleanupForBlackCard(shinkiFsm.Controller);
            }

            // === 步骤1: 神绮走到待机位置（偏左），回调检测到达 ===
            var shinkiArrived = false;
            if (shinkiRid > 0)
            {
                var shinkiFsm = GuestsMap.GetGuestFsm(shinkiRid);
                if (shinkiFsm?.Controller != null)
                {
                    System.Action<GuestGroupController> onShinkiStandArrive2 = _ => { shinkiArrived = true; };
                    shinkiFsm.Controller.MoveToTargetPosition(
                        -1, new Il2CppSystem.Nullable<Vector3>(shinkiStandPos), Vector3Int.zero, false,
                        Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GuestGroupController>>(onShinkiStandArrive2));
                }
                else shinkiArrived = true;
            }
            else shinkiArrived = true;

            // 等待神绮到达 → 创建传送门 + 零资金 + 客人移动
            CommandScheduler.Enqueue(() => shinkiArrived, () =>
            {
                DiagLog("ReplayBlackCard: Shinki arrived, creating portal");
                CreatePortalVisual(portalPos);

                // === 步骤2: 其他客人零资金 + 走向传送门，每人独立回调 ===
                var guestsArrivedFlags = new bool[affectedRuntimeIds.Length];
                for (int i = 0; i < affectedRuntimeIds.Length; i++)
                {
                    var rid = affectedRuntimeIds[i];
                    var fsm = GuestsMap.GetGuestFsm(rid);
                    if (fsm?.Controller == null)
                    {
                        guestsArrivedFlags[i] = true;
                        continue;
                    }

                    fsm.Controller.GetFund = 0;

                    int idx = i;
                    System.Action<GuestGroupController> onGuestPortalArrive2 = _ => { guestsArrivedFlags[idx] = true; };
                    fsm.Controller.MoveToTargetPosition(
                        -1, new Il2CppSystem.Nullable<Vector3>(portalPos), Vector3Int.zero, false,
                        Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GuestGroupController>>(onGuestPortalArrive2));
                }

                // === 步骤3: 所有客人到达 → 淡出（客机批量） → 1s 后神绮走向传送门 ===
                CommandScheduler.Enqueue(() => guestsArrivedFlags.All(f => f), () =>
                {
                    DiagLog("ReplayBlackCard: all guests arrived, fading out");
                    foreach (var rid in affectedRuntimeIds)
                    {
                        var fsm = GuestsMap.GetGuestFsm(rid);
                        if (fsm?.Controller != null)
                        {
                            GuestsManagerPatch.SkipLeaveFromDeskPatch.Grant();
                            GuestsManagerPatch.SkipLeaveFromDeskBroadcastPatch.Grant();
                            GuestsManagerPatch.LeaveFromDesk_ReversePatch(
                                GuestsManager.Instance, fsm.Controller,
                                GuestGroupController.LeaveType.Fading, null, false);
                        }
                        GuestsMap.Remove(rid);
                    }

                    // === 1s 延迟后神绮走向传送门 ===
                    CommandScheduler.Enqueue(() => true, () =>
                    {
                        var shinkiArrivedAtPortal = false;
                        if (shinkiRid > 0)
                        {
                            var shinkiFsm = GuestsMap.GetGuestFsm(shinkiRid);
                            if (shinkiFsm?.Controller != null)
                            {
                                System.Action<GuestGroupController> onShinkiPortalArrive2 = _ => { shinkiArrivedAtPortal = true; };
                                shinkiFsm.Controller.MoveToTargetPosition(
                                    -1, new Il2CppSystem.Nullable<Vector3>(portalPos), Vector3Int.zero, false,
                                    Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GuestGroupController>>(onShinkiPortalArrive2));
                            }
                            else shinkiArrivedAtPortal = true;
                        }
                        else shinkiArrivedAtPortal = true;

                        // === 神绮到达传送门 → 淡出 → 销毁传送门 ===
                        CommandScheduler.Enqueue(() => shinkiArrivedAtPortal, () =>
                        {
                            DiagLog("ReplayBlackCard: Shinki fading out at portal");
                            if (shinkiRid > 0)
                            {
                                var shinkiFsm = GuestsMap.GetGuestFsm(shinkiRid);
                                if (shinkiFsm?.Controller != null)
                                {
                                    GuestsManagerPatch.SkipLeaveFromDeskPatch.Grant();
                                    GuestsManagerPatch.SkipLeaveFromDeskBroadcastPatch.Grant();
                                    GuestsManagerPatch.LeaveFromDesk_ReversePatch(
                                        GuestsManager.Instance, shinkiFsm.Controller,
                                        GuestGroupController.LeaveType.Fading, null, false);
                                }
                                GuestsMap.Remove(shinkiRid);
                            }

                            CommandScheduler.Enqueue(() => true, () =>
                            {
                                DiagLog("ReplayBlackCard: destroying portal");
                                DestroyPortalVisual();
                            }, "Shinki:ReplayDestroyPortal", timeoutSeconds: BlackCardPortalDisplayDuration + 1f);

                        }, "Shinki:ReplayShinkiArrivePortal", timeoutSeconds: 5f);

                    }, "Shinki:ReplayGuestsArrive", timeoutSeconds: 10f);

                }, "Shinki:ReplayGuestsArrive", timeoutSeconds: 10f);

            }, "Shinki:ReplayPortalCreated", timeoutSeconds: 3f);
        });
    }

    // ================================================================================
    // 红卡：【魔神降临】
    // ================================================================================

    [HideFromIl2Cpp]
    private IEnumerator PositiveBuffRoutine(SpellExecutionContext ctx)
    {
        DiagLog($"Red Card: 【魔神降临】 activated! _portalActive={_portalActive}");

        // 如果传送门已开启，直接召唤
        if (_portalActive)
        {
            DiagLog("Red Card: portal already active, summoning directly");
            SummonRandomMakaiGuests(2);
            yield break;
        }

        // 1. 首次开启传送门
        _portalActive = true;
        _portalPosition = DeterminePortalPosition();
        DiagLog($"Red Card: portal position = {_portalPosition}");
        CreatePortalVisual(_portalPosition);

        // 2. 广播网络同步
        if (MpManager.IsConnected)
        {
            ShinkiRedCardAction.Send(false);
        }

        // 3. 立即召唤首批客人
        DiagLog("Red Card: summoning initial batch of 2 guests");
        SummonRandomMakaiGuests(2);

        // 4. 注册定时召唤（每15秒）
        CommandScheduler.EnqueueInterval(_portalTimerId, 15f, OnPortalTick);
        DiagLog("Red Card: Makai portal opened! Guests will arrive every 15 seconds.");
        yield break;
    }

    /// <summary>
    /// 定时回调：每15秒召唤2位魔界客人
    /// </summary>
    private static void OnPortalTick()
    {
        if (GuestsManager.Instance == null || EventManager.Instance == null)
        {
            DiagLog("OnPortalTick: GuestsManager or EventManager null, closing portal");
            CleanupPortal();
            return;
        }

        var remaining = EventManager.Instance.TotalCountDown + EventManager.Instance.extraCountDown;
        DiagLog($"OnPortalTick: remaining={remaining}s, summoning 2 guests");
        if (remaining <= 0)
        {
            DiagLog("OnPortalTick: time up, closing portal");
            CleanupPortal();
            return;
        }

        SummonRandomMakaiGuests(2);
    }

    /// <summary>
    /// 召唤指定数量的随机魔界客人
    /// </summary>
    private static void SummonRandomMakaiGuests(int count)
    {
        if (GuestsManager.Instance == null)
        {
            DiagLog("SummonRandomMakaiGuests: GuestsManager.Instance is null, aborting");
            return;
        }

        var allIds = _makaiSpecialGuestIds.Concat(_makaiNormalGuestIds).ToList();
        if (allIds.Count == 0)
        {
            DiagLog("SummonRandomMakaiGuests: no Makai IDs available, aborting");
            return;
        }

        DiagLog($"SummonRandomMakaiGuests: attempting {count} from pool of {allIds.Count} IDs " +
                 $"(special={_makaiSpecialGuestIds.Count}, normal={_makaiNormalGuestIds.Count})");

        for (int i = 0; i < count; i++)
        {
            var id = allIds[UnityEngine.Random.Range(0, allIds.Count)];
            DiagLog($"  Attempt {i}: picked id={id}, isSpecial={_makaiSpecialGuestIds.Contains(id)}");

            if (_makaiSpecialGuestIds.Contains(id))
            {
                if (!PlayerManager.SpecialGuestAvailable(id))
                {
                    DiagLog($"  id={id}: SpecialGuestAvailable=false, skipping");
                    continue;
                }
                var specialGuest = DataBaseCharacter.RefSGuest(id);
                if (specialGuest == null)
                {
                    DiagLog($"  id={id}: RefSGuest returned null, skipping");
                    continue;
                }

                var ctrl = new SpecialGuestsController(
                    specialGuest,
                    new Il2CppSystem.Nullable<Vector3>(_portalPosition),
                    null,
                    GuestGroupController.LeaveType.Move,
                    SpecialGuestsController.GuestSpawnType.Normal);

                GuestsManager.Instance.PostInitializeGuestGroup(ctrl, -1, false, true);
                DiagLog($"  Spawned special guest #{id} successfully");
            }
            else
            {
                if (!PlayerManager.NormalGuestAvailable(id))
                {
                    DiagLog($"  id={id}: NormalGuestAvailable=false, skipping");
                    continue;
                }
                var normalGuest = DataBaseCharacter.RefNGuest(id);
                if (normalGuest == null)
                {
                    DiagLog($"  id={id}: RefNGuest returned null, skipping");
                    continue;
                }

                var il2cppGuests = new Il2CppSystem.Collections.Generic.List<NormalGuest>();
                il2cppGuests.Add(normalGuest);

                var postprocessCallback = GuestsManager.Instance.getPostprocessCharacterCallback.Invoke();
                var guestsEnumerable = new Il2CppSystem.Collections.Generic.IEnumerable<NormalGuest>(il2cppGuests.Pointer);

                var ctrl = new NormalGuestsController(
                    guestsEnumerable,
                    new Il2CppSystem.Nullable<Vector3>(_portalPosition),
                    postprocessCallback,
                    GuestGroupController.LeaveType.Move);

                GuestsManager.Instance.PostInitializeGuestGroup(ctrl, -1, false, true);
                DiagLog($"  Spawned normal guest #{id} successfully");
            }
        }
    }

    /// <summary>
    /// 客机重放红卡效果（仅视觉）
    /// </summary>
    public static void ReplayRedCard(bool portalAlreadyOpen)
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            if (!portalAlreadyOpen)
            {
                _portalActive = true;
                _portalPosition = DeterminePortalPosition();
                CreatePortalVisual(_portalPosition);
            }
        });
    }

    // ================================================================================
    // 清理
    // ================================================================================

    public static void CleanupPortal()
    {
        CommandScheduler.CancelInterval(_portalTimerId);
        _portalActive = false;
        DestroyPortalVisual();
        DiagLog("Shinki: Makai portal closed");
    }

    // ================================================================================
    // 传送门视觉（可替换接口）
    // ================================================================================

    /// <summary>
    /// 自定义传送门视觉创建委托。
    /// 接收传送门场景世界坐标，返回创建的 GameObject（用于后续销毁）。
    /// 返回 null 表示不创建视觉。
    /// </summary>
    public static Func<Vector3, GameObject> CustomPortalVisualFactory { get; set; }

    // ScreenSpaceOverlay sortingOrder — 负数确保在游戏 UI 之下（UI 通常 ≥ 0）
    private const int PortalSortingOrder = -100;

    private static void CreatePortalVisual(Vector3 position)
    {
        DestroyPortalVisual();

        if (CustomPortalVisualFactory != null)
        {
            DiagLog("CreatePortalVisual: using custom visual factory");
            _portalVisual = CustomPortalVisualFactory(position);
            return;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            DiagLog("CreatePortalVisual: Camera.main is null, aborting");
            return;
        }

        var screenPos = cam.WorldToScreenPoint(position);
        DiagLog($"CreatePortalVisual: world={position} → screen=({screenPos.x:F0}, {screenPos.y:F0})");

        var canvasGO = new GameObject("Shinki_MakaiPortal");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = PortalSortingOrder;

        var imgGO = new GameObject("PortalImage");
        imgGO.transform.SetParent(canvasGO.transform, false);

        var img = imgGO.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(1f, 0f, 1f, 0.85f);

        var rt = img.rectTransform;
        var portalSize = new Vector2(120f, 180f);
        rt.sizeDelta = portalSize;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(screenPos.x, screenPos.y);

        _portalVisual = canvasGO;
        DiagLog($"CreatePortalVisual: DONE — screen=({screenPos.x:F0},{screenPos.y:F0}), size={portalSize}, order={PortalSortingOrder}");
    }

    private static void DestroyPortalVisual()
    {
        if (_portalVisual != null)
        {
            UnityEngine.Object.Destroy(_portalVisual);
            _portalVisual = null;
            DiagLog("DestroyPortalVisual: portal destroyed");
        }
    }

    /// <summary>
    /// 创建序列帧动画传送门工厂。传入 rex:// 精灵路径数组和帧率，返回可赋给
    /// <see cref="CustomPortalVisualFactory"/> 的委托。
    /// </summary>
    public static Func<Vector3, GameObject> CreateAnimatedPortalFactory(
        string[] spriteUris, float framesPerSecond = 12f)
    {
        // 预加载所有帧精灵
        var frames = new List<Sprite>();
        foreach (var uri in spriteUris)
        {
            if (TryGetSprite(uri, out var s) && s != null)
                frames.Add(s);
            else
                DiagLog($"CreateAnimatedPortalFactory: failed to load '{uri}'");
        }

        if (frames.Count == 0)
        {
            DiagLog("CreateAnimatedPortalFactory: no frames loaded, returning null factory");
            return _ => null;
        }

        DiagLog($"CreateAnimatedPortalFactory: loaded {frames.Count} frames at {framesPerSecond} fps");
        var frameArray = frames.ToArray();

        // 注册 MonoBehaviour 到 IL2CPP
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<PortalSpriteAnimator>())
            ClassInjector.RegisterTypeInIl2Cpp<PortalSpriteAnimator>();

        return position =>
        {
            var cam = Camera.main;
            if (cam == null) return null;

            var screenPos = cam.WorldToScreenPoint(position);

            var canvasGO = new GameObject("Shinki_Portal_Animated");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = PortalSortingOrder;

            var imgGO = new GameObject("PortalImage");
            imgGO.transform.SetParent(canvasGO.transform, false);

            var img = imgGO.AddComponent<UnityEngine.UI.Image>();
            img.sprite = frameArray[0];
            img.preserveAspect = true;

            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(screenPos.x, screenPos.y);

            var animator = canvasGO.AddComponent<PortalSpriteAnimator>();
            animator.Frames = frameArray;
            animator.FramesPerSecond = framesPerSecond;

            DiagLog($"CreateAnimatedPortalFactory: ScreenSpaceOverlay at screen=({screenPos.x:F0},{screenPos.y:F0})");
            return canvasGO;
        };
    }

    private static bool TryGetSprite(string uri, out Sprite sprite)
        => ResourceExManager.TryGetSprite(uri, out sprite);

    // ================================================================================
    // 传送门位置
    // ================================================================================

    private static Vector3 DeterminePortalPosition()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            DiagLog("DeterminePortalPosition: Camera.main is null, returning zero");
            return Vector3.zero;
        }

        var screenX = Screen.width * PortalScreenXRatio;
        var screenY = Screen.height * PortalScreenYRatio;
        var worldPos = cam.ScreenToWorldPoint(new Vector3(screenX, screenY, cam.nearClipPlane));
        var portalPos = new Vector3(worldPos.x + PortalWorldOffsetX, worldPos.y, 0);
        DiagLog($"DeterminePortalPosition: screen=({screenX:F0},{screenY:F0}), world={portalPos}, offsetX={PortalWorldOffsetX}");
        return portalPos;
    }

    // ================================================================================
    // 角色 ID 解析
    // ================================================================================

    public static void ResolveCharacterIds()
    {
        _makaiSpecialGuestIds.Clear();
        _makaiNormalGuestIds.Clear();

        var specialGuests = DataBaseCharacter.SpecialGuest;
        if (specialGuests != null)
        {
            foreach (var kvp in specialGuests)
            {
                var id = kvp.Key;
                if (id >= 5000 && id <= 5015)
                {
                    _makaiSpecialGuestIds.Add(id);
                }
            }
        }

        var normalGuests = DataBaseCharacter.NormalGuest;
        if (normalGuests != null)
        {
            foreach (var kvp in normalGuests)
            {
                var id = kvp.Key;
                if (id >= 5000 && id <= 5015)
                {
                    _makaiNormalGuestIds.Add(id);
                }
            }
        }

        DiagLog($"ResolveCharacterIds: {_makaiSpecialGuestIds.Count} special, " +
                 $"{_makaiNormalGuestIds.Count} normal — " +
                 $"special=[{string.Join(",", _makaiSpecialGuestIds)}], " +
                 $"normal=[{string.Join(",", _makaiNormalGuestIds)}]");
    }

    // ================================================================================
    // 注册相关
    // ================================================================================

    private static string _shinkiLabel = "";

    public static void SetShinkiLabel(string label) => _shinkiLabel = label;
    public static void SetShinkiCharacterId(int id) => _shinkiCharacterId = id;
    public static void SetShinkiResourceExId(int id) => _shinkiResourceExId = id;
}

/// <summary>
/// 传送门序列帧动画驱动。挂在带 Image 的 Canvas GameObject 上（ScreenSpaceOverlay）。
/// </summary>
public class PortalSpriteAnimator : MonoBehaviour
{
    public Sprite[] Frames;
    public float FramesPerSecond = 12f;

    private float _timer;
    private int _index;
    private UnityEngine.UI.Image _image;

    public PortalSpriteAnimator(IntPtr ptr) : base(ptr) { }

    void Update()
    {
        if (Frames == null || Frames.Length == 0) return;
        _image ??= GetComponent<UnityEngine.UI.Image>();
        if (_image == null) return;

        _timer += Time.deltaTime;
        var interval = 1f / FramesPerSecond;
        if (_timer >= interval)
        {
            _timer -= interval;
            _index = (_index + 1) % Frames.Length;
            _image.sprite = Frames[_index];
        }
    }
}
