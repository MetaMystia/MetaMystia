using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;

using GameData.Core.Collections;
using NightScene.CookingUtility;
using NightScene.EventUtility;

using MetaMystia.ResourceEx.Models;
using MetaMystia.ResourceEx.Registries;

namespace MetaMystia.ResourceEx.DecorationCollection;

public class DarumaYukiyukiDecoration : DecorationBaseEx, IDecorationDependencies
{
    private const int BuffSeconds = 30;
    private const float FreeCookRate = 0.2f;

    private CookSystemManager _cookManager;
    private Il2CppSystem.Action<Sellable> _onResultComplete;
    private Il2CppSystem.Action _onBuffEnd;
    private Il2CppSystem.Func<float> _cookTimeMultiplier;
    private Il2CppSystem.Func<int, string, string> _buffDescription;

    [HideFromIl2Cpp]
    public static string CheckDependencies(DecorationConfig config) =>
        config.buffId is int id && BuffRegistry.IsAvailable(id)
            ? null
            : "缺少 buffId 或对应 buff 的名称、说明、图标";

    public override void DecorationBuffEnterNight(EventManager eventManager)
    {
        if (_cookManager != null && _onResultComplete != null)
            _cookManager.OnResultCompleteCallback -= _onResultComplete;

        _cookManager = CookSystemManager.Instance;
        _cookTimeMultiplier = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<float>>(() => 0.8f);
        _buffDescription = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<int, string, string>>(
            (int seconds, string description) => description.Replace("$c", seconds.ToString()));
        _onResultComplete = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Sellable>>((Sellable food) =>
        {
            if (food.Type == Sellable.SellableType.Food && food.Id == -1)
                RefreshBuff(eventManager);
        });
        _cookManager.OnResultCompleteCallback += _onResultComplete;
    }

    [HideFromIl2Cpp]
    private void RefreshBuff(EventManager eventManager)
    {
        var buff = (EventManager.BuffType)Config.buffId.Value;
        // 先结束旧效果再登记 30 秒，沿用原版暂停、驱散及结束回调，不累计时长或倍率。
        eventManager.RemoveAllRegisteredTimedBuff(buff);
        eventManager.registeredFreeCookRate.Add(FreeCookRate);
        _onBuffEnd = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(() =>
        {
            eventManager.registeredFreeCookRate.Remove(FreeCookRate);
        });
        eventManager.SetExtraCookSpeedFunc(_cookTimeMultiplier, BuffSeconds, out _, buff, _onBuffEnd, _buffDescription);
    }
}
