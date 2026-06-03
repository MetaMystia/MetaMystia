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
    private const float BlackCardWalkDuration = 4.0f;
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

    private const float ShinkiWalkToPortalDuration = 1.5f;
    private const float ShinkiFinalEnterDelay = 0.5f;

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

        // 2. 创建传送门
        _portalPosition = DeterminePortalPosition();
        CreatePortalVisual(_portalPosition);

        // 3. 广播网络同步
        var affectedIds = affectedGuests.Select(g => g.runtimeId).ToArray();
        int shinkiRid = shinkiGuest?.rid ?? -1;
        if (MpManager.IsConnected)
        {
            ShinkiBlackCardAction.Send(affectedIds, shinkiRid, _portalPosition);
        }

        // 4. Phase 1 清理（所有客人 + 神绮）
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

        // 5. 神绮先走向传送门右侧
        var shinkiStandPos = _portalPosition + new Vector3(1.5f, 0, 0);
        if (shinkiGuest != null && shinkiGuest.Value.fsm.Controller != null)
        {
            DiagLog($"  Shinki walking to portal-right, rid={shinkiGuest.Value.rid}, pos={shinkiStandPos}");
            shinkiGuest.Value.fsm.Controller.MoveToTargetPosition(
                -1, new Il2CppSystem.Nullable<Vector3>(shinkiStandPos), Vector3Int.zero, false, null);
        }

        // 6. 等待神绮到达传送门
        yield return new WaitForSeconds(ShinkiWalkToPortalDuration);

        // 7. 神绮切换为举旗精灵（如果素材已加载）
        if (shinkiGuest != null && shinkiGuest.Value.fsm.Controller != null)
        {
            SwitchShinkiToFlagSprite(shinkiGuest.Value.fsm.Controller);
        }

        // 8. 其他客人走向传送门
        foreach (var (rid, fsm, _) in affectedGuests)
        {
            if (fsm.Controller != null)
            {
                DiagLog($"  Moving guest rid={rid} to portal");
                fsm.Controller.MoveToTargetPosition(
                    -1, new Il2CppSystem.Nullable<Vector3>(_portalPosition), Vector3Int.zero, false, null);
            }
        }

        // 9. 等待其他客人走到传送门
        yield return new WaitForSeconds(BlackCardWalkDuration);

        // 10. Phase 2：移除所有其他客人（统一用 LeaveFromDesk 确保从 GuestsManager 内部列表移除）
        foreach (var (rid, fsm, _) in affectedGuests)
        {
            if (fsm.Controller == null) continue;

            DiagLog($"  Phase2: LeaveFromDesk(Fading) guest rid={rid}");
            GuestsManagerPatch.SkipLeaveFromDeskPatch.Grant();
            GuestsManagerPatch.SkipLeaveFromDeskBroadcastPatch.Grant();
            GuestsManagerPatch.LeaveFromDesk_ReversePatch(
                GuestsManager.Instance, fsm.Controller, GuestGroupController.LeaveType.Fading, null, false);
            GuestsMap.Remove(rid);
        }

        // 11. 神绮最后进入传送门
        yield return new WaitForSeconds(ShinkiFinalEnterDelay);
        if (shinkiGuest != null)
        {
            DiagLog($"  Shinki entering portal last, rid={shinkiGuest.Value.rid}");
            SwitchShinkiToOriginalSprite(shinkiGuest.Value.fsm.Controller);
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

        // 12. 展示传送门后销毁
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
    /// 客机重放黑卡效果（完整动画序列）
    /// </summary>
    public static void ReplayBlackCard(int[] affectedRuntimeIds, int shinkiRid, Vector3 portalPos)
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            CreatePortalVisual(portalPos);

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

            // 神绮先走到传送门右侧
            var shinkiStandPos = portalPos + new Vector3(1.5f, 0, 0);
            if (shinkiRid > 0)
            {
                var shinkiFsm = GuestsMap.GetGuestFsm(shinkiRid);
                if (shinkiFsm?.Controller != null)
                    shinkiFsm.Controller.MoveToTargetPosition(-1, new Il2CppSystem.Nullable<Vector3>(shinkiStandPos), Vector3Int.zero, false, null);
            }

            // 等神绮走到 → 切举旗 → 其他客人走 → 等待 → 移除
            CommandScheduler.Enqueue(() => true, () =>
            {
                // 神绮到达，切换举旗
                if (shinkiRid > 0)
                {
                    var shinkiFsm = GuestsMap.GetGuestFsm(shinkiRid);
                    if (shinkiFsm?.Controller != null)
                        SwitchShinkiToFlagSprite(shinkiFsm.Controller);
                }

                // 其他客人走向传送门
                foreach (var rid in affectedRuntimeIds)
                {
                    var fsm = GuestsMap.GetGuestFsm(rid);
                    if (fsm?.Controller != null)
                        fsm.Controller.MoveToTargetPosition(-1, new Il2CppSystem.Nullable<Vector3>(portalPos), Vector3Int.zero, false, null);
                }

                // 等待其他客人走到
                CommandScheduler.Enqueue(() => true, () =>
                {
                    // 移除其他客人（统一用 LeaveFromDesk 确保完整清理）
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

                    // 神绮最后进入
                    CommandScheduler.Enqueue(() => true, () =>
                    {
                        if (shinkiRid > 0)
                        {
                            var shinkiFsm = GuestsMap.GetGuestFsm(shinkiRid);
                            if (shinkiFsm?.Controller != null)
                            {
                                SwitchShinkiToOriginalSprite(shinkiFsm.Controller);
                                GuestsManagerPatch.SkipLeaveFromDeskPatch.Grant();
                                GuestsManagerPatch.SkipLeaveFromDeskBroadcastPatch.Grant();
                                GuestsManagerPatch.LeaveFromDesk_ReversePatch(
                                    GuestsManager.Instance, shinkiFsm.Controller,
                                    GuestGroupController.LeaveType.Fading, null, false);
                            }
                            GuestsMap.Remove(shinkiRid);
                        }

                        CommandScheduler.Enqueue(() => true, DestroyPortalVisual, "Shinki:DestroyPortal");
                    }, "Shinki:ShinkiFinalEnter", timeoutSeconds: ShinkiFinalEnterDelay + 1f);
                }, "Shinki:ReplayOthersWalk", timeoutSeconds: BlackCardWalkDuration + 1f);
            }, "Shinki:ReplayShinkiWalk", timeoutSeconds: ShinkiWalkToPortalDuration + 1f);
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

    private static void CreatePortalVisual(Vector3 position)
    {
        DestroyPortalVisual();

        if (CustomPortalVisualFactory != null)
        {
            DiagLog("CreatePortalVisual: using custom visual factory");
            _portalVisual = CustomPortalVisualFactory(position);
            return;
        }

        DiagLog("CreatePortalVisual: creating default ScreenSpace-Overlay Canvas portal");

        var canvasGO = new GameObject("Shinki_MakaiPortal_Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        canvasGO.AddComponent<CanvasScaler>();

        var imageGO = new GameObject("Shinki_MakaiPortal_Image");
        imageGO.transform.SetParent(canvasGO.transform, false);

        var image = imageGO.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(1f, 0f, 1f, 0.85f);

        var rt = image.rectTransform;
        rt.anchorMin = new Vector2(0.615f, 0.245f);
        rt.anchorMax = new Vector2(0.685f, 0.455f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _portalVisual = canvasGO;
        DiagLog($"CreatePortalVisual: DONE — anchorMin={rt.anchorMin}, anchorMax={rt.anchorMax}");
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

        return _ =>
        {
            var canvasGO = new GameObject("Shinki_Portal_Animated");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;
            canvasGO.AddComponent<CanvasScaler>();

            var imageGO = new GameObject("Shinki_Portal_Image");
            imageGO.transform.SetParent(canvasGO.transform, false);

            var image = imageGO.AddComponent<UnityEngine.UI.Image>();
            image.sprite = frameArray[0];

            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(0.615f, 0.245f);
            rt.anchorMax = new Vector2(0.685f, 0.455f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 保持原始精灵宽高比
            image.preserveAspect = true;

            var animator = canvasGO.AddComponent<PortalSpriteAnimator>();
            animator.Frames = frameArray;
            animator.FramesPerSecond = framesPerSecond;

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

        // 传送门 Canvas anchor 中心 = (0.615+0.685)/2, (0.245+0.455)/2 = (0.65, 0.35)
        var screenX = Screen.width * 0.65f;
        var screenY = Screen.height * 0.35f;
        var worldPos = cam.ScreenToWorldPoint(new Vector3(screenX, screenY, cam.nearClipPlane));
        var portalPos = new Vector3(worldPos.x, worldPos.y, 0);
        DiagLog($"DeterminePortalPosition: screen=({screenX:F0},{screenY:F0}), world={portalPos}");
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
/// 传送门序列帧动画驱动。挂在带 Image 的 Canvas GameObject 上。
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
