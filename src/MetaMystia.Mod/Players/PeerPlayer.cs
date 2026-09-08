using UnityEngine;

using Common.CharacterUtility;
using DayScene.Interactables.Collections.ConditionComponents;
using GameData.RunTime.DaySceneUtility;

namespace MetaMystia;

// 只拥有当前角色的皮肤缓存、动画和插值；收到的数据保存在 ModPlayerStore。
public sealed class PeerPlayer : NetPlayer
{
    public override int Uid { get; }
    public string CharacterId => $"MetaMystia_{Uid}";
    private AppearanceSnapshot _appearance;
    private PlayerSkin _skin = new();
    private MotionSnapshot _appliedMotion;
    private Vector2 _actualVelocity;
    private Vector2 _offset;
    private ModPlayerStore.PlayerData Data => ModPlayerStore.Get(Uid);
    public override MapLabel MapLabel => Data?.Presence.Map ?? MapLabel.Unknown;
    public override ResourceDataBase DataBase => Data?.Resources;
    public override float Speed => _appliedMotion?.Speed ?? 1;
    public override bool IsSprinting => _appliedMotion?.Sprinting ?? false;
    public override Vector2 InputDirection => _actualVelocity;
    public override Vector2 Position
    {
        get
        {
            var motion = Data?.Presence.Scene == Common.UI.Scene.WorkScene ? Data.NightMotion : Data?.Presence.Motion;
            return motion == null ? Vector2.zero : new Vector2(motion.X, motion.Y);
        }
    }
    public override PlayerSkin Skin
    {
        get
        {
            var appearance = Data?.Appearance;
            if (appearance == null || appearance == _appearance) return _skin;
            _appearance = appearance;
            _skin = new PlayerSkin
            {
                CharacterId = appearance.CharacterId, SelectedType = appearance.SelectedType, SkinIndex = appearance.SkinIndex,
                NetSkinName = appearance.NetSkinName, RotateOverride = appearance.RotateOverride
            };
            return _skin;
        }
    }

    public PeerPlayer(int uid) => Uid = uid;
    public override CharacterControllerUnit GetCharacterUnit()
    {
        var director = Common.SceneDirector.Instance;
        return director != null && director.characterCollection.TryGetValue(CharacterId, out var character) ? character : null;
    }
    public CharacterConditionComponent GetCharacterComponent() => unit?.GetComponent<CharacterConditionComponent>();

    internal void Spawn(MotionSnapshot motion)
    {
        var director = Common.SceneDirector.Instance;
        if (director == null || unit != null) return;
        director.SpawnCharacter(Common.SceneDirector.Identity.Special, 14, new Vector2(motion.X, motion.Y), CharacterId);
        if (unit == null || rb2d == null || cl2d == null) return;
        _appliedMotion = null;
        _offset = Vector2.zero;
        if (unit.GetComponent<HeightBlendedInputProcessorComponent>() == null)
            unit.AddInputProcessor<HeightBlendedInputProcessorComponent>();
        var height = unit.GetComponent<HeightBlendedInputProcessorComponent>();
        if (MpManager.LocalScene == Common.UI.Scene.DayScene) height.Initialize(DayScene.SceneManager.Instance.CurrentActiveMap.height);
        else height.Initialize(NightScene.MapManager.Instance.height);
        IgnoreCollisionWithSelf();
        UpdateCharacterSprite();
        UI.FloatingTextHelper.SetPlayerLabel(Uid, LiveModeManager.GetDisplayName(Uid), unit.transform);
    }

    internal void DespawnCharacter()
    {
        var collection = Common.SceneDirector.Instance?.characterCollection;
        if (collection != null && collection.TryGetValue(CharacterId, out var existing))
        {
            collection.Remove(CharacterId);
            if (existing != null) Object.Destroy(existing.gameObject);
        }
        UI.FloatingTextHelper.RemovePlayerLabel(Uid);
        _appliedMotion = null;
        _offset = Vector2.zero;
        _actualVelocity = Vector2.zero;
    }

    public void IgnoreCollisionWithSelf(bool ignore = true)
    {
        if (cl2d == null) return;
        if (PlayerManager.Local.cl2d != null) Physics2D.IgnoreCollision(cl2d, PlayerManager.Local.cl2d, ignore);
        foreach (var peer in PlayerPresentation.Avatars.Values)
            if (peer != this && peer.cl2d != null) Physics2D.IgnoreCollision(cl2d, peer.cl2d, ignore);
    }

    internal void Apply(MotionSnapshot motion)
    {
        if (unit == null || rb2d == null || ReferenceEquals(motion, _appliedMotion)) return;
        bool first = _appliedMotion == null;
        _appliedMotion = motion;
        _actualVelocity = new Vector2(motion.Vx, motion.Vy);
        unit.MoveSpeedMultiplier = motion.Speed;
        unit.sprintMultiplier = motion.Sprinting ? 1.5f : 1;
        unit.IsMoving = _actualVelocity.sqrMagnitude > 0;
        _offset = new Vector2(motion.X, motion.Y) - rb2d.position;
        if (first || _offset.magnitude > 3)
        {
            rb2d.transform.position = new Vector3(motion.X, motion.Y, rb2d.transform.position.z);
            _offset = Vector2.zero;
        }
    }

    internal void OnFixedUpdate()
    {
        if (unit == null || rb2d == null || _appliedMotion == null) return;
        var correction = _offset / 0.5f / 5;
        _offset -= correction * Time.fixedDeltaTime * 5 * unit.sprintMultiplier;
        var velocity = _actualVelocity + correction;
        unit.IsMoving = velocity.magnitude >= 0.01f;
        if (unit.IsMoving) unit.UpdateInputVelocity(velocity);
        if (MpManager.LocalScene == Common.UI.Scene.DayScene)
        {
            var tracked = RunTimeDayScene.GetTrackedNPC(CharacterId);
            if (tracked?.overridePosition != null)
                tracked.overridePosition.position = new Il2CppSystem.Collections.Generic.KeyValuePair<float, float>(rb2d.position.x, rb2d.position.y);
        }
    }
}
