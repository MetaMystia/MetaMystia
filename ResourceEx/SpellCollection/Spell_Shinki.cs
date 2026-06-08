using System.Collections.Generic;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections;
using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage.Collections;
using GameData.RunTime.NightSceneUtility;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;
using SgrYuki.Utils;
using MetaMystia.Patch;

namespace MetaMystia.ResourceEx.SpellCollection;

[AutoLog]
public partial class Spell_Shinki : SpellBase
{
    private const int PortalBuffId = 9004;
    private const int PortalDurationSeconds = 60;
    private const int SummonIntervalSeconds = 15;
    private const int SummonCountPerWave = 2;
    private const float SpecialGuestChance = 1f / 3f;
    private const float BlackCardWalkToGateSeconds = 3.5f;
    private const float BlackCardGateScreenXRatio = 0.50f;
    private const float BlackCardGateScreenYRatio = 0.25f;
    private const float BlackCardGateWorldOffsetX = 1.5f;
    private const float BlackCardQueueColumnSpacing = 0.28f;
    private const float BlackCardQueueRowSpacing = 0.20f;

    private static readonly int[] MakaiSpecialGuestCandidates =
    [
        1002,  // Alice
        5005,  // Luize
        11000, // Yuki
        11001, // Mai
    ];

    private static readonly int[] MakaiNormalGuestCandidates =
    [
        5000, // Card soldier
        5001, // Clown
    ];

    private static bool _portalActive;
    private static int _portalGeneration;
    private static float _portalEndTime;

    public override string OnGettingSpellOwnerIdentifier() => "_ResourceExample_Shinki";

    public override Il2CppSystem.Collections.IEnumerator OnPositiveBuffExecute(SpellExecutionContext ctx)
        => PositiveBuffRoutine(ctx).WrapToIl2Cpp();

    public override Il2CppSystem.Collections.IEnumerator OnNegativeBuffExecute(SpellExecutionContext ctx)
        => NegativeBuffRoutine(ctx).WrapToIl2Cpp();

    [HideFromIl2Cpp]
    private System.Collections.IEnumerator PositiveBuffRoutine(SpellExecutionContext ctx)
    {
        RegisterPortalBuffDescription();
        EventManager.Instance.RemoveAllRegisteredTimedBuff(PortalBuffType);
        EventManager.Instance.RegisterTimedBuff(PortalDurationSeconds, PortalBuffType, out var _, null, null, null);

        _portalEndTime = Time.time + PortalDurationSeconds;
        var portalAlreadyOpen = _portalActive;
        _portalActive = true;
        _portalGeneration++;
        var generation = _portalGeneration;
        Notify(portalAlreadyOpen
            ? "神绮维持魔界传送门，新的客人正穿过边界。"
            : "神绮开启了魔界传送门，魔界的客人陆续抵达。");

        SummonMakaiGuests(SummonCountPerWave);

        while (_portalActive && generation == _portalGeneration && Time.time < _portalEndTime)
        {
            var nextWaveTime = Mathf.Min(Time.time + SummonIntervalSeconds, _portalEndTime);
            while (_portalActive && generation == _portalGeneration && Time.time < nextWaveTime)
            {
                yield return new WaitForSeconds(0.5f);
            }

            if (_portalActive && generation == _portalGeneration && Time.time < _portalEndTime)
            {
                SummonMakaiGuests(SummonCountPerWave);
            }
        }

        if (generation == _portalGeneration)
        {
            CleanupPortal();
        }
    }

    [HideFromIl2Cpp]
    private System.Collections.IEnumerator NegativeBuffRoutine(SpellExecutionContext ctx)
    {
        CleanupPortal();

        var guests = CollectActiveGuests();
        if (guests.Count == 0)
        {
            Notify("神绮打开了魔界之门，但现在没有客人需要远行。");
            yield return null;
            yield break;
        }

        Notify($"神绮邀请 {guests.Count} 组客人前往魔界游玩。");
        Log.Info($"[Spell_Shinki] 黑卡发动，准备送离 {guests.Count} 组客人");

        var gatePosition = DetermineBlackCardGatePosition();
        var stagedCount = 0;
        for (var i = 0; i < guests.Count; i++)
        {
            if (TryStageGuestForBlackCard(guests[i].Controller, gatePosition, stagedCount))
            {
                stagedCount++;
            }
        }

        if (stagedCount > 0)
        {
            Log.Info($"[Spell_Shinki] 黑卡引导 {stagedCount}/{guests.Count} 组客人前往魔界门");
            yield return new WaitForSeconds(BlackCardWalkToGateSeconds);
        }

        var banishedCount = 0;
        foreach (var guest in guests)
        {
            if (TryBanishGuest(guest))
            {
                banishedCount++;
                yield return new WaitForSeconds(0.05f);
            }
        }

        Notify($"绮符「环游魔界80天」送走了 {banishedCount} 组客人。");
        Log.Info($"[Spell_Shinki] 黑卡结束，成功送离 {banishedCount}/{guests.Count} 组客人");
        yield return null;
    }

    private static EventManager.BuffType PortalBuffType => (EventManager.BuffType)PortalBuffId;

    private static void RegisterPortalBuffDescription()
    {
        var visual = DataBaseLanguage.BuffDescription[EventManager.BuffType.PhilosopherStone]?.Visual;
        DataBaseLanguage.BuffDescription[PortalBuffType] = new GameData.CoreLanguage.ObjectLanguageBase(
            "「魔神降临」",
            "$a 秒内神绮开启魔界传送门，每 15 秒召唤两位魔界人",
            visual);
    }

    private static void SummonMakaiGuests(int count)
    {
        if (GuestsManager.Instance == null)
        {
            Log.Warning("[Spell_Shinki] SummonMakaiGuests: GuestsManager.Instance is null");
            return;
        }

        var availableSpecial = GetAvailableMakaiSpecialGuestIds();
        var availableNormal = GetAvailableMakaiNormalGuestIds();
        if (availableSpecial.Count == 0 && availableNormal.Count == 0)
        {
            Log.Warning("[Spell_Shinki] SummonMakaiGuests: 没有可用的魔界客人 ID");
            return;
        }

        var summoned = 0;
        while (summoned < count && (availableSpecial.Count > 0 || availableNormal.Count > 0))
        {
            var shouldSummonSpecial = availableSpecial.Count > 0
                && (availableNormal.Count == 0 || UnityEngine.Random.value < SpecialGuestChance);

            if (shouldSummonSpecial)
            {
                var index = UnityEngine.Random.Range(0, availableSpecial.Count);
                var guestId = availableSpecial[index];
                availableSpecial.RemoveAt(index);

                if (IsSpecialGuestPresent(guestId))
                {
                    continue;
                }

                if (!TrySpawnSpecialGuest(guestId))
                {
                    continue;
                }
            }
            else
            {
                var index = UnityEngine.Random.Range(0, availableNormal.Count);
                var guestId = availableNormal[index];
                availableNormal.RemoveAt(index);

                if (!TrySpawnNormalGuest(guestId))
                {
                    continue;
                }
            }

            summoned++;
        }

        Log.Info($"[Spell_Shinki] 魔界传送门召唤 {summoned}/{count} 组客人");
    }

    private static List<int> GetAvailableMakaiSpecialGuestIds()
    {
        var result = new List<int>();
        foreach (var guestId in MakaiSpecialGuestCandidates)
        {
            if (!CanUseSpecialGuestId(guestId)) continue;
            result.Add(guestId);
        }
        return result;
    }

    private static List<int> GetAvailableMakaiNormalGuestIds()
    {
        var result = new List<int>();
        foreach (var guestId in MakaiNormalGuestCandidates)
        {
            if (!CanUseNormalGuestId(guestId)) continue;
            result.Add(guestId);
        }
        return result;
    }

    private static bool CanUseSpecialGuestId(int guestId)
    {
        try
        {
            if (MpManager.IsConnected && !PlayerManager.SpecialGuestAvailable(guestId)) return false;
            return DataBaseCharacter.RefSGuest(guestId) != null;
        }
        catch (System.Exception e)
        {
            Log.Warning($"[Spell_Shinki] 稀客 ID {guestId} 不可用: {e.Message}");
            return false;
        }
    }

    private static bool CanUseNormalGuestId(int guestId)
    {
        try
        {
            if (MpManager.IsConnected && !PlayerManager.NormalGuestAvailable(guestId)) return false;
            return DataBaseCharacter.RefNGuest(guestId) != null;
        }
        catch (System.Exception e)
        {
            Log.Warning($"[Spell_Shinki] 普客 ID {guestId} 不可用: {e.Message}");
            return false;
        }
    }

    private static bool TrySpawnSpecialGuest(int guestId)
    {
        try
        {
            var spawned = GuestsManager.Instance.SpawnSpecialGuestGroup(
                guestId,
                SpecialGuestsController.GuestSpawnType.Normal,
                new Il2CppSystem.Nullable<Vector3>(),
                null,
                GuestGroupController.LeaveType.Move,
                false,
                -1,
                false,
                null,
                true);

            if (spawned == null)
            {
                Log.Warning($"[Spell_Shinki] 召唤稀客 ID {guestId} 被游戏拒绝");
                return false;
            }

            IzakayaConfigure.Instance.SetThisGuestHasSpawned(guestId);
            Notify($"魔界传送门召来了 {GetSpecialGuestName(guestId)}。");
            return true;
        }
        catch (System.Exception e)
        {
            Log.Warning($"[Spell_Shinki] 召唤稀客 ID {guestId} 失败: {e}");
            return false;
        }
    }

    private static bool TrySpawnNormalGuest(int guestId)
    {
        try
        {
            var normalGuest = DataBaseCharacter.RefNGuest(guestId);
            if (normalGuest == null)
            {
                Log.Warning($"[Spell_Shinki] 普客 ID {guestId} 不存在");
                return false;
            }

            var guests = new Il2CppSystem.Collections.Generic.List<NormalGuest>();
            guests.Add(normalGuest);

            var spawned = GuestsManager.Instance.SpawnNormalGuestGroup(
                guests.ToIEnumerable(),
                new Il2CppSystem.Nullable<Vector3>(),
                GuestGroupController.LeaveType.Move,
                -1,
                true);

            if (spawned == null)
            {
                Log.Warning($"[Spell_Shinki] 召唤普客 ID {guestId} 被游戏拒绝");
                return false;
            }

            Notify($"魔界传送门召来了 {GetNormalGuestName(guestId)}。");
            return true;
        }
        catch (System.Exception e)
        {
            Log.Warning($"[Spell_Shinki] 召唤普客 ID {guestId} 失败: {e}");
            return false;
        }
    }

    private static string GetSpecialGuestName(int guestId)
    {
        try
        {
            return DataBaseCharacter.RefSGuest(guestId)?.Text?.Name ?? $"GuestId={guestId}";
        }
        catch
        {
            return $"GuestId={guestId}";
        }
    }

    private static string GetNormalGuestName(int guestId)
    {
        try
        {
            return DataBaseCharacter.RefNGuest(guestId)?.Text?.Name ?? $"GuestId={guestId}";
        }
        catch
        {
            return $"GuestId={guestId}";
        }
    }

    private static bool IsSpecialGuestPresent(int guestId)
    {
        var seated = GuestsManager.Instance?.AllGuestInDeskController;
        if (seated != null)
        {
            var count = Il2CppSystem.Linq.Enumerable.Count<GuestGroupController>(seated);
            for (var i = 0; i < count; i++)
            {
                var group = Il2CppSystem.Linq.Enumerable.ElementAt<GuestGroupController>(seated, i);
                if (GetSpecialGuestId(group) == guestId)
                {
                    return true;
                }
            }
        }

        var queued = GuestGroupController.QueuedGuestControllers;
        if (queued != null)
        {
            for (var i = 0; i < queued.Count; i++)
            {
                if (GetSpecialGuestId(queued[i]) == guestId)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int GetSpecialGuestId(GuestGroupController group)
    {
        if (group == null || group.ControllType != GuestsManager.GuestType.Special)
        {
            return -1;
        }

        var special = group.TryCast<SpecialGuestsController>();
        return special?.SpecialGuest?.Id ?? -1;
    }

    private readonly struct BlackCardGuest
    {
        public BlackCardGuest(GuestGroupController controller)
        {
            Controller = controller;
            DeskCode = controller?.DeskCode ?? -1;
            WasQueued = controller?.queued ?? false;
        }

        public GuestGroupController Controller { get; }

        public int DeskCode { get; }

        public bool WasQueued { get; }
    }

    private static List<BlackCardGuest> CollectActiveGuests()
    {
        var result = new List<BlackCardGuest>();
        var seen = new HashSet<System.IntPtr>();

        void Add(GuestGroupController group)
        {
            if (group == null) return;
            if (!seen.Add(group.Pointer)) return;
            result.Add(new BlackCardGuest(group));
        }

        foreach (var (_, fsm) in GuestsMap.GetAllGuestsSnapshot())
        {
            if (!IsActiveGuestFsm(fsm)) continue;
            Add(fsm.Controller);
        }

        var seated = GuestsManager.Instance?.AllGuestInDeskController;
        if (seated != null)
        {
            var count = Il2CppSystem.Linq.Enumerable.Count<GuestGroupController>(seated);
            for (var i = 0; i < count; i++)
            {
                Add(Il2CppSystem.Linq.Enumerable.ElementAt<GuestGroupController>(seated, i));
            }
        }

        var queued = GuestGroupController.QueuedGuestControllers;
        if (queued != null)
        {
            for (var i = 0; i < queued.Count; i++)
            {
                Add(queued[i]);
            }
        }

        return result;
    }

    private static bool IsActiveGuestFsm(GuestFSM fsm)
    {
        if (fsm?.Controller == null) return false;
        return fsm.CurrentState != GuestFSM.State.None
            && fsm.CurrentState != GuestFSM.State.Left
            && fsm.CurrentState != GuestFSM.State.Dead
            && fsm.Controller.HaveNotLeft();
    }

    private static Vector3 DetermineBlackCardGatePosition()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Log.Warning("[Spell_Shinki] DetermineBlackCardGatePosition: Camera.main is null, using world origin");
            return Vector3.zero;
        }

        var screenX = Screen.width * BlackCardGateScreenXRatio;
        var screenY = Screen.height * BlackCardGateScreenYRatio;
        var worldPos = cam.ScreenToWorldPoint(new Vector3(screenX, screenY, cam.nearClipPlane));
        var gatePosition = new Vector3(worldPos.x + BlackCardGateWorldOffsetX, worldPos.y, 0f);
        Log.Info($"[Spell_Shinki] 魔界门目标位置 screen=({screenX:F0},{screenY:F0}) world={gatePosition}");
        return gatePosition;
    }

    private static Vector3 GetBlackCardQueueOffset(int queueIndex)
    {
        var column = queueIndex % 5;
        var row = queueIndex / 5;
        return new Vector3(
            (column - 2) * BlackCardQueueColumnSpacing,
            -row * BlackCardQueueRowSpacing,
            0f);
    }

    private static bool TryStageGuestForBlackCard(GuestGroupController guest, Vector3 gatePosition, int queueIndex)
    {
        if (guest == null || !guest.HaveNotLeft()) return false;

        try
        {
            var targetPosition = gatePosition + GetBlackCardQueueOffset(queueIndex);
            guest.MoveToTargetPosition(
                -1,
                new Il2CppSystem.Nullable<Vector3>(targetPosition),
                Vector3Int.zero,
                false,
                null);
            return true;
        }
        catch (System.Exception e)
        {
            Log.Warning($"[Spell_Shinki] 黑卡引导客人移动失败: {e}");
            return false;
        }
    }

    private static bool TryBanishGuest(BlackCardGuest guest)
    {
        try
        {
            ForceCleanupBlackCardGuest(guest);
            return true;
        }
        catch (System.Exception e)
        {
            Log.Warning($"[Spell_Shinki] 送离客人失败: {e}");
            return false;
        }
    }

    private static void ForceCleanupBlackCardGuest(BlackCardGuest guest)
    {
        var controller = guest.Controller;
        if (controller == null) return;

        controller.GetFund = 0;
        GuestService.CleanGuestOrderRegistration(controller);

        if (guest.DeskCode != -1)
        {
            GuestsManager.Instance.RemoveFromPatientCountdown(controller);
            GuestFSM.TryCloseServePanel(guest.DeskCode);
            GuestsManagerPatch.SkipLeaveFromDeskBroadcastPatch.Grant();
            GuestsManagerPatch.LeaveFromDesk_ReversePatch(
                GuestsManager.Instance,
                controller,
                GuestGroupController.LeaveType.Fading,
                null,
                false);
            return;
        }

        if (guest.WasQueued || controller.queued)
        {
            try
            {
                controller.RemoveFromQueue();
            }
            catch
            {
                // MoveToTargetPosition may already have removed the group from the queue.
            }

            GuestsManager.Instance.RemoveFromPatientCountdown(controller);
        }

        controller.FlyToSpawn(true);
    }

    private static void CleanupPortal()
    {
        if (!_portalActive) return;

        _portalActive = false;
        _portalGeneration++;
        EventManager.Instance?.RemoveAllRegisteredTimedBuff(PortalBuffType);
        Log.Info("[Spell_Shinki] 魔界传送门关闭");
    }

    private static void Notify(string message)
        => Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage(message);

    /*
     * 后续视觉预留：
     *
     * patch 中曾经用 ScreenSpace-Overlay Canvas 强制画一个传送门，并在黑卡里把客人移动到
     * 传送门位置后再淡出。那套实现需要额外处理 UI 生命周期、联机回放和 FlyToSpawn 行为拦截，
     * 目前先不启用，避免为了视觉效果引入大范围 Harmony patch。
     *
     * 可复审方向：
     * - 用 ResourceExManager.TryGetSprite("rex://ResourceExample/assets/Spell/9004_1.png", out sprite)
     *   加载传送门序列帧或静态图。
     * - 用一个独立的 CustomPortalVisualFactory 创建/销毁视觉对象。
     * - 如果要让黑卡客人“走进传送门”，优先在 Spell_Shinki 内部显式调度移动和清理，
     *   不要 patch GuestGroupController.FlyToSpawn 的全局行为。
     */
}
