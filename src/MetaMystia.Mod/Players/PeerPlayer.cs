using System;
using UnityEngine;

using Common.CharacterUtility;
using DayScene.Interactables.Collections.ConditionComponents;
using GameData.RunTime.DaySceneUtility;

using MetaMystia.Network;
using MetaMystia.UI;
using SgrYuki;

namespace MetaMystia;

/// <summary>
/// 远程对端玩家实例，管理单个远程连接对象的角色状态和位置同步逻辑
/// </summary>
[AutoLog]
public partial class PeerPlayer : NetPlayer
{
    /// <summary>
    /// 角色在 characterCollection 中的标识符（使用 "peer_{uid}" 格式）
    /// </summary>
    public string CharacterId { get; set; }

    /// <summary>
    /// 角色模型 ID（用于 SpawnCharacter），后续会允许玩家自定义
    /// </summary>
    public int CharacterModelId { get; set; } = 14;

    public bool IsSameMapAsLocal => MapLabel == LocalPlayer.CurrentMapLabel;

    private string SpawnCommandKey => $"PeerSpawn_{CharacterId}";

    private static bool IsUnitReady(CharacterControllerUnit u) =>
        u != null && u.rb2d != null && u.cl2d != null;

    #region 角色运动速度修正
    /// <summary>
    /// 实际的运动速度（由对端同步输入方向计算得出）
    /// </summary>
    private Vector2 actualVelocity;
    /// <summary>
    /// 位置偏移（用于插值修正瞬移），由 SyncFromPeer 更新，在 FixedUpdate 中逐渐衰减修正
    /// </summary>
    private Vector2 positionOffset;
    /// <summary>
    /// 当前速度（用于指数衰减模型），在 FixedUpdate 中根据 positionOffset 更新
    /// 不是直接设置给角色的速度，而是用于计算插值修正的中间变量
    /// 这样可以实现对位置偏移的平滑修正，避免瞬移感，同时允许对端输入引起的实际运动速度正常作用
    /// 这个设计是为了在网络同步中平衡响应性和视觉平滑度，尤其是在网络延迟较高时
    /// 通过调整衰减速率，可以控制修正的快慢，找到适合游戏体验的参数
    /// </summary>
    private Vector2 currentVelocity;
    #endregion

    private bool firstSync = true;

    /// <summary>
    /// 一个足够小的Z值，用于在摄像机中隐藏角色渲染
    /// </summary>
    private readonly int LARGE_Z_VALUE = -40815;

    /// <summary>
    /// 一个足够远的坐标，用于 peer 生成的初始位置，后由位置同步修正到真实位置
    /// </summary>
    private readonly float FAR_POS = 40815f;

    /// <summary>
    /// 构造函数，接受玩家 UID 和可选的增量资源数据库（自动展开并缓存）
    /// </summary>
    /// <param name="uid">玩家 UID，由主机分配</param>
    /// <param name="incrementalDataBase">增量格式资源数据库，为 null 时使用本地资源</param>
    public PeerPlayer(int uid, ResourceDataBase incrementalDataBase = null)
    {
        Uid = uid;
        CharacterId = $"MetaMystia_{uid}";
        if (incrementalDataBase != null)
        {
            IncrementalDataBase = incrementalDataBase;
            DataBase = ResourceDataBase.Expand(incrementalDataBase);
        }
        else
        {
            DataBase = new ResourceDataBase().LoadResourceIds();
        }
    }

    public override void ResetState()
    {
        base.ResetState();
        firstSync = true;
        Log.LogMessage($"PeerPlayer '{CharacterId}' state reset");
    }

    /// <summary>
    /// 重置运动插值相关变量
    /// </summary>
    public override void ResetMotion()
    {
        base.ResetMotion();
        actualVelocity = Vector2.zero;
        positionOffset = Vector2.zero;
        currentVelocity = Vector2.zero;
    }

    #region 角色生命周期

    /// <summary>
    /// 根据当前场景生成角色并延迟设置（HeightProcessor、碰撞忽略、可见性）。
    /// 仅在 DayScene / WorkScene 中有效，其他场景不做任何操作。
    /// 整个流程会等待 Local unit 初始化完毕后才开始。
    /// </summary>
    public void SpawnForScene()
    {
        var scene = MpManager.LocalScene;
        if (scene is not Common.UI.Scene.DayScene and not Common.UI.Scene.WorkScene)
        {
            Log.LogDebug($"SpawnForScene called in {scene}, skipping for '{CharacterId}'");
            return;
        }

        CommandScheduler.RemoveKeyFromKeyQueue(SpawnCommandKey);
        bool visible = scene == Common.UI.Scene.WorkScene;
        var spawnPos = new Vector2(FAR_POS + Uid, FAR_POS);
        Func<bool> readyWhen = scene == Common.UI.Scene.DayScene
            ? () => IsUnitReady(PlayerManager.Local.unit)
                && DayScene.SceneManager.Instance?.CurrentActiveMap != null
            : () => IsUnitReady(PlayerManager.Local.unit)
                && NightScene.MapManager.Instance?.height != null;

        CommandScheduler.EnqueueKey(
            key: SpawnCommandKey,
            executeWhen: readyWhen,
            execute: () =>
            {
                SpawnCharacter(spawnPos);
                CommandScheduler.Enqueue(
                    executeWhen: () => IsUnitReady(unit),
                    execute: () => PostSpawnSetup(visible),
                    timeoutSeconds: 30
                );
            },
            timeoutSeconds: 60
        );
        Log.LogMessage($"PeerPlayer '{CharacterId}' spawn scheduled for {scene} at ({spawnPos.x}, {spawnPos.y})");
    }

    /// <summary>
    /// 取消 pending spawn，从 characterCollection 移除并销毁对端角色 GameObject。
    /// </summary>
    public void DespawnCharacter()
    {
        CommandScheduler.RemoveKeyFromKeyQueue(SpawnCommandKey);
        var collection = Common.SceneDirector.Instance?.characterCollection;
        if (collection == null || !collection.TryGetValue(CharacterId, out var existing))
            return;

        collection.Remove(CharacterId);
        if (existing != null)
            UnityEngine.Object.Destroy(existing.gameObject);
        Log.LogMessage($"Despawned character '{CharacterId}'");
    }

    /// <summary>
    /// 角色生成后的延迟初始化：高度处理器、碰撞忽略、可见性、头顶标签
    /// </summary>
    private void PostSpawnSetup(bool visible)
    {
        if (!IsUnitReady(unit)) return;

        TryAddHeightProcessor();
        IgnoreCollisionWithSelf();
        UpdateVisibleState(visible);
        FloatingTextHelper.SetPlayerLabel(Uid, LiveModeManager.GetDisplayName(Uid), unit.transform);
        Skin.ApplyToUnit(unit);
        Log.LogMessage($"PeerPlayer '{CharacterId}' post-spawn setup done (visible={visible})");
    }

    private void SpawnCharacter(Vector2 position)
    {
        var collection = Common.SceneDirector.Instance?.characterCollection;
        if (collection == null) return;

        if (collection.TryGetValue(CharacterId, out var existing))
        {
            if (IsUnitReady(existing))
            {
                Log.LogInfo($"Character '{CharacterId}' already exists, skip spawning");
                return;
            }
            collection.Remove(CharacterId);
            if (existing != null)
                UnityEngine.Object.Destroy(existing.gameObject);
            Log.LogInfo($"Removed stale character '{CharacterId}' before respawn");
        }

        Common.SceneDirector.Instance.SpawnCharacter(Common.SceneDirector.Identity.Special, CharacterModelId, position, CharacterId);
        Log.LogMessage($"Spawned character '{CharacterId}' at ({position.x}, {position.y})");
    }

    private void TryAddHeightProcessor()
    {
        if (!IsUnitReady(unit)) return;

        if (unit.GetComponent<HeightBlendedInputProcessorComponent>() == null)
            unit.AddInputProcessor<HeightBlendedInputProcessorComponent>();

        var heightProcessor = unit.GetComponent<HeightBlendedInputProcessorComponent>();
        switch (MpManager.LocalScene)
        {
            case Common.UI.Scene.DayScene:
                heightProcessor.Initialize(DayScene.SceneManager.Instance.CurrentActiveMap.height);
                break;
            case Common.UI.Scene.WorkScene:
                heightProcessor.Initialize(NightScene.MapManager.Instance.height);
                break;
        }
    }

    public void IgnoreCollisionWithSelf(bool ignore = true)
    {
        if (!IsUnitReady(unit)) return;

        var collection = Common.SceneDirector.Instance?.characterCollection;
        if (collection != null && collection.TryGetValue("Self", out var selfUnit) && IsUnitReady(selfUnit))
            Physics2D.IgnoreCollision(unit.cl2d, selfUnit.cl2d, ignore);

        foreach (var peer in PlayerManager.Peers.Values)
        {
            if (peer == this || !IsUnitReady(peer.unit)) continue;
            Physics2D.IgnoreCollision(unit.cl2d, peer.unit.cl2d, ignore);
        }
    }

    #endregion

    public override CharacterControllerUnit GetCharacterUnit()
    {
        if (Common.SceneDirector.Instance?.characterCollection.TryGetValue(CharacterId, out var characterUnit) != true)
            return null;
        return IsUnitReady(characterUnit) ? characterUnit : null;
    }

    public CharacterConditionComponent GetCharacterComponent() =>
        GetCharacterUnit()?.GetComponent<CharacterConditionComponent>();

    #region FixedUpdate 位置插值

    public void OnFixedUpdate()
    {
        if (MpManager.ShouldSkipAction) return;

        var unit = GetCharacterUnit();
        if (unit == null) return;

        // 指数衰减模型修正位置偏移
        currentVelocity = positionOffset / 0.5f / 5f;
        positionOffset -= currentVelocity * Time.fixedDeltaTime * 5f * unit.sprintMultiplier;

        var velocity = actualVelocity + currentVelocity;
        if (velocity.magnitude < 0.01f)
        {
            if (unit.IsMoving) unit.IsMoving = false;
            if (unit.MoveSpeedMultiplier != Speed) unit.MoveSpeedMultiplier = Speed;
            return;
        }

        if (!unit.IsMoving) unit.IsMoving = true;
        unit.UpdateInputVelocity(velocity);

        if (MpManager.LocalScene == Common.UI.Scene.DayScene)
        {
            var trackedNPC = RunTimeDayScene.GetTrackedNPC(CharacterId);
            var position = unit.rb2d.position;
            trackedNPC?.overridePosition?.position = new Il2CppSystem.Collections.Generic.KeyValuePair<float, float>(
                position.x, position.y
            ); // TODO: 也许有更优雅的方式？
        }
    }

    #endregion

    #region 网络同步

    /// <summary>
    /// DayScene 同步：接收对端的地图、奔跑、方向、位置
    /// </summary>
    public void SyncFromPeer(MapLabel mapLabel, bool isSprinting, float speed, Vector2 inputDirection, Vector2 position)
    {
        if (unit == null)
        {
            Log.LogWarning($"SyncFromPeer: character '{CharacterId}' not found, spawning");
            SpawnForScene();
            return; // 等待下一次 sync，SpawnForScene 是异步的
        }

        if (firstSync)
        {
            position = new Vector3(position.x, position.y, unit.transform.position.z);

            firstSync = false;
            Log.LogInfo($"First sync for '{CharacterId}', teleported to ({position.x}, {position.y})");
        }

        if (mapLabel != MapLabel)
        {
            MapLabel = mapLabel;
            OnMapChanged();
        }

        // 更新运动状态
        Speed = speed;
        unit.MoveSpeedMultiplier = speed;
        actualVelocity = inputDirection;
        InputDirection = inputDirection;
        unit.IsMoving = inputDirection.magnitude > 0;
        unit.sprintMultiplier = isSprinting ? 1.5f : 1.0f;
        IsSprinting = isSprinting;

        // 位置修正
        UpdateOffsetPosition(unit, position);
        UpdateVisibleState();
    }

    /// <summary>
    /// WorkScene 同步：仅方向和位置
    /// </summary>
    public void NightSyncFromPeer(float speed, Vector2 inputDirection, Vector2 position)
    {
        if (!IsUnitReady(unit)) return;

        Speed = speed;
        unit.MoveSpeedMultiplier = speed;
        actualVelocity = inputDirection;
        InputDirection = inputDirection;
        unit.IsMoving = inputDirection.magnitude > 0;

        UpdateOffsetPosition(unit, position);
    }

    private void UpdateOffsetPosition(CharacterControllerUnit unit, Vector2 syncPosition)
    {
        positionOffset = syncPosition - rb2d.position;

        if (positionOffset.magnitude > 3.0f)
        {
            Log.Info($"Position offset too large ({positionOffset.magnitude}), teleporting '{CharacterId}'");
            rb2d.transform.position = new Vector3(syncPosition.x, syncPosition.y, rb2d.transform.position.z);
            positionOffset = Vector2.zero;
        }
    }

    #endregion

    #region 可见性

    public void UpdateVisibleState(bool? forceVisible = null)
    {
        if (!IsUnitReady(unit)) return;

        bool visible = forceVisible ?? IsSameMapAsLocal;
        SetZ(visible ? 0 : LARGE_Z_VALUE);
        unit.cl2d.enabled = visible;
    }

    private void OnMapChanged()
    {
        Log.LogInfo($"{CharacterId} map changed to {MapLabel}");
        TryAddHeightProcessor();
        UpdateVisibleState();
    }

    #endregion
}
