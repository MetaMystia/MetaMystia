using GameData.Profile;
using NightScene.EventUtility;

namespace MetaMystia.ResourceEx.DecorationCollection;

/// <summary>
/// 小鸽子挂坠装饰的效果载体，实现 DecorationBase 夜间钩子占位。
/// </summary>
public class DovePendantDecoration : DecorationBase
{
    /// <summary>
    /// 在夜间营业开始时登记小鸽子挂坠装饰的占位钩子，确保装饰可注册且昼夜不抛异常。
    /// </summary>
    /// <param name="eventManager">夜场营业开始时传入的全局事件管理器。</param>
    public override void DecorationBuffEnterNight(EventManager eventManager)
    {
        MetaMystia.ResourceExManager.ApplyDovePendantNightBuffs(eventManager);
    }
}
