using Common.UI;

namespace MetaMystia;

public static partial class GameFlow
{
    // 场景转换只判定玩法入口；读档、回菜单和回退在存档重置入口提前断开。
    internal static bool CanKeepConnection(Scene source, Scene target, DayDestination destination,
        bool enteringTrial, bool returningFromTrial)
    {
        bool cooperative = destination != DayDestination.None;
        bool finalTrial = destination is DayDestination.FinalTrial or DayDestination.FinalTrialAgain;
        return target switch
        {
            Scene.WorkScene => (source == Scene.IzakayaPrepScene && destination == DayDestination.Business)
                || (source == Scene.DayScene && finalTrial && enteringTrial),
            Scene.IzakayaPrepScene => source == Scene.DayScene && destination == DayDestination.Business,
            Scene.ResultScene => !cooperative || source == Scene.WorkScene,
            Scene.DayScene => !cooperative || source == Scene.ResultScene
                || (source == Scene.WorkScene && finalTrial && returningFromTrial),
            _ => !cooperative,
        };
    }
}
