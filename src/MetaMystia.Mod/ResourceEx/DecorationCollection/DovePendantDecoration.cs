using System;

using GameData.Profile;
using NightScene.EventUtility;

namespace MetaMystia.ResourceEx.DecorationCollection;

/// <summary>
/// 小鸽子挂坠装饰的效果载体，在夜间营业开始钩子中施加夜间效果。
/// </summary>
[AutoLog]
public partial class DovePendantDecoration : DecorationBase
{
    /// <summary>
    /// 在夜间营业开始时施加小鸽子挂坠的夜间效果。
    /// </summary>
    /// <param name="eventManager">夜场营业开始时传入的全局事件管理器。</param>
    public override void DecorationBuffEnterNight(EventManager eventManager)
    {
        try
        {
            MetaMystia.ResourceExManager.ApplyDovePendantNightBuffs(eventManager);
        }
        catch (Exception ex)
        {
            Log.Error($"[DovePendant] 夜间效果施加异常: {ex}");
        }
    }
}
