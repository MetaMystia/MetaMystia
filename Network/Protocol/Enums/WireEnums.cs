namespace MetaMystia.Network;

// 游戏 il2cpp 枚举的线协议镜像。数值必须与游戏枚举逐一对齐（见 Behaviors/WireEnumMaps.cs 的静态断言）。
// 来源游戏版本：4.4.0e。游戏更新若重排成员，须同步修订此处并复核断言。
// 协议层零游戏依赖：服务端/MockClient 仅认识 Wire* 枚举；mod 在收发边界与游戏枚举互转。

/// <summary>镜像 <c>Common.UI.Scene</c>。</summary>
public enum WireScene
{
    DayScene,
    MainScene,
    LoadScene,
    IzakayaPrepScene,
    WorkScene,
    ResultScene,
    StaffScene,
    EmptyScene,
}

/// <summary>镜像 <c>CharacterSkinSets.SelectedType</c>。</summary>
public enum WireSkinType
{
    Default,
    Explicit,
    DLC,
}

/// <summary>镜像 <c>Sellable.SellableType</c>。</summary>
public enum WireSellableType
{
    Food,
    Beverage,
}

/// <summary>镜像 <c>EventManager.MathOperation</c>。</summary>
public enum WireMathOperation
{
    Add,
    Multiply,
    Set,
}

/// <summary>镜像 <c>EventManager.ServeType</c>。</summary>
public enum WireServeType
{
    Player,
    Boss,
}

/// <summary>镜像 <c>GuestGroupController.LeaveType</c>。</summary>
public enum WireLeaveType
{
    Move,
    Fading,
    Delete,
    MoveToTargetPosition,
}

/// <summary>镜像 <c>GuestGroupController.EvaluationResult</c>。</summary>
public enum WireEvaluationResult
{
    Exbad,
    Bad,
    Normal,
    Good,
    ExGood,
    Null,
}

/// <summary>镜像 <c>GuestsManager.GuestType</c>。</summary>
public enum WireGuestType
{
    Normal,
    Special,
}

/// <summary>镜像 <c>GuestsManager.OrderGenerationResult</c>（游戏侧为 private，interop 提升为 public）。</summary>
public enum WireOrderGenerationResult
{
    Succeed,
    OrderCountDepleted,
    NoMoney,
    ExceedEndurance,
    NotContinue,
}

/// <summary>镜像 <c>GuestsManager.OrderBase.OrderType</c>。</summary>
public enum WireOrderType
{
    Normal,
    Special,
}
