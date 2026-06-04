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
    private const float GuestWalkDuration = 6f;



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

    //需要(SpellExecutionContext ctx) 符卡执行上下文
    //黑卡主协程：驱逐所有客人到传送门→淡出→神绮离场
    //返回(IEnumerator) Unity协程
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

        // 3. 计算 runtimeId，Phase 1 清理
        var affectedIds = affectedGuests.Select(g => g.runtimeId).ToArray();
        int shinkiRid = shinkiGuest?.rid ?? -1;
        PhaseCleanupForBlackCard(affectedIds, shinkiRid);

        // === 4. 神绮移动到待机位置（偏左），用回调检测移动完成 ===
        var shinkiArrived = false;
        InitiateShinkiWalk(shinkiRid, shinkiStandPos,
            () => { shinkiArrived = true; DiagLog("  Shinki: arrived at standby pos"); });

        // 逐帧等待神绮到达（超时兜底 10s，防止移动回调因支付流程冲突永不触发）
        if (!shinkiArrived)
        {
            var shinkiStandTimer = 0f;
            const float shinkiStandTimeout = 10f;
            while (!shinkiArrived && shinkiStandTimer < shinkiStandTimeout) { shinkiStandTimer += Time.deltaTime; yield return null; }
            if (!shinkiArrived) DiagLog($"  Shinki standby: timed out after {shinkiStandTimeout}s, proceeding anyway");
        }

        // === 4.5 神绮就位后，切换为举旗立绘 ===
        if (shinkiGuest != null && shinkiGuest.Value.fsm.Controller != null)
            SwitchShinkiToFlagSprite(shinkiGuest.Value.fsm.Controller);

        // === 5. 神绮就位后，开启传送门 ===
        DiagLog("Black Card: creating portal");
        CreatePortalVisual(_portalPosition);

        // === 6. 广播网络同步 ===
        if (MpManager.IsConnected)
        {
            ShinkiBlackCardAction.Send(affectedIds, shinkiRid, _portalPosition);
        }

        // === 7. 所有客人走到传送门 → 淡出 ===
        InitiateGuestsWalkToPortal(affectedIds, _portalPosition);

        // 等待一段时间让客人走动画播放，然后淡出
        yield return new WaitForSeconds(GuestWalkDuration);

        // 所有客人到达后，淡出并移除
        FadeOutAllGuests(affectedIds);

        DiagLog("Black Card: all affected guests banished through portal");

        // === 9. 等待 1s 后，神绮走向传送门并离场 ===
        yield return new WaitForSeconds(1f);
        if (shinkiRid > 0)
        {
            // 恢复原始立绘
            var shinkiFsm = GuestsMap.GetGuestFsm(shinkiRid);
            if (shinkiFsm?.Controller != null)
                SwitchShinkiToOriginalSprite(shinkiFsm.Controller);

            DiagLog($"  Shinki walking to portal, rid={shinkiRid}");
            var shinkiArrivedAtPortal = false;
            InitiateShinkiWalk(shinkiRid, _portalPosition,
                () => { shinkiArrivedAtPortal = true; DiagLog("  Shinki: arrived at portal"); });

            if (!shinkiArrivedAtPortal)
            {
                var shinkiPortalTimer = 0f;
                const float shinkiPortalTimeout = 10f;
                while (!shinkiArrivedAtPortal && shinkiPortalTimer < shinkiPortalTimeout) { shinkiPortalTimer += Time.deltaTime; yield return null; }
                if (!shinkiArrivedAtPortal) DiagLog($"  Shinki portal: timed out after {shinkiPortalTimeout}s");
            }

            FadeOutShinki(shinkiRid);
        }

        // === 10. 展示传送门后销毁 ===
        yield return new WaitForSeconds(BlackCardPortalDisplayDuration);
        DestroyPortalVisual();

        DiagLog("Black Card: all guests banished to Makai!");
    }

    // ================================================================================
    // 旗子立绘切换（神绮黑卡等待时显示举旗形象）
    // ================================================================================

    private const string FlagSpriteUri = "rex://ResourceExample/assets/Character/9004/Sprite/flag.png";

    //不需要，无参数
    //预加载举旗精灵素材，失败不崩溃仅禁用功能
    //不返回
    public static void LoadFlagSprite()
    {
        if (_flagSprite != null) return; // 已加载
        if (TryGetSprite(FlagSpriteUri, out var sprite) && sprite != null)
        {
            _flagSprite = sprite;
            DiagLog($"LoadFlagSprite: flag sprite loaded from '{FlagSpriteUri}'");
        }
        else
        {
            DiagLog($"LoadFlagSprite: failed to load '{FlagSpriteUri}', flag feature disabled");
        }
    }

    //需要(GuestGroupController ctrl) 神绮的客人控制器
    //切换神绮立绘为举旗精灵，黑卡就位后调用
    //不返回
    private static void SwitchShinkiToFlagSprite(GuestGroupController ctrl)
    {
        if (ctrl == null || _flagSprite == null) return;

        try
        {
            var sr = GetMainSpriteRenderer(ctrl);
            if (sr == null) { DiagLog("SwitchShinkiToFlagSprite: SpriteRenderer not found"); return; }

            if (_originalSprite == null)
                _originalSprite = sr.sprite; // 首次备份原始精灵

            sr.sprite = _flagSprite;
            DiagLog("SwitchShinkiToFlagSprite: sprite replaced with flag");
        }
        catch (Exception ex)
        {
            DiagLog($"SwitchShinkiToFlagSprite: error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    //需要(GuestGroupController ctrl) 神绮的客人控制器
    //恢复神绮立绘为原始精灵，走向传送门离场前调用
    //不返回
    private static void SwitchShinkiToOriginalSprite(GuestGroupController ctrl)
    {
        if (ctrl == null || _originalSprite == null) return;

        try
        {
            var sr = GetMainSpriteRenderer(ctrl);
            if (sr == null) { DiagLog("SwitchShinkiToOriginalSprite: SpriteRenderer not found"); return; }

            sr.sprite = _originalSprite;
            DiagLog("SwitchShinkiToOriginalSprite: original sprite restored");
        }
        catch (Exception ex)
        {
            DiagLog($"SwitchShinkiToOriginalSprite: error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    //需要(GuestGroupController ctrl) 客人控制器
    //通过guestInstances[0]→gameObject→GetComponentsInChildren查找主SpriteRenderer
    //返回(SpriteRenderer) 主精灵渲染器，失败返回null
    private static SpriteRenderer GetMainSpriteRenderer(GuestGroupController ctrl)
    {
        if (ctrl == null) return null;

        try
        {
            var instances = ctrl.guestInstances;
            if (instances == null || instances.Length == 0)
            {
                DiagLog("GetMainSpriteRenderer: guestInstances is null or empty");
                return null;
            }

            var unit = instances[0];
            if (unit == null)
            {
                DiagLog("GetMainSpriteRenderer: guestInstances[0] is null");
                return null;
            }

            // 精灵在子 GameObject 上，通过 gameObject.GetComponentsInChildren 查找
            var go = unit.gameObject;
            if (go == null)
            {
                DiagLog("GetMainSpriteRenderer: unit.gameObject is null");
                return null;
            }

            var allRenderers = go.GetComponentsInChildren<SpriteRenderer>();
            if (allRenderers == null || allRenderers.Length == 0)
            {
                DiagLog($"GetMainSpriteRenderer: no SpriteRenderers found under '{go.name}'");
                return null;
            }

            DiagLog($"GetMainSpriteRenderer: found {allRenderers.Length} SpriteRenderer(s) under '{go.name}'");
            for (int i = 0; i < allRenderers.Length; i++)
            {
                var r = allRenderers[i];
                DiagLog($"  [{i}] {r.name} sprite={(r.sprite != null ? r.sprite.name : "null")}");
            }

            // 返回第一个，通常是 Main sprite
            return allRenderers[0];
        }
        catch (Exception ex)
        {
            DiagLog($"GetMainSpriteRenderer: exception: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static Sprite _flagSprite;
    private static Sprite _originalSprite;

    //需要(GuestGroupController ctrl) 客人控制器
    //黑卡阶段一：清零资金+清理订单/面板/倒计时，保留Controller存活供后续离场
    //不返回
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

    // ================================================================================
    // 黑卡共享工具方法（主机/客机共用）
    // ================================================================================

    //需要(int[] runtimeIds) 受影响客人runtimeId数组, (int shinkiRid) 神绮runtimeId
    //Phase1批量清理：对所有受影响客人和神绮执行PartialCleanupForBlackCard
    //不返回
    private static void PhaseCleanupForBlackCard(int[] runtimeIds, int shinkiRid)
    {
        foreach (var rid in runtimeIds)
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            if (fsm?.Controller == null) continue;
            PartialCleanupForBlackCard(fsm.Controller);
        }
        if (shinkiRid > 0)
        {
            var fsm = GuestsMap.GetGuestFsm(shinkiRid);
            if (fsm?.Controller != null) PartialCleanupForBlackCard(fsm.Controller);
        }
    }

    //需要(int shinkiRid) 神绮runtimeId, (Vector3 targetPos) 目标世界坐标, (System.Action onArrived) 到达回调
    //发起神绮走到目标位置的移动指令，通过回调通知到达。shinki无效时立即触发回调
    //返回(bool) true=已发起移动(需等回调), false=无需移动(回调已触发)
    private static bool InitiateShinkiWalk(int shinkiRid, Vector3 targetPos, System.Action onArrived)
    {
        if (shinkiRid <= 0) { onArrived?.Invoke(); return false; }
        var fsm = GuestsMap.GetGuestFsm(shinkiRid);
        if (fsm?.Controller == null) { onArrived?.Invoke(); return false; }
        System.Action<GuestGroupController> cb = _ => { onArrived?.Invoke(); };
        fsm.Controller.MoveToTargetPosition(
            -1, new Il2CppSystem.Nullable<Vector3>(targetPos), Vector3Int.zero, false,
            Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GuestGroupController>>(cb));
        return true;
    }

    //需要(int[] runtimeIds) 客人runtimeId数组, (Vector3 portalPos) 传送门世界坐标
    //发起所有客人走向传送门的移动指令，跳过已离开/错误的客人
    //返回(bool) 是否有客人在移动中
    private static bool InitiateGuestsWalkToPortal(int[] runtimeIds, Vector3 portalPos)
    {
        bool hasWalking = false;
        for (int i = 0; i < runtimeIds.Length; i++)
        {
            try
            {
                var rid = runtimeIds[i];
                var fsm = GuestsMap.GetGuestFsm(rid);
                if (fsm?.Controller == null)
                {
                    try { GuestsMap.Remove(rid); } catch { }
                    continue;
                }
                if (fsm.CurrentState == GuestFSM.State.Leaving || fsm.CurrentState == GuestFSM.State.Left)
                    continue;
                DiagLog($"InitiateGuestsWalkToPortal: guest rid={rid} walking to portal");
                fsm.Controller.MoveToTargetPosition(
                    -1, new Il2CppSystem.Nullable<Vector3>(portalPos), Vector3Int.zero, false, null);
                hasWalking = true;
            }
            catch (Exception ex)
            {
                DiagLog($"InitiateGuestsWalkToPortal: guest walk failed for rid={runtimeIds[i]}: {ex.Message}");
                try { GuestsMap.Remove(runtimeIds[i]); } catch { }
            }
        }
        return hasWalking;
    }

    //需要(int[] runtimeIds) 客人runtimeId数组
    //在传送门处淡出所有客人(LeaveFromDesk或FlyToSpawn)并移除
    //不返回
    private static void FadeOutAllGuests(int[] runtimeIds)
    {
        for (int i = 0; i < runtimeIds.Length; i++)
        {
            var rid = runtimeIds[i];
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
                catch (Exception ex) { DiagLog($"FadeOutAllGuests: guest rid={rid} failed: {ex.Message}"); }
            }
            try { GuestsMap.Remove(rid); } catch { }
        }
        DiagLog("FadeOutAllGuests: all guests faded at portal");
    }

    //需要(int shinkiRid) 神绮runtimeId
    //在传送门处淡出神绮(LeaveFromDesk)并移除
    //不返回
    private static void FadeOutShinki(int shinkiRid)
    {
        if (shinkiRid <= 0) return;
        var fsm = GuestsMap.GetGuestFsm(shinkiRid);
        if (fsm?.Controller != null)
        {
            DiagLog("FadeOutShinki: LeaveFromDesk at portal");
            try
            {
                GuestsManagerPatch.SkipLeaveFromDeskPatch.Grant();
                GuestsManagerPatch.SkipLeaveFromDeskBroadcastPatch.Grant();
                GuestsManager.Instance.LeaveFromDesk(
                    fsm.Controller, GuestGroupController.LeaveType.Fading, null, false);
            }
            catch (Exception ex) { DiagLog($"FadeOutShinki: LeaveFromDesk failed: {ex.Message}"); }
        }
        GuestsMap.Remove(shinkiRid);
    }

    //需要(int[] affectedRuntimeIds) 受影响客人runtimeId数组, (int shinkiRid) 神绮runtimeId, (Vector3 portalPos) 传送门世界坐标
    //客机重放黑卡效果：客人走到传送门→淡出→神绮离场→销毁传送门
    //不返回
    public static void ReplayBlackCard(int[] affectedRuntimeIds, int shinkiRid, Vector3 portalPos)
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            _portalPosition = portalPos;
            var shinkiStandPos = portalPos + ShinkiStandOffset;

            // Phase 1 清理
            PhaseCleanupForBlackCard(affectedRuntimeIds, shinkiRid);

            // === 步骤1: 神绮走到待机位置（偏左），回调检测到达 ===
            var shinkiArrived = false;
            InitiateShinkiWalk(shinkiRid, shinkiStandPos, () => { shinkiArrived = true; });

            // 等待神绮到达 → 创建传送门 → 客人走到传送门淡出
            CommandScheduler.Enqueue(() => shinkiArrived, () =>
            {
                DiagLog("ReplayBlackCard: Shinki arrived, creating portal");
                CreatePortalVisual(portalPos);

                // 所有活跃客人（含排队中）走到传送门
                bool hasWalkingGuests = InitiateGuestsWalkToPortal(affectedRuntimeIds, portalPos);

                // 固定等待客人走路动画，然后离场
                if (hasWalkingGuests)
                {
                    var guestWalkStart = CommandScheduler.Now;
                    CommandScheduler.Enqueue(() => CommandScheduler.Now - guestWalkStart > GuestWalkDuration, () =>
                    {
                        FadeOutAllGuests(affectedRuntimeIds);
                        DiagLog("ReplayBlackCard: all guests faded at portal");
                    }, "Shinki:ReplayGuestWalk", timeoutSeconds: GuestWalkDuration + 6f);
                }

                // 1s 延迟后神绮走向传送门
                CommandScheduler.Enqueue(() => true, () =>
                {
                    var shinkiArrivedAtPortal = false;
                    InitiateShinkiWalk(shinkiRid, portalPos, () => { shinkiArrivedAtPortal = true; });

                    // 神绮到达传送门 → 离场 → 销毁传送门
                    CommandScheduler.Enqueue(() => shinkiArrivedAtPortal, () =>
                    {
                        DiagLog("ReplayBlackCard: Shinki removing at portal");
                        FadeOutShinki(shinkiRid);

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

    //需要(SpellExecutionContext ctx) 符卡执行上下文
    //红卡主协程：开启传送门→注册buff→首次召唤→每15秒定时召唤
    //返回(IEnumerator) Unity协程
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

    //不需要，无参数
    //定时回调：检查剩余时间→每15秒召唤2位魔界客人→超时自动关闭传送门
    //不返回
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

    //需要(int count) 召唤数量
    //从魔界客人池中随机召唤指定数量客人，稀客概率1/3，支持验重和可用性检查
    //不返回
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

    //不需要，无参数
    //遍历场上所有活跃客人，收集属于魔界稀客池的客人ID集合用于验重
    //返回(HashSet<int>) 场上已有的魔界稀客ID集合
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

    //需要(bool portalAlreadyOpen) 传送门是否已开启
    //客机重放红卡效果：如传送门未开则创建传送门视觉
    //不返回
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

    //不需要，无参数
    //关闭传送门：取消定时器→清除状态→销毁视觉→移除buff
    //不返回
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
    // Buff 注册 / 移除（红卡传送门）—— 游戏原生 RegisterTimedBuff
    // ================================================================================

    //不需要，无参数
    //调用NativeBuffHelper.Register向游戏注册原生buff图标
    //不返回
    private static void RegisterPortalBuff()
    {
        // 如果设置了自定义图标，刷新 BuffDescription
        if (CustomBuffIcon != null)
        {
            DiagLog("RegisterPortalBuff: applying custom buff icon");
            NativeBuffHelper.RegisterCustomBuffDescription(
                NativeBuffHelper.BT.Null,
                title: "魔神降临",
                description: "每隔15秒从魔界传送门中随机召唤两位魔界人",
                visual: CustomBuffIcon);
        }

        DiagLog("RegisterPortalBuff: calling native RegisterTimedBuff");
        var ok = NativeBuffHelper.Register(NativeBuffHelper.BT.Null, float.MaxValue);
        DiagLog($"RegisterPortalBuff: {(ok ? "SUCCESS" : "FAILED")}");
    }

    //不需要，无参数
    //重置NativeBuffHelper内部注册状态标记
    //不返回
    private static void RemovePortalBuff()
    {
        NativeBuffHelper.Reset();
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

    /// <summary>
    /// 自定义 Buff 图标。设置后传送门激活时会覆盖 BuffDescription 的 visual。
    /// 需在红卡触发前（如 Spell 注册阶段）赋值。
    /// </summary>
    public static Sprite CustomBuffIcon { get; set; }

    // ScreenSpaceOverlay sortingOrder — 负数确保在游戏 UI 之下（UI 通常 ≥ 0）
    private const int PortalSortingOrder = -100;

    //需要(Vector3 position) 传送门世界坐标
    //在世界坐标处创建紫色矩形传送门视觉（ScreenSpaceOverlay Canvas），支持自定义工厂替换
    //不返回
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

    //不需要，无参数
    //销毁当前传送门视觉GameObject
    //不返回
    private static void DestroyPortalVisual()
    {
        if (_portalVisual != null)
        {
            UnityEngine.Object.Destroy(_portalVisual);
            _portalVisual = null;
            DiagLog("DestroyPortalVisual: portal destroyed");
        }
    }

    //需要(string[] spriteUris) 精灵uri数组, (float framesPerSecond) 帧率默认12
    //创建序列帧动画传送门工厂，预加载帧精灵→注册MonoBehaviour→返回可赋给CustomPortalVisualFactory的委托
    //返回(Func<Vector3, GameObject>) 传送门创建委托
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

    //需要(string uri) rex资源路径, (out Sprite sprite) 输出精灵
    //通过ResourceExManager加载精灵资源的便捷包装
    //返回(bool) true=加载成功
    private static bool TryGetSprite(string uri, out Sprite sprite)
        => ResourceExManager.TryGetSprite(uri, out sprite);

    // ================================================================================
    // 传送门位置
    // ================================================================================

    //不需要，无参数
    //根据PortalScreenXRatio/YRatio从屏幕坐标反算世界坐标，返回传送门放置位置
    //返回(Vector3) 传送门世界坐标
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

    //不需要，无参数
    //填充魔界稀客池(4人)和普客池(2人)的硬编码ID列表
    //不返回
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

    //不需要，无参数
    //开启黑卡FlyToSpawn拦截标志，客人FlyToSpawn时检查此标志决定行为
    //不返回
    private static void EnableBlackCardFlyToSpawnOverride()
    {
        _isBlackCardActive = true;
        DiagLog("BlackCard: FlyToSpawn override ENABLED");
    }

    //不需要，无参数
    //关闭黑卡FlyToSpawn拦截标志
    //不返回
    private static void DisableBlackCardFlyToSpawnOverride()
    {
        _isBlackCardActive = false;
        DiagLog("BlackCard: FlyToSpawn override DISABLED");
    }
}



[HarmonyPatch(typeof(GuestIconManager), "SwitchState")]
public static class ShinkiGuestIconManagerPatch
{
    //需要(GuestGroupController controller) 客人控制器, (GuestState state) 目标状态
    //Prefix检查：controller为null则跳过原方法，防止已清理客人触发NRE
    //返回(bool) true=执行原方法 false=跳过
    [HarmonyPrefix]
    public static bool SwitchState_Prefix(GuestGroupController controller, GuestState state)
    {
        return controller != null;
    }
}


[HarmonyPatch(typeof(PrototypingManagers.NightSceneDebugConsole), "Guests")]
public static class ShinkiDebugConsolePatch
{
    //需要(Exception __exception) Harmony拦截到的异常
    //Finalizer：吞掉NullReferenceException防止刷屏，其他异常照常抛出
    //返回(Exception) null=吞掉异常, 非null=照常抛出
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
