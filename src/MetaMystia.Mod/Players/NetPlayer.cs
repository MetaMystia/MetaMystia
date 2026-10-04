
using System;

using Common.CharacterUtility;

using Mystia.Assets;
using Mystia.Scenes;

using Vector2 = Mystia.Numerics.Vector2;

namespace MetaMystia;

/// <summary>
/// 玩家基类，包含本地玩家和远程对端玩家的公共状态和方法
/// </summary>
/// <remarks>
/// 角色的位置与渲染层级都经框架的角色面读写（<see cref="ICharacterServices"/>），本类不再持有
/// <c>Rigidbody2D</c>/<c>Collider2D</c> 这类引擎组件；<see cref="GetCharacterUnit"/> 仍交给游戏自己的
/// 角色单元用于速度等游戏 API。
/// 模组自己持有的向量状态（<see cref="InputDirection"/>、<see cref="Position"/>）一律是镜像值类型
/// <c>Mystia.Numerics.Vector2</c>（<c>X</c>/<c>Y</c>、<c>Vector2.Zero</c>）；与引擎相接的换算只在读写角色
/// 位置的地方逐分量发生。本文件里仍保留的引擎对象访问（<c>Rigidbody2D</c>／<c>Collider2D</c>／
/// <c>Transform</c> 直取，以及 <see cref="SetZ"/> 写回的引擎向量）保持现状：这几处等框架的
/// 角色/预制体面，本次不动；因此这里也照旧需要 <c>using UnityEngine;</c> 来写出这两个组件类型名。
/// </remarks>
[AutoLog]
public abstract partial class NetPlayer
{
    #region 玩家标识
    /// <summary>
    /// 玩家自定义 ID（显示名）
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// 服务器分配的玩家身份，0 表示尚未连接。
    /// </summary>
    public int Uid { get; set; }

    #endregion


    #region Unity角色组件便捷访问
    // 保留的引擎对象面（Rigidbody2D／Collider2D／.transform 直取）：这几处等框架的角色/预制体面。
    /// <summary>
    /// 获取玩家角色的 CharacterControllerUnit 实例
    /// </summary>
    public abstract CharacterControllerUnit GetCharacterUnit();

    public CharacterControllerUnit unit => GetCharacterUnit();

    /// <summary>
    /// 该玩家角色的框架句柄：本地玩家按 label 现取，远端玩家用创建时拿到的那个；取不到时为 null
    /// （位置与层级随之成为无操作）。
    /// </summary>
    public abstract CharacterHandle? CharacterHandle { get; }
    #endregion


    /// <summary>
    /// 玩家的资源数据库，记录该玩家拥有的 DLC / Mod 资源 ID
    /// </summary>
    public ResourceDataBase DataBase { get; set; } = new();

    /// <summary>
    /// 缓存的增量格式资源数据库。会话期间 DataBase 不变，避免重复 ToIncremental() 计算。
    /// 对 Local: 首次访问时惰性计算；对 Peer: 在接收时直接缓存原始增量数据。
    /// </summary>
    public ResourceDataBase IncrementalDataBase
    {
        get => _incrementalDataBase ??= DataBase.ToIncremental();
        set => _incrementalDataBase = value;
    }
    private ResourceDataBase _incrementalDataBase;

    public void ReloadResourceTable()
    {
        DataBase.LoadResourceIds();
        _incrementalDataBase = null;
    }

    #region 角色状态
    /// <summary>
    /// 当前所在地图（主要用于 `DayScene`）
    /// </summary>
    public MapLabel MapLabel { get; set; } = MapLabel.Unknown;

    /// <summary>
    /// 是否已经结束白天
    /// </summary>
    public bool IsDayOver { get; set; } = false;

    /// <summary>
    /// 是否已经结束准备
    /// </summary>
    public bool IsPrepOver { get; set; } = false;

    /// <summary>
    /// 选择的居酒屋地图（选店阶段）
    /// </summary>
    public MapLabel IzakayaMapLabel { get; set; } = MapLabel.Unknown;

    /// <summary>
    /// 选择的居酒屋等级（选店阶段）
    /// </summary>
    public int IzakayaLevel { get; set; } = 0;

    /// <summary>
    /// 是否正在奔跑
    /// </summary>
    public bool IsSprinting { get; set; } = false;

    /// <summary>
    /// 角色移动速度
    /// </summary>
    public virtual float Speed { get; set; } = 1f;

    /// <summary>
    /// 皮肤
    /// </summary>
    public PlayerSkin Skin { get; set; } = new();

    public void UpdateCharacterSprite() => Skin?.ApplyToUnit(unit);

    /// <summary>
    /// 输入方向向量（镜像值类型，<c>X</c>/<c>Y</c>）
    /// </summary>
    public Vector2 InputDirection { get; set; } = Vector2.Zero;

    /// <summary>玩家角色当前位置（镜像值类型）；句柄无效时为零向量。</summary>
    public Vector2 Position =>
        CharacterHandle is { } handle
        && ModRuntime.CommonServices.Characters.TryGetCharacterPosition(handle, out var at)
            ? at
            : Vector2.Zero;
    #endregion

    /// <summary>
    /// 设置角色的 Z 轴位置（用于控制渲染层级）
    /// </summary>
    /// <param name="z"></param>
    public void SetZ(int z)
    {
        if (CharacterHandle is { } handle)
            ModRuntime.CommonServices.Characters.SetCharacterZ(handle, z);
    }

    /// <summary>
    /// 重置同步状态标志（DayOver、PrepOver、IzakayaSelection 等）
    /// </summary>
    public virtual void ResetState()
    {
        MapLabel = MapLabel.Unknown;
        IsDayOver = false;
        IsPrepOver = false;
        IzakayaMapLabel = MapLabel.Unknown;
        IzakayaLevel = 0;
    }

    /// <summary>
    /// 重置运动相关状态
    /// </summary>
    public virtual void ResetMotion()
    {
        IsSprinting = false;
        InputDirection = Vector2.Zero;
    }
}
