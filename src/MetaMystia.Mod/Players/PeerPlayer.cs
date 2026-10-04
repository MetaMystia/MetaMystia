using Common;
using Common.CharacterUtility;
using Common.UI;
using GameData.Core.Collections.CharacterUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Network;
using MetaMystia.UI;

using Mystia.Assets;
using Mystia.Numerics;
using Mystia.Scenes;

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
    public Scene Scene => (NetworkState?.Scene ?? PlayerScene.Empty).ToGameScene();
    public bool HasMotion => NetworkState?.HasMotion == true;
    public bool IsSameMapAsLocal => MapLabel != MapLabel.Unknown && MapLabel == LocalPlayer.CurrentMapLabel;
    public bool CanRender => GameSession.IsOnline && GameFlow.CharactersReady
        && PlayerManager.TryGetVisiblePeer(Uid, out var current) && ReferenceEquals(this, current)
        && Scene == GameFlow.LocalScene
        && (Scene == Scene.DayScene || (Scene == Scene.WorkScene && PlayerManager.Peers.ContainsKey(Uid)));

    private CharacterControllerUnit character;
    private CharacterHandle handle;

    /// <summary>远端角色由本模组创建，句柄就是创建时拿到的那个。</summary>
    public override CharacterHandle? CharacterHandle => handle;
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
            handle = null;
            // 生成走框架的角色面：它按游戏剧情的方式克隆 prefab、按 label 登记进场景角色表，
            // 并让角色自己决定带不带碰撞体（false = 游戏自己的"不要碰撞体"，剧情角色就是这条）。
            handle = ModRuntime.CommonServices.Characters.CreateCharacter(
                new CharacterCreateSpec(CharacterId, Skin.ResolveSkin(), motion.Speed));
            if (handle is null || !owner.characterCollection.TryGetValue(CharacterId, out character) || character == null)
            {
                handle = null;
                character = null;
                Log.Error($"Failed to create peer '{CharacterId}' in {Scene}");
                return;
            }

            Skin.ApplyToUnit(character);
            Log.Info($"Created peer '{CharacterId}' in {Scene}");
        }

        // 身高融合按当前运行场景的地图高度图重设，取代原来的 Scene 分支。
        ModRuntime.CommonServices.Characters.SetCharacterHeightBlending(handle);

        MapLabel = motion.Map;
        Speed = motion.Speed;
        IsSprinting = motion.Sprinting;
        InputDirection = new(motion.DirectionX, motion.DirectionY);
        character.MoveSpeedMultiplier = Speed;
        character.sprintMultiplier = IsSprinting ? 1.5f : 1f;
        var characters = ModRuntime.CommonServices.Characters;
        if (created || updateMotion)
        {
            var at = characters.TryGetCharacterPosition(handle, out var current) ? current : Vector2.Zero;
            positionOffset = new Vector2(motion.X, motion.Y) - at;
            if (created || positionOffset.SqrMagnitude > 9f)
            {
                characters.SetCharacterPosition(handle, new(motion.X, motion.Y));
                positionOffset = Vector2.Zero;
            }
        }
        bool visible = Scene == Scene.WorkScene || IsSameMapAsLocal;
        SetZ(visible ? 0 : -40815);
        // 远端角色按游戏自己的参数创建（不带碰撞体，见 CharacterCreateSpec 的说明），因此这里不再开关
        // 碰撞体：那个组件已被游戏销毁，写它会在每帧抛 MissingReferenceException。
        FloatingTextHelper.SetPlayerLabel(Uid, LiveModeManager.GetDisplayName(Uid), character);
    }

    /// <summary>场景卸载由游戏销毁对象；中途离线由模组移除自己的角色。</summary>
    public void ReleaseCharacter(bool sceneUnloading = false)
    {
        if (!sceneUnloading)
        {
            FloatingTextHelper.RemovePlayerLabel(Uid);
            // 场景卸载时不销毁：游戏自己会清空并销毁角色表里的对象。
            if (handle != null)
                ModRuntime.CommonServices.Characters.DestroyCharacter(handle);
        }
        handle = null;
        character = null;
        owner = null;
        ResetMotion();
    }

    public override void ResetMotion()
    {
        base.ResetMotion();
        positionOffset = Vector2.Zero;
    }

    /// <param name="delta">全局循环 FixedUpdate 的步长，就是原来的 <c>Time.fixedDeltaTime</c>。</param>
    public void OnFixedUpdate(float delta)
    {
        if (!CanRender || character == null || GameFlow.InStory) return;
        if (Scene == Scene.DayScene && DayScene.SceneManager.Instance.IsMapSwapping)
        {
            character.IsMoving = false;
            character.UpdateInputVelocity(new(0f, 0f));
            return;
        }
        var correction = positionOffset / 0.5f / 5f;
        positionOffset -= correction * delta * 5f * character.sprintMultiplier;
        var velocity = new Vector2(InputDirection.X + correction.X, InputDirection.Y + correction.Y);
        if (velocity.SqrMagnitude < 0.0001f) velocity = Vector2.Zero;
        character.IsMoving = velocity != Vector2.Zero;
        character.UpdateInputVelocity(new(velocity.X, velocity.Y));
    }
}
