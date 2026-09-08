using UnityEngine;

using Common.CharacterUtility;

using MetaMystia.Network;

namespace MetaMystia;

// UI 和已有 Hook 的只读组合视图。身份、玩法状态与 Unity 对象各有自己的拥有者。
public abstract class NetPlayer
{
    public abstract int Uid { get; }
    public string Id => MpWire.Session.Players.TryGetValue(Uid, out var profile) ? profile.Name : MpManager.PlayerId;
    public abstract PlayerSkin Skin { get; }
    public abstract ResourceDataBase DataBase { get; }
    public abstract MapLabel MapLabel { get; }
    public bool IsDayOver => RoomGameplay.IsPlayerReady(Uid, GameplayPhase.Day);
    public bool IsPrepOver => RoomGameplay.IsPlayerReady(Uid, GameplayPhase.Prep);
    public MapLabel IzakayaMapLabel => RoomGameplay.Selection(Uid)?.Map ?? MapLabel.Unknown;
    public int IzakayaLevel => RoomGameplay.Selection(Uid)?.Level ?? 0;
    public abstract float Speed { get; }
    public abstract bool IsSprinting { get; }
    public abstract Vector2 InputDirection { get; }
    public abstract CharacterControllerUnit GetCharacterUnit();
    public CharacterControllerUnit unit => GetCharacterUnit();
    public Rigidbody2D rb2d => unit?.rb2d;
    public Collider2D cl2d => unit?.cl2d;
    public virtual Vector2 Position => rb2d == null ? Vector2.zero : rb2d.position;
    public void UpdateCharacterSprite() => Skin?.ApplyToUnit(unit);
    public void SetZ(int z)
    {
        if (rb2d == null) return;
        var position = rb2d.transform.position;
        rb2d.transform.position = new Vector3(position.x, position.y, z);
    }
}
