using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;

using GameData.Core.Collections;
using GameData.RunTime.Common;
using NightScene.CookingUtility;
using NightScene.EventUtility;

using MetaMystia.ResourceEx.Registries;

namespace MetaMystia.ResourceEx.DecorationCollection;

/// <summary>
/// 达摩雪雪：营业时做出黑暗料理后获得 30 秒「不倒翁七転八起」，烹饪有 20% 概率返还食材、烹饪时间减少 20%。
/// 原版所有黑暗料理来源（QTE 失败、厨具、伙伴、符卡覆写）都经过 OnResultCompleteCallback。
/// </summary>
public sealed class DarumaYukiyukiDecoration : DecorationBaseEx, IDecorationDependencies
{
    // 占位 ID，正式 ID 分配后替换。
    private const EventManager.BuffType Buff = (EventManager.BuffType)8997;
    private const int Duration = 30;
    private const float FreeCookRate = 0.2f;
    private const float CookTimeMultiplier = 0.8f;

    [HideFromIl2Cpp]
    public static string CheckDependencies() =>
        BuffRegistry.IsAvailable((int)Buff) ? null : "不倒翁七転八起 buff 未声明或资源缺失";

    public override void DecorationBuffEnterNight(EventManager eventManager)
    {
        CookSystemManager.Instance.OnResultCompleteCallback += DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Sellable>>((Sellable food) =>
        {
            if (food.Type == Sellable.SellableType.Food && food.Id == RunTimeStorage.DARK_MATTER_ID)
                Trigger(eventManager);
        });
    }

    [HideFromIl2Cpp]
    private static void Trigger(EventManager eventManager)
    {
        // 原版 Additive 覆盖：结束旧 buff 后按新旧剩余时间较长者重新登记，即刷新而不叠加。
        eventManager.TryOverrideTimedBuff(Buff, Duration,
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action<int>>((int duration) => Register(eventManager, duration)),
            EventManager.BuffTimeType.OtherPositiveBuff);
    }

    [HideFromIl2Cpp]
    private static void Register(EventManager eventManager, int duration)
    {
        eventManager.registeredFreeCookRate.Add(FreeCookRate);
        eventManager.SetExtraCookSpeedFunc(
            DelegateSupport.ConvertDelegate<Il2CppSystem.Func<float>>(() => CookTimeMultiplier),
            duration, out _, Buff,
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(() => eventManager.registeredFreeCookRate.Remove(FreeCookRate)),
            DelegateSupport.ConvertDelegate<Il2CppSystem.Func<int, string, string>>(
                (int seconds, string description) => description.Replace("$a", seconds.ToString())));
    }
}
