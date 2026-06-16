using GameData.Core.Collections;
using GameData.Core.Collections.CharacterUtility;
using MetaMystia.Protocol.Enums;
using MetaMystia.UI;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

namespace MetaMystia.Network.Utilities;

public static class EnumConverter
{
    // EvaluationResult
    public static EvaluationResult ToProtocol(GuestGroupController.EvaluationResult result)
        => (EvaluationResult)result;
    public static GuestGroupController.EvaluationResult ToGame(EvaluationResult result)
        => (GuestGroupController.EvaluationResult)result;
    // GuestType
    public static GuestType ToProtocol(GuestsManager.GuestType type)
        => (GuestType)type;
    public static GuestsManager.GuestType ToGame(GuestType type)
        => (GuestsManager.GuestType)type;
    // LeaveType
    public static LeaveType ToProtocol(GuestGroupController.LeaveType type)
        => (LeaveType)type;
    public static GuestGroupController.LeaveType ToGame(LeaveType type)
        => (GuestGroupController.LeaveType)type;
    // MathOperation
    public static MathOperation ToProtocol(EventManager.MathOperation op)
        => (MathOperation)op;
    public static EventManager.MathOperation ToGame(MathOperation op)
        => (EventManager.MathOperation)op;
    // OrderGenerationResult
    public static OrderGenerationResult ToProtocol(GuestsManager.OrderGenerationResult result)
        => (OrderGenerationResult)result;
    public static GuestsManager.OrderGenerationResult ToGame(OrderGenerationResult result)
        => (GuestsManager.OrderGenerationResult)result;
    // OrderType
    public static OrderType ToProtocol(GuestsManager.OrderBase.OrderType type)
        => (OrderType)type;
    public static GuestsManager.OrderBase.OrderType ToGame(OrderType type)
        => (GuestsManager.OrderBase.OrderType)type;
    // Scene
    public static Scene ToProtocol(Common.UI.Scene scene)
        => (Scene)scene;
    public static Common.UI.Scene ToGame(Scene scene)
        => (Common.UI.Scene)scene;
    // SellableType
    public static SellableType ToProtocol(Sellable.SellableType type)
        => (SellableType)type;
    public static Sellable.SellableType ToGame(SellableType type)
        => (Sellable.SellableType)type;
    // ServeType
    public static ServeType ToProtocol(EventManager.ServeType type)
        => (ServeType)type;
    public static EventManager.ServeType ToGame(ServeType type)
        => (EventManager.ServeType)type;
    // SkinSelectedType
    public static SkinSelectedType ToProtocol(CharacterSkinSets.SelectedType type)
        => (SkinSelectedType)(int)type;
    public static CharacterSkinSets.SelectedType ToGame(SkinSelectedType type)
        => (CharacterSkinSets.SelectedType)(int)type;
    // RejectReason <-> L10n TextId
    public static RejectReason ToProtocol(TextId textId)
    {
        return textId switch
        {
            TextId.UnknownError => RejectReason.UnknownError,
            TextId.ModVersionMismatch => RejectReason.ModVersionMismatch,
            TextId.GameVersionMismatch => RejectReason.GameVersionMismatch,
            TextId.GameResourcesNotLoaded => RejectReason.GameResourcesNotLoaded,
            TextId.RoomFull => RejectReason.RoomFull,
            TextId.DuplicatePeerId => RejectReason.DuplicatePeerId,
            TextId.MpPlayerIdInvalid => RejectReason.InvalidPlayerId,
            TextId.PrepWorkReconnectBlocked => RejectReason.PrepWorkConnectNotAllowed,
            _ => RejectReason.UnknownError
        };
    }
    public static TextId ToGame(RejectReason reason)
    {
        return reason switch
        {
            RejectReason.UnknownError => TextId.UnknownError,
            RejectReason.ModVersionMismatch => TextId.ModVersionMismatch,
            RejectReason.GameVersionMismatch => TextId.GameVersionMismatch,
            RejectReason.GameResourcesNotLoaded => TextId.GameResourcesNotLoaded,
            RejectReason.RoomFull => TextId.RoomFull,
            RejectReason.DuplicatePeerId => TextId.DuplicatePeerId,
            RejectReason.InvalidPlayerId => TextId.MpPlayerIdInvalid,
            RejectReason.PrepWorkConnectNotAllowed => TextId.PrepWorkReconnectBlocked,
            _ => TextId.UnknownError
        };
    }
}
