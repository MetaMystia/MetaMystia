using System;

using Common.UI;
using GameData.Core.Collections;
using GameData.Core.Collections.CharacterUtility;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

namespace MetaMystia.Network;

/// <summary>
/// 游戏 il2cpp 枚举 ↔ 协议层 Wire* 枚举 的边界互转（行为侧，仅 mod 编译）。
/// 唯一同时认识两种类型的地方；服务端只见 Wire* 枚举。
/// 全部映射按 (int) 值直转，并在静态构造里断言数值对齐——游戏更新若重排成员，将在启动时立即报错。
/// </summary>
internal static class WireEnumMaps
{
    public static WireScene ToWire(this Scene v) => (WireScene)(int)v;
    public static Scene ToGame(this WireScene v) => (Scene)(int)v;

    public static WireSkinType ToWire(this CharacterSkinSets.SelectedType v) => (WireSkinType)(int)v;
    public static CharacterSkinSets.SelectedType ToGameSkinType(this WireSkinType v) => (CharacterSkinSets.SelectedType)(int)v;

    public static WireSellableType ToWire(this Sellable.SellableType v) => (WireSellableType)(int)v;
    public static Sellable.SellableType ToGameSellableType(this WireSellableType v) => (Sellable.SellableType)(int)v;

    public static WireMathOperation ToWire(this EventManager.MathOperation v) => (WireMathOperation)(int)v;
    public static EventManager.MathOperation ToGameMathOperation(this WireMathOperation v) => (EventManager.MathOperation)(int)v;

    public static WireServeType ToWire(this EventManager.ServeType v) => (WireServeType)(int)v;
    public static EventManager.ServeType ToGameServeType(this WireServeType v) => (EventManager.ServeType)(int)v;

    public static WireLeaveType ToWire(this GuestGroupController.LeaveType v) => (WireLeaveType)(int)v;
    public static GuestGroupController.LeaveType ToGameLeaveType(this WireLeaveType v) => (GuestGroupController.LeaveType)(int)v;

    public static WireEvaluationResult ToWire(this GuestGroupController.EvaluationResult v) => (WireEvaluationResult)(int)v;
    public static GuestGroupController.EvaluationResult ToGameEvaluationResult(this WireEvaluationResult v) => (GuestGroupController.EvaluationResult)(int)v;

    public static WireGuestType ToWire(this GuestsManager.GuestType v) => (WireGuestType)(int)v;
    public static GuestsManager.GuestType ToGameGuestType(this WireGuestType v) => (GuestsManager.GuestType)(int)v;

    public static WireOrderGenerationResult ToWire(this GuestsManager.OrderGenerationResult v) => (WireOrderGenerationResult)(int)v;
    public static GuestsManager.OrderGenerationResult ToGameOrderGenerationResult(this WireOrderGenerationResult v) => (GuestsManager.OrderGenerationResult)(int)v;

    public static WireOrderType ToWire(this GuestsManager.OrderBase.OrderType v) => (WireOrderType)(int)v;
    public static GuestsManager.OrderBase.OrderType ToGameOrderType(this WireOrderType v) => (GuestsManager.OrderBase.OrderType)(int)v;

    /// <summary>断言每个 Wire* 枚举与对应游戏枚举数值逐一对齐。任一不符即抛出，标明漂移的枚举。</summary>
    public static void AssertAligned()
    {
        AssertEnum<Scene, WireScene>();
        AssertEnum<CharacterSkinSets.SelectedType, WireSkinType>();
        AssertEnum<Sellable.SellableType, WireSellableType>();
        AssertEnum<EventManager.MathOperation, WireMathOperation>();
        AssertEnum<EventManager.ServeType, WireServeType>();
        AssertEnum<GuestGroupController.LeaveType, WireLeaveType>();
        AssertEnum<GuestGroupController.EvaluationResult, WireEvaluationResult>();
        AssertEnum<GuestsManager.GuestType, WireGuestType>();
        AssertEnum<GuestsManager.OrderGenerationResult, WireOrderGenerationResult>();
        AssertEnum<GuestsManager.OrderBase.OrderType, WireOrderType>();
    }

    private static void AssertEnum<TGame, TWire>()
        where TGame : struct, Enum
        where TWire : struct, Enum
    {
        var gameNames = Enum.GetNames(typeof(TGame));
        var wireNames = Enum.GetNames(typeof(TWire));
        if (gameNames.Length != wireNames.Length)
            throw new InvalidOperationException(
                $"Wire enum {typeof(TWire).Name} has {wireNames.Length} members but game {typeof(TGame).Name} has {gameNames.Length}");

        foreach (var name in wireNames)
        {
            if (!Enum.TryParse<TGame>(name, out var gameVal))
                throw new InvalidOperationException(
                    $"Wire enum {typeof(TWire).Name}.{name} has no counterpart in game {typeof(TGame).Name}");
            int wireVal = (int)(object)Enum.Parse<TWire>(name);
            if ((int)(object)gameVal != wireVal)
                throw new InvalidOperationException(
                    $"Wire enum {typeof(TWire).Name}.{name}={wireVal} misaligned with game {typeof(TGame).Name}.{name}={(int)(object)gameVal}");
        }
    }
}
