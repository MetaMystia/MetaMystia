using UnityEngine;

using Common;
using Common.CharacterUtility;
using Common.UI;
using GameData.Core.Collections.CharacterUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Network;
using MetaMystia.UI;

namespace MetaMystia;

/// <summary>远端玩家的最新状态，以及当前场景中由模组创建的角色。</summary>
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
        DataBase = ResourceDataBase.Expand(resources);
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
            var go = Object.Instantiate(DataBaseCharacter.CharacterBase, owner.transform);
            go.name = CharacterId;
            character = go.GetComponent<CharacterControllerUnit>();
            // 剧情用的 SpawnCharacter 会删除碰撞体，联机角色需保留碰撞体。
            character.Initialize(Skin.ResolveSkin(), motion.Speed, true);
            owner.characterCollection.Add(CharacterId, character);
            character.AddInputProcessor<HeightBlendedInputProcessorComponent>();
            Skin.ApplyToUnit(character);
            IgnorePlayerCollisions();
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
            positionOffset = new Vector2(motion.X, motion.Y) - character.rb2d.position;
            if (created || positionOffset.sqrMagnitude > 9f)
            {
                character.rb2d.position = new(motion.X, motion.Y);
                positionOffset = Vector2.zero;
            }
        }
        bool visible = Scene == Scene.WorkScene || IsSameMapAsLocal;
        SetZ(visible ? 0 : -40815);
        character.cl2d.enabled = visible;
        FloatingTextHelper.SetPlayerLabel(Uid, LiveModeManager.GetDisplayName(Uid), character.transform);
    }

    private void IgnorePlayerCollisions()
    {
        var self = PlayerManager.Local.unit;
        if (self?.cl2d != null) Physics2D.IgnoreCollision(character.cl2d, self.cl2d);
        foreach (var peer in PlayerManager.Peers.Values)
            if (peer != this && peer.character != null && peer.character.cl2d != null)
                Physics2D.IgnoreCollision(character.cl2d, peer.character.cl2d);
        foreach (var peer in PlayerManager.PublicPeers.Values)
            if (peer != this && peer.character != null && peer.character.cl2d != null)
                Physics2D.IgnoreCollision(character.cl2d, peer.character.cl2d);
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
                Object.Destroy(character.gameObject);
            }
        }
        character = null;
        owner = null;
        ResetMotion();
    }

    public override void ResetMotion()
    {
        base.ResetMotion();
        positionOffset = Vector2.zero;
    }

    public void OnFixedUpdate()
    {
        if (!CanRender || character == null || GameFlow.InStory) return;
        if (Scene == Scene.DayScene && DayScene.SceneManager.Instance.IsMapSwapping)
        {
            character.IsMoving = false;
            character.UpdateInputVelocity(Vector2.zero);
            return;
        }
        var correction = positionOffset / 0.5f / 5f;
        positionOffset -= correction * Time.fixedDeltaTime * 5f * character.sprintMultiplier;
        var velocity = InputDirection + correction;
        if (velocity.sqrMagnitude < 0.0001f) velocity = Vector2.zero;
        character.IsMoving = velocity != Vector2.zero;
        character.UpdateInputVelocity(velocity);
    }
}
