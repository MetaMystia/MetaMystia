using Common;
using Common.CharacterUtility;
using Common.UI;
using GameData.Core.Collections.CharacterUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Network;
using MetaMystia.UI;

using Mystia.Numerics;

namespace MetaMystia;

/// <summary>远端玩家的最新状态，以及当前场景中由模组创建的角色。</summary>
/// <remarks>
/// 角色本身仍由模组实例化、按网络位置驱动：框架提供角色句柄与精灵集（<c>BindCharacter</c>／
/// <c>ApplyCharacterSprite</c>），但没有「生成一个角色」「设置角色位置/输入速度」的服务，因此
/// <c>UnityEngine.Object.Instantiate/Destroy</c>、<c>Rigidbody2D</c>／<c>Collider2D</c> 直取与
/// <c>Time.fixedDeltaTime</c> 保留现状（缺口见交付报告）；模组自己持有的向量状态改用
/// <see cref="Mystia.Numerics"/> 的镜像值类型，只在读写角色接口的地方换算。
/// </remarks>
[AutoLog]
public partial class PeerPlayer : NetPlayer
{
    public string CharacterId => $"MetaMystia_{Uid}";
    public Player NetworkState { get; private set; }
    public Scene Scene => NetworkState?.Scene ?? Scene.EmptyScene;
    public bool HasMotion => NetworkState?.HasMotion == true;
    public bool IsSameMapAsLocal => MapLabel != MapLabel.Unknown && MapLabel == LocalPlayer.CurrentMapLabel;
    public bool CanRender => GameSession.IsOnline && GameFlow.CharactersReady
        && PlayerManager.TryGetVisiblePeer(Uid, out var current) && ReferenceEquals(this, current)
        && Scene == GameFlow.LocalScene
        && (Scene == Scene.DayScene || (Scene == Scene.WorkScene && PlayerManager.Peers.ContainsKey(Uid)));

    private CharacterControllerUnit character;
    private SceneDirector owner;
    private Vector2 positionOffset;

    public PeerPlayer(int uid, ResourceDataBase resources)
    {
        Uid = uid;
        IncrementalDataBase = resources;
        DataBase = resources.Expand();
    }

    public override CharacterControllerUnit GetCharacterUnit() => character;

    public void ApplyState(Player state)
    {
        bool skinChanged = NetworkState?.Skin != state.Skin;
        bool motionReceived = !ReferenceEquals(NetworkState?.Motion, state.Motion);
        NetworkState = state;
        Id = state.Name;
        if (skinChanged)
        {
            Skin = PlayerProfile.ReadSkin(state.Skin);
            UpdateCharacterSprite();
        }
        RefreshCharacter(motionReceived);
    }

    /// <summary>网络状态或场景就绪变化后，从最新快照更新角色。</summary>
    public void RefreshCharacter(bool updateMotion = true)
    {
        if (!CanRender || !HasMotion)
        {
            ReleaseCharacter();
            return;
        }
        if (Scene == Scene.DayScene && DayScene.SceneManager.Instance.IsMapSwapping) return;

        var motion = NetworkState.Motion;
        bool created = character == null;
        if (created)
        {
            owner = SceneDirector.Instance;
            // 角色 prefab 的实例化没有框架入口（框架只提供角色句柄与精灵集，没有生成角色的服务），
            // 故仍直接实例化游戏自带 prefab 并注册进场景的角色集合（缺口见交付报告）。
            character = UnityEngine.Object.Instantiate(DataBaseCharacter.CharacterBase, owner.transform)
                .GetComponent<CharacterControllerUnit>();
            character.name = CharacterId;
            // 游戏自己的参数决定要不要碰撞体：传 false 会让 CharacterControllerUnit.Initialize 直接
            // Destroy(cl2d) 并置 hasCollider = false（游戏剧情角色走的就是这条，SceneDirector.cs:406）。
            // 与「保留碰撞体再改成触发器」相比，物理结果相同（都不挡人、不与地图障碍碰撞，位置由网络驱动），
            // 但这里连触发事件也不会再发（白天交互区 InteractableArea 是按 Player tag 过滤的），
            // 而且游戏自己的 hasCollider 状态与实际一致。代价：远端角色身上的装饰件同样不带碰撞体。
            character.Initialize(Skin.ResolveSkin(), motion.Speed, false);
            owner.characterCollection.Add(CharacterId, character);
            character.AddInputProcessor<HeightBlendedInputProcessorComponent>();
            Skin.ApplyToUnit(character);
            Log.Info($"Created peer '{CharacterId}' in {Scene}");
        }

        var height = character.GetComponent<HeightBlendedInputProcessorComponent>();
        if (Scene == Scene.DayScene)
            height.Initialize(DayScene.SceneManager.Instance.CurrentActiveMap.height);
        else
            height.Initialize(NightScene.MapManager.Instance.height);

        MapLabel = motion.Map;
        Speed = motion.Speed;
        IsSprinting = motion.Sprinting;
        InputDirection = new(motion.DirectionX, motion.DirectionY);
        character.MoveSpeedMultiplier = Speed;
        character.sprintMultiplier = IsSprinting ? 1.5f : 1f;
        if (created || updateMotion)
        {
            // 与引擎相接的向量换算集中在这几行：模组侧一律用镜像 Vector2，引擎类型不出现名字。
            positionOffset = new Vector2(motion.X, motion.Y)
                - new Vector2(character.rb2d.position.x, character.rb2d.position.y);
            if (created || positionOffset.SqrMagnitude > 9f)
            {
                character.rb2d.position = new(motion.X, motion.Y);
                positionOffset = Vector2.Zero;
            }
        }
        bool visible = Scene == Scene.WorkScene || IsSameMapAsLocal;
        SetZ(visible ? 0 : -40815);
        character.cl2d.enabled = visible;
        FloatingTextHelper.SetPlayerLabel(Uid, LiveModeManager.GetDisplayName(Uid), character);
    }

    /// <summary>场景卸载由游戏销毁对象；中途离线由模组移除自己的角色。</summary>
    public void ReleaseCharacter(bool sceneUnloading = false)
    {
        if (!sceneUnloading)
        {
            FloatingTextHelper.RemovePlayerLabel(Uid);
            if (character != null)
            {
                if (owner != null && owner.characterCollection.TryGetValue(CharacterId, out var registered)
                    && registered == character)
                    owner.characterCollection.Remove(CharacterId);
                UnityEngine.Object.Destroy(character.gameObject);
            }
        }
        character = null;
        owner = null;
        ResetMotion();
    }

    public override void ResetMotion()
    {
        base.ResetMotion();
        positionOffset = Vector2.Zero;
    }

    public void OnFixedUpdate()
    {
        if (!CanRender || character == null || GameFlow.InStory) return;
        if (Scene == Scene.DayScene && DayScene.SceneManager.Instance.IsMapSwapping)
        {
            character.IsMoving = false;
            character.UpdateInputVelocity(new(0f, 0f));
            return;
        }
        var correction = positionOffset / 0.5f / 5f;
        // Time.fixedDeltaTime 没有框架替代入口（全局循环的 FixedUpdate 才有 delta，调用点不在本文件），保留现状。
        positionOffset -= correction * UnityEngine.Time.fixedDeltaTime * 5f * character.sprintMultiplier;
        var velocity = new Vector2(InputDirection.x + correction.X, InputDirection.y + correction.Y);
        if (velocity.SqrMagnitude < 0.0001f) velocity = Vector2.Zero;
        character.IsMoving = velocity != Vector2.Zero;
        character.UpdateInputVelocity(new(velocity.X, velocity.Y));
    }
}
