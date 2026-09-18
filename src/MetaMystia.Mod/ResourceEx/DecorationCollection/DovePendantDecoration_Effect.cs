using GameData.RunTime.Common;
using NightScene.EventUtility;

using SgrYuki;

namespace MetaMystia;

public static partial class ResourceExManager
{
    /// <summary>
    /// 白天移动速度加成数值。
    /// </summary>
    private const float DovePendantDaySpeedBonus = 0.12f;

    /// <summary>
    /// 夜间效果倍率。
    /// </summary>
    private const float DovePendantNightSpeedFactor = 1.2f;

    /// <summary>
    /// 等待玩家角色就绪的最大帧数。
    /// </summary>
    private const int PlayerUnitReadyMaxWaitFrames = 900;

    /// <summary>
    /// 装备且在白天时应用白天移速加成。
    /// </summary>
    public static void ApplyDovePendantDaytimeSpeed()
    {
        if (PlayerManager.LocalIsDayOver)
        {
            return;
        }
        if (!RunTimeAlbum.HasDecorationUsing(DovePendantDecorationId))
        {
            return;
        }
        ApplyDaytimeSpeed(RunTimePlayerData.LevelProfile.MoveSpeedMultiplier + DovePendantDaySpeedBonus);
    }

    /// <summary>
    /// 卸下装饰时撤销白天移速加成。
    /// </summary>
    public static void RemoveDovePendantDaytimeSpeed()
    {
        if (RunTimeAlbum.HasDecorationUsing(DovePendantDecorationId))
        {
            return;
        }
        ApplyDaytimeSpeed(RunTimePlayerData.LevelProfile.MoveSpeedMultiplier);
    }

    /// <summary>
    /// 进入夜晚时撤销白天移速加成。
    /// </summary>
    public static void ClearDovePendantDaytimeSpeed()
    {
        ApplyDaytimeSpeed(RunTimePlayerData.LevelProfile.MoveSpeedMultiplier);
    }

    /// <summary>
    /// 进入夜晚时应用小鸽子挂坠夜间效果。
    /// </summary>
    /// <param name="eventManager">夜间事件管理器实例。</param>
    public static void ApplyDovePendantNightBuffs(EventManager eventManager)
    {
        if (eventManager == null) return;
        ApplyNightPlayerSpeed();
        eventManager.RemovePartnerExtraWorkSpeed(DovePendantNightSpeedFactor, false);
        eventManager.SetPartnerExtraWorkSpeed(DovePendantNightSpeedFactor, false);
        eventManager.RemovePartnerExtraMoveSpeed(DovePendantNightSpeedFactor, false);
        eventManager.SetPartnerExtraMoveSpeed(DovePendantNightSpeedFactor, false);
        Log.Info("[DovePendant] 已施加夜间效果：玩家移速+20%，伙伴效率+20%、移速+20%");
    }

    /// <summary>
    /// 卸下装饰或夜场结束时撤销夜间伙伴效果。
    /// </summary>
    public static void RemoveDovePendantNightBuffs()
    {
        var eventManager = EventManager.Instance;
        if (eventManager == null) return;
        eventManager.RemovePartnerExtraWorkSpeed(DovePendantNightSpeedFactor, false);
        eventManager.RemovePartnerExtraMoveSpeed(DovePendantNightSpeedFactor, false);
        Log.Info("[DovePendant] 已撤销夜间伙伴效果");
    }

    /// <summary>
    /// 应用夜间玩家移速。
    /// </summary>
    private static void ApplyNightPlayerSpeed()
    {
        var target = RunTimePlayerData.LevelProfile.MoveSpeedMultiplier * DovePendantNightSpeedFactor;
        if (PlayerManager.Local.unit != null)
        {
            PlayerManager.Local.Speed = target;
            Log.Info($"[DovePendant] 已应用夜间玩家移速（目标={target}）");
            return;
        }
        PluginHost.Instance?.StartManagedCoroutine(SetPlayerSpeedWhenReady(target));
    }

    /// <summary>
    /// 应用白天玩家移速。
    /// </summary>
    /// <param name="targetSpeed">目标移速。</param>
    private static void ApplyDaytimeSpeed(float targetSpeed)
    {
        if (PlayerManager.Local.unit != null)
        {
            PlayerManager.Local.Speed = targetSpeed;
            Log.Info($"[DovePendant] 已应用白天移速（目标={targetSpeed}，当前={PlayerManager.Local.Speed}）");
            return;
        }
        PluginHost.Instance?.StartManagedCoroutine(SetPlayerSpeedWhenReady(targetSpeed));
    }

    /// <summary>
    /// 等待本地玩家角色就绪后设置移速。
    /// </summary>
    /// <param name="targetSpeed">目标移速。</param>
    private static System.Collections.IEnumerator SetPlayerSpeedWhenReady(float targetSpeed)
    {
        for (int i = 0; i < PlayerUnitReadyMaxWaitFrames; i++)
        {
            if (PlayerManager.Local.unit != null)
            {
                PlayerManager.Local.Speed = targetSpeed;
                Log.Info($"[DovePendant] 已应用移速（目标={targetSpeed}，当前={PlayerManager.Local.Speed}）");
                yield break;
            }
            yield return null;
        }
        Log.Warning($"[DovePendant] 等待玩家角色就绪超时，放弃应用移速（目标={targetSpeed}）");
    }
}
