// 这里是描述神绮符卡的具体实施情况的代码
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using UnityEngine.UI;

using Common.CharacterUtility;
using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;
using NightScene.Tiles;
using SgrYuki;

using MetaMystia;
using MetaMystia.Network;
using MetaMystia.Patch;

namespace MetaMystia.ResourceEx.SpellCollection;

[AutoLog]
public partial class Spell_Shinki : SpellBase
{
    // ================================================================================
    // 日志辅助
    // ================================================================================
    internal static void DiagLog(string message)
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
    private static readonly List<GuestGroupController> _spawnedGuestControllers = [];
    internal static Vector3 _portalPosition;
    private static GameObject _portalVisual;
    internal static bool _isBlackCardActive;

    // 位置参数 --------------------------------------------------------------------------
    private const float PortalScreenXRatio = 0.50f;
    private const float PortalScreenYRatio = 0.25f;
    private const float PortalWorldOffsetX = 1.5f; // 世界坐标 X 轴右移
    // -----------------------------------------------------------------------------------

    private const float BlackCardPortalDisplayDuration = 1.5f;

    // Buff 参数 ------------------------------------------------------------------------
    private static GameObject _buffPanel; // 自建 Buff UI
    private const float BuffPanelScreenXRatio = 0.85f;
    private const float BuffPanelScreenYRatio = 0.92f;
    private const int BuffPanelSortingOrder = 500;
    // -----------------------------------------------------------------------------------

    // ================================================================================
    // SpellBase 重写
    // ================================================================================

    public override string OnGettingSpellOwnerIdentifier()
    {
        return _shinkiLabel;
    }

    public override bool HasPositiveSpell => true;

    public override bool ShouldCallSpellDeclarationAuto(bool isPositiveSpell) => true;

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

        // 0. 暂停传送门定时召唤，防止黑卡驱逐期间
        //    OnPortalTick 继续生成新客人与当前驱逐流程冲突。
        var portalWasActive = _portalActive;
        if (_portalActive)
        {
            DiagLog("Black Card: pausing portal timer to prevent conflict");
            CommandScheduler.CancelInterval(_portalTimerId);
            _portalActive = false;
        }

        // 1. 收集所有活跃客人，分离神绮和其他客人
        var allGuests = GuestsMap.GetAllGuestsSnapshot();
        DiagLog($"Black Card: GuestsMap snapshot count={allGuests.Count}");

        (int rid, GuestFSM fsm)? shinkiGuest = null;
        var affectedGuests = new List<(int runtimeId, GuestFSM fsm)>();

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

            if (isShinki)
            {
                if (shinkiGuest == null)
                    shinkiGuest = (rid, fsm);
                else
                    affectedGuests.Add((rid, fsm)); // 多余神绮实例按普通客人处理
            }
            else
                affectedGuests.Add((rid, fsm));
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
        foreach (var (rid, fsm) in affectedGuests)
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

        // 逐帧等待神绮到达（超时兜底 10s，防止移动回调因支付流程冲突永不触发）
        var shinkiStandTimer = 0f;
        const float shinkiStandTimeout = 10f;
        while (!shinkiArrived && shinkiStandTimer < shinkiStandTimeout) { shinkiStandTimer += Time.deltaTime; yield return null; }
        if (!shinkiArrived) DiagLog($"  Shinki standby: timed out after {shinkiStandTimeout}s, proceeding anyway");

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

        // === 7. 所有客人走到传送门 → 淡出 ===
        // 不依赖 LeaveFromDesk + FlyToSpawn Hook，而是直接控制客人移动到传送门，
        // 到达后播放 FlyToSpawn(true) 原地淡出。
        // Phase 1 已完成资金清零 + 订单/面板清理，此处仅处理视觉离场。
        var guestArrived = new bool[affectedGuests.Count]; // 可变标志，供回调写入、轮询读取

        for (int i = 0; i < affectedGuests.Count; i++)
        {
            try
            {
                var (rid, fsm) = affectedGuests[i];
                if (fsm?.Controller == null)
                {
                    DiagLog($"  Guest rid={rid}: controller is null, removing from map");
                    try { GuestsMap.Remove(rid); } catch { }
                    guestArrived[i] = true;
                    continue;
                }

                if (fsm.CurrentState == GuestFSM.State.Leaving || fsm.CurrentState == GuestFSM.State.Left)
                {
                    DiagLog($"  Guest rid={rid}: already Leaving/Left, skip");
                    guestArrived[i] = true;
                    continue;
                }

                // 所有活跃客人（含排队中）都走向传送门
                DiagLog($"  Guest rid={rid}: walking to portal at {_portalPosition}");
                fsm.Controller.MoveToTargetPosition(
                    -1,
                    new Il2CppSystem.Nullable<Vector3>(_portalPosition),
                    Vector3Int.zero,
                    false,
                    null);
            }
            catch (Exception ex)
            {
                var failedRid = affectedGuests[i].runtimeId;
                DiagLog($"  Guest rid={failedRid}: Step7 init threw ({ex.GetType().Name}: {ex.Message})");
                try { GuestsMap.Remove(failedRid); } catch { }
                guestArrived[i] = true; // 失败也标记完成，跳过等待
            }
        }

        // 等待一段时间让客人走动画播放，然后淡出（不依赖 MoveToTargetPosition 的 onArrive 回调）
        const float guestWalkDuration = 7f;
        var globalWaitTimer = 0f;
        while (globalWaitTimer < guestWalkDuration)
        {
            globalWaitTimer += Time.deltaTime;
            yield return null;
        }
        // 标记所有客人已完成
        for (int k = 0; k < guestArrived.Length; k++)
        {
            if (!guestArrived[k])
            {
                guestArrived[k] = true;
                DiagLog($"  Guest rid={affectedGuests[k].runtimeId}: walk timer done ({guestWalkDuration}s)");
            }
        }

        // 所有客人到达后，淡出并移除
        for (int i = 0; i < affectedGuests.Count; i++)
        {
            try
            {
                var (rid, fsm) = affectedGuests[i];
                if (!guestArrived[i]) DiagLog($"  Guest rid={rid}: arrival timed out");

                if (fsm?.Controller != null && fsm.Controller.DeskCode != -1)
                {
                    DiagLog($"  Guest rid={rid}: LeaveFromDesk(Fading) at portal");
                    try
                    {
                        GuestsManagerPatch.SkipLeaveFromDeskPatch.Grant();
                        GuestsManagerPatch.SkipLeaveFromDeskBroadcastPatch.Grant();
                        GuestsManager.Instance.LeaveFromDesk(
                            fsm.Controller, GuestGroupController.LeaveType.Fading, null, false);
                    }
                    catch (Exception flyEx) { DiagLog($"  Guest rid={rid}: LeaveFromDesk threw: {flyEx.Message}"); }
                }
                else if (fsm?.Controller != null)
                {
                    DiagLog($"  Guest rid={rid}: FlyToSpawn(true) (queue guest) at portal");
                    try { fsm.Controller.FlyToSpawn(true); }
                    catch (Exception flyEx) { DiagLog($"  Guest rid={rid}: FlyToSpawn threw: {flyEx.Message}"); }
                }

                try { GuestsMap.Remove(rid); } catch { }
            }
            catch (Exception ex)
            {
                var failedRid = affectedGuests[i].runtimeId;
                DiagLog($"  Guest rid={failedRid}: Step7 cleanup threw ({ex.GetType().Name}: {ex.Message})");
                try { GuestsMap.Remove(failedRid); } catch { }
            }
        }

        DiagLog("Black Card: all affected guests banished through portal");

        // === 9. 等待 1s 后，神绮走向传送门并离场 ===
        yield return new WaitForSeconds(1f);
        if (shinkiGuest != null)
        {
            var shinkiCtrl = shinkiGuest.Value.fsm.Controller;
            DiagLog($"  Shinki walking to portal, rid={shinkiGuest.Value.rid}");
            if (shinkiCtrl != null)
            {
                var shinkiArrivedAtPortal = false;
                System.Action<GuestGroupController> onShinkiPortalArrive = _ =>
                {
                    shinkiArrivedAtPortal = true;
                    DiagLog("  Shinki: arrived at portal");
                };
                shinkiCtrl.MoveToTargetPosition(
                    -1, new Il2CppSystem.Nullable<Vector3>(_portalPosition), Vector3Int.zero, false,
                    Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GuestGroupController>>(onShinkiPortalArrive));

                var shinkiPortalTimer = 0f;
                const float shinkiPortalTimeout = 10f;
                while (!shinkiArrivedAtPortal && shinkiPortalTimer < shinkiPortalTimeout)
                {
                    shinkiPortalTimer += Time.deltaTime;
                    yield return null;
                }
                if (!shinkiArrivedAtPortal)
                    DiagLog($"  Shinki portal: timed out after {shinkiPortalTimeout}s");

                // 神绮到传送门后，通过 LeaveFromDesk 释放桌位并淡出
                DiagLog("  Shinki: LeaveFromDesk(Fading) at portal");
                try
                {
                    GuestsManagerPatch.SkipLeaveFromDeskPatch.Grant();
                    GuestsManagerPatch.SkipLeaveFromDeskBroadcastPatch.Grant();
                    GuestsManager.Instance.LeaveFromDesk(
                        shinkiCtrl, GuestGroupController.LeaveType.Fading, null, false);
                }
                catch (Exception ex) { DiagLog($"  Shinki LeaveFromDesk failed: {ex.Message}"); }
            }
            GuestsMap.Remove(shinkiGuest.Value.rid);
        }

        // === 10. 展示传送门后销毁 ===
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
    /// 阶段一清理：清零资金并清理订单/面板，保留 Controller 存活。
    /// 后续由 LeaveFromDesk 正常流程处理离桌和资源释放。
    /// 关键：必须在支付流程读取 GetFund 之前将其清零，否则正在买单的客人会按原价结算。
    /// </summary>
    private static void PartialCleanupForBlackCard(GuestGroupController ctrl)
    {
        if (ctrl == null) return;

        // 在支付流程读取前清零资金，防止正在买单的客人按原价结算
        ctrl.GetFund = 0;

        if (ctrl.DeskCode != -1)
        {
            GuestService.CleanGuestOrderRegistration(ctrl);
            GuestsManager.Instance.RemoveFromPatientCountdown(ctrl);
            GuestFSM.TryCloseServePanel(ctrl.DeskCode);
        }
        else if (ctrl.queued)
        {
            // 仅移除耐心倒计时，不调用 RemoveFromQueue()
            // RemoveFromQueue() 会摧毁 GuestGroupController
            GuestsManager.Instance.RemoveFromPatientCountdown(ctrl);
        }
    }

    /// <summary>
    /// 客机重放黑卡效果（客人走到传送门 → 淡出，与主机一致）
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

            // 等待神绮到达 → 创建传送门 → 客人走到传送门淡出
            CommandScheduler.Enqueue(() => shinkiArrived, () =>
            {
                DiagLog("ReplayBlackCard: Shinki arrived, creating portal");
                CreatePortalVisual(portalPos);

                // 所有活跃客人（含排队中）走到传送门
                bool hasWalkingGuests = false;
                for (int i = 0; i < affectedRuntimeIds.Length; i++)
                {
                    try
                    {
                        var rid = affectedRuntimeIds[i];
                        var fsm = GuestsMap.GetGuestFsm(rid);
                        if (fsm?.Controller == null)
                        {
                            try { GuestsMap.Remove(rid); } catch { }
                            continue;
                        }

                        if (fsm.CurrentState == GuestFSM.State.Leaving)
                            continue;

                        DiagLog($"ReplayBlackCard: guest rid={rid} walking to portal");
                        fsm.Controller.MoveToTargetPosition(
                            -1, new Il2CppSystem.Nullable<Vector3>(portalPos), Vector3Int.zero, false, null);
                        hasWalkingGuests = true;
                    }
                    catch (Exception ex)
                    {
                        DiagLog($"ReplayBlackCard: guest walk threw for rid={affectedRuntimeIds[i]}: {ex.Message}");
                        try { GuestsMap.Remove(affectedRuntimeIds[i]); } catch { }
                    }
                }

                // 固定等待客人走路动画（7s），然后离场
                if (hasWalkingGuests)
                {
                    var guestWalkStart = CommandScheduler.Now;
                    CommandScheduler.Enqueue(() => CommandScheduler.Now - guestWalkStart > 7f, () =>
                    {
                        for (int i = 0; i < affectedRuntimeIds.Length; i++)
                        {
                            var rid = affectedRuntimeIds[i];
                            var fsm = GuestsMap.GetGuestFsm(rid);
                            if (fsm?.Controller != null)
                            {
                                try
                                {
                                    if (fsm.Controller.DeskCode != -1)
                                    {
                                        GuestsManagerPatch.SkipLeaveFromDeskPatch.Grant();
                                        GuestsManagerPatch.SkipLeaveFromDeskBroadcastPatch.Grant();
                                        GuestsManager.Instance.LeaveFromDesk(
                                            fsm.Controller, GuestGroupController.LeaveType.Fading, null, false);
                                    }
                                    else
                                    {
                                        fsm.Controller.FlyToSpawn(true);
                                    }
                                }
                                catch { }
                            }
                            try { GuestsMap.Remove(rid); } catch { }
                        }
                        DiagLog("ReplayBlackCard: all guests faded at portal");
                    }, "Shinki:ReplayGuestWalk", timeoutSeconds: 12f);
                }

                // 1s 延迟后神绮走向传送门
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

                    // 神绮到达传送门 → 离场 → 销毁传送门
                    CommandScheduler.Enqueue(() => shinkiArrivedAtPortal, () =>
                    {
                        DiagLog("ReplayBlackCard: Shinki removing at portal");
                        if (shinkiRid > 0)
                        {
                            var shinkiFsm = GuestsMap.GetGuestFsm(shinkiRid);
                            if (shinkiFsm?.Controller != null)
                            {
                                // 神绮到传送门后，通过 LeaveFromDesk 释放桌位并淡出
                                DiagLog($"ReplayBlackCard: Shinki LeaveFromDesk at portal");
                                try
                                {
                                    GuestsManagerPatch.SkipLeaveFromDeskPatch.Grant();
                                    GuestsManagerPatch.SkipLeaveFromDeskBroadcastPatch.Grant();
                                    GuestsManager.Instance.LeaveFromDesk(
                                        shinkiFsm.Controller, GuestGroupController.LeaveType.Fading, null, false);
                                }
                                catch (Exception ex) { DiagLog($"ReplayBlackCard: Shinki LeaveFromDesk failed: {ex.Message}"); }
                            }
                            GuestsMap.Remove(shinkiRid);
                        }

                        CommandScheduler.Enqueue(() => true, () =>
                        {
                            DiagLog("ReplayBlackCard: destroying portal");
                            DestroyPortalVisual();
                        }, "Shinki:ReplayDestroyPortal", timeoutSeconds: BlackCardPortalDisplayDuration + 1f);

                    }, "Shinki:ReplayShinkiArrivePortal", timeoutSeconds: 5f);

                }, "Shinki:ReplayDelay", timeoutSeconds: 5f);

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

        // 诊断：ctx 的公开方法
        try
        {
            DiagLog("=== SpellExecutionContext Methods ===");
            var ctxMethods = ctx.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
            foreach (var m in ctxMethods)
            {
                if (m.IsSpecialName) continue; // 跳过 get_/set_
                var parms = string.Join(",", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                DiagLog($"  ctx.{m.Name}({parms}) → {m.ReturnType.Name} [virtual={m.IsVirtual}]");
            }
        }
        catch (Exception ex) { DiagLog($"ctx methods error: {ex.Message}"); }

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
        RegisterPortalBuff(); // 注册 Buff 显示

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

        // 获取场上已有稀客 ID（验重）
        var existingSpecialIds = GetExistingMakaiSpecialGuestIds();

        // 构建可用稀客列表（排除场上已存在的）
        var availableSpecial = _makaiSpecialGuestIds
            .Where(id => !existingSpecialIds.Contains(id))
            .ToList();

        // 构建可用普客列表
        var availableNormal = _makaiNormalGuestIds.ToList();

        DiagLog($"SummonRandomMakaiGuests: attempting {count} guests — " +
                 $"availableSpecial={availableSpecial.Count}/{_makaiSpecialGuestIds.Count}, " +
                 $"availableNormal={availableNormal.Count}/{_makaiNormalGuestIds.Count})");

        if (availableSpecial.Count == 0 && availableNormal.Count == 0)
        {
            DiagLog("SummonRandomMakaiGuests: no available guests at all, aborting");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            // 决定召唤稀客还是普客：稀客概率 = 1/3（普客的 1/2）
            bool summonSpecial = availableSpecial.Count > 0
                && (availableNormal.Count == 0 || UnityEngine.Random.value < 1f / 3f);

            if (summonSpecial)
            {
                var id = availableSpecial[UnityEngine.Random.Range(0, availableSpecial.Count)];
                DiagLog($"  Attempt {i}: picked SPECIAL id={id}");

                if (!PlayerManager.SpecialGuestAvailable(id))
                {
                    DiagLog($"  id={id}: SpecialGuestAvailable=false, removing from pool, skipping");
                    availableSpecial.Remove(id);
                    i--; // 重新尝试
                    continue;
                }
                var specialGuest = DataBaseCharacter.RefSGuest(id);
                if (specialGuest == null)
                {
                    DiagLog($"  id={id}: RefSGuest returned null, removing from pool, skipping");
                    availableSpecial.Remove(id);
                    i--;
                    continue;
                }

                var ctrl = new SpecialGuestsController(
                    specialGuest,
                    new Il2CppSystem.Nullable<Vector3>(_portalPosition),
                    null,
                    GuestGroupController.LeaveType.Move,
                    SpecialGuestsController.GuestSpawnType.Normal);

                GuestsManager.Instance.PostInitializeGuestGroup(ctrl, -1, false, true);
                _spawnedGuestControllers.Add(ctrl);

                // 从可用池移除，防止同批次重复召唤
                availableSpecial.Remove(id);
                DiagLog($"  Spawned special guest #{id} successfully, total spawned={_spawnedGuestControllers.Count}");
            }
            else
            {
                if (availableNormal.Count == 0)
                {
                    DiagLog($"  Attempt {i}: no normal guests available, skipping");
                    continue;
                }
                var id = availableNormal[UnityEngine.Random.Range(0, availableNormal.Count)];
                DiagLog($"  Attempt {i}: picked NORMAL id={id}");

                if (!PlayerManager.NormalGuestAvailable(id))
                {
                    DiagLog($"  id={id}: NormalGuestAvailable=false, removing from pool, skipping");
                    availableNormal.Remove(id);
                    i--; // 重新尝试
                    continue;
                }
                var normalGuest = DataBaseCharacter.RefNGuest(id);
                if (normalGuest == null)
                {
                    DiagLog($"  id={id}: RefNGuest returned null, removing from pool, skipping");
                    availableNormal.Remove(id);
                    i--;
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
                _spawnedGuestControllers.Add(ctrl);
                DiagLog($"  Spawned normal guest #{id} successfully, total spawned={_spawnedGuestControllers.Count}");
            }
        }
    }

    /// <summary>
    /// 获取场上已有的魔界稀客 ID 集合（用于验重）
    /// </summary>
    private static HashSet<int> GetExistingMakaiSpecialGuestIds()
    {
        var existing = new HashSet<int>();
        var allGuests = GuestsMap.GetAllGuestsSnapshot();
        foreach (var (rid, fsm) in allGuests)
        {
            if (fsm?.Ids == null || fsm.Controller == null) continue;
            // 跳过已离开/死亡/无状态的客人
            var state = fsm.CurrentState;
            if (state == GuestFSM.State.Left || state == GuestFSM.State.Dead || state == GuestFSM.State.None)
                continue;

            foreach (var id in fsm.Ids)
            {
                if (_makaiSpecialGuestIds.Contains(id))
                    existing.Add(id);
            }
        }
        DiagLog($"GetExistingMakaiSpecialGuestIds: found {existing.Count} existing specials: [{string.Join(",", existing)}]");
        return existing;
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
        _spawnedGuestControllers.Clear();
        DestroyPortalVisual();
        RemovePortalBuff(); // 移除 Buff 显示
        DiagLog("Shinki: Makai portal closed");
    }

    // ================================================================================
    // Buff 注册 / 移除（红卡传送门）—— 自建 Canvas UI
    // ================================================================================
    // 游戏 RegisterTimedBuff 签名: (Int32, BuffType, Action&, Action, Func`3, Func`2)
    // BuffType 是游戏内部枚举，titleOverride Func 类型不可靠，无法通过 mod 构造。
    // 因此改用自建 ScreenSpaceOverlay Canvas 实现 Buff 显示。
    // ================================================================================

    private static void RegisterPortalBuff()
    {
        try
        {
            if (_buffPanel != null) return; // 已存在

            var remaining = EventManager.Instance == null
                ? 0
                : EventManager.Instance.TotalCountDown + EventManager.Instance.extraCountDown;
            DiagLog($"RegisterPortalBuff: creating buff UI, remaining={remaining}s");

            _buffPanel = new GameObject("Shinki_BuffPanel");
            UnityEngine.Object.DontDestroyOnLoad(_buffPanel);

            var canvas = _buffPanel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = BuffPanelSortingOrder;

            var scaler = _buffPanel.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            // === 背景面板 ===
            var bgGO = new GameObject("Bg");
            bgGO.transform.SetParent(_buffPanel.transform, false);
            var bgImg = bgGO.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.75f);
            var bgRt = bgImg.rectTransform;
            bgRt.sizeDelta = new Vector2(220f, 44f);
            bgRt.anchorMin = Vector2.one;
            bgRt.anchorMax = Vector2.one;
            bgRt.pivot = Vector2.one;
            bgRt.anchoredPosition = new Vector2(-10f, -10f);

            // === 图标（占位：紫色方块） ===
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(bgGO.transform, false);
            var iconImg = iconGO.AddComponent<UnityEngine.UI.Image>();
            iconImg.color = new Color(0.7f, 0.2f, 1f, 1f); // 紫色
            var iconRt = iconImg.rectTransform;
            iconRt.sizeDelta = new Vector2(30f, 30f);
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(8f, 0f);

            // === 文字描述 ===
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(bgGO.transform, false);
            var txt = textGO.AddComponent<UnityEngine.UI.Text>();
            txt.text = "魔神降临 · 魔界传送门";
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 16;
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleLeft;
            var txtRt = txt.rectTransform;
            txtRt.sizeDelta = new Vector2(155f, 44f);
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.pivot = new Vector2(0f, 0.5f);
            txtRt.anchoredPosition = new Vector2(45f, 0f);

            DiagLog("RegisterPortalBuff: buff UI created");
        }
        catch (Exception ex)
        {
            DiagLog($"RegisterPortalBuff: error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RemovePortalBuff()
    {
        try
        {
            if (_buffPanel != null)
            {
                UnityEngine.Object.Destroy(_buffPanel);
                _buffPanel = null;
                DiagLog("RemovePortalBuff: buff UI destroyed");
            }
        }
        catch (Exception ex)
        {
            DiagLog($"RemovePortalBuff: error: {ex.Message}");
        }
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

        // 稀客池：爱丽丝(1002/本体) + 露易兹(5005/DLC5) + 雪(11000/mod) + 舞(11001/mod)
        _makaiSpecialGuestIds.Add(1002);  // 爱丽丝 (Alice)
        _makaiSpecialGuestIds.Add(5005);  // 露易兹 (Luize)
        _makaiSpecialGuestIds.Add(11000); // 雪 (Yuki)
        _makaiSpecialGuestIds.Add(11001); // 舞 (Mai)

        // 普客池：纸牌兵(5000/DLC5) + 小丑(5001/DLC5)
        _makaiNormalGuestIds.Add(5000); // 纸牌兵
        _makaiNormalGuestIds.Add(5001); // 小丑

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

    // ================================================================================
    // 黑卡 FlyToSpawn Hook：让客人走到传送门再淡出
    // ================================================================================

    /// <summary>
    /// 开启黑卡 FlyToSpawn 拦截
    /// </summary>
    private static void EnableBlackCardFlyToSpawnOverride()
    {
        _isBlackCardActive = true;
        DiagLog("BlackCard: FlyToSpawn override ENABLED");
    }

    /// <summary>
    /// 关闭黑卡 FlyToSpawn 拦截
    /// </summary>
    private static void DisableBlackCardFlyToSpawnOverride()
    {
        _isBlackCardActive = false;
        DiagLog("BlackCard: FlyToSpawn override DISABLED");
    }
}


/// <summary>
/// GuestIconManager.SwitchState() 安全网：
/// 传送门生成的客人通过 PostInitializeGuestGroup → TrySendToSeat 入座后，游戏的延迟回调
/// （TrySendToSeat.b__0/b__1）会调用 GuestIconManager.SwitchState 更新图标状态。
/// 若客人在回调触发前已被清理（FlyToSpawn / CleanDeskState），GuestGroupController 引用
/// 变为 null，原方法未做 null-check 导致每帧 NRE 刷屏。
/// 此处用 Prefix 检查 controller 是否为 null，为 null 则跳过原方法。
///
/// 注意：SwitchState 签名为 (GuestGroupController controller, GuestState state)，
/// Prefix 必须同时声明两个参数才能通过 HarmonyX 参数名匹配。
/// </summary>
[HarmonyPatch(typeof(GuestIconManager), "SwitchState")]
public static class ShinkiGuestIconManagerPatch
{
    [HarmonyPrefix]
    public static bool SwitchState_Prefix(GuestGroupController controller, GuestState state)
    {
        return controller != null;
    }
}

/// <summary>
/// NightSceneDebugConsole.Guests() 安全网：
/// NightSceneDebugConsole 是调试工具，在 FlyToSpawn(true) 销毁客人 GameObject 后，
/// 其内部遍历残留 null 引用的 AStarInputGeneratorComponent，导致每帧 Guests() 调用时触发 NRE。
/// 由于 Guests() 为 Il2Cpp 原生方法（C# 仅壳），无法 Transpiler 修改其内部遍历逻辑。
/// 使用 HarmonyFinalizer 在异常时静默吞掉 NRE，保留正常情况下的调试功能，同时消除刷屏。
/// </summary>
[HarmonyPatch(typeof(PrototypingManagers.NightSceneDebugConsole), "Guests")]
public static class ShinkiDebugConsolePatch
{
    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception)
    {
        // 只吞掉 NullReferenceException，其他异常照常抛出
        if (__exception is NullReferenceException)
            return null;
        return __exception;
    }
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
