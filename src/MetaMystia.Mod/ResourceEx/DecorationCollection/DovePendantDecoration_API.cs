using System;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using NightScene.EventUtility;

using GameData.Core.Collections;
using GameData.CoreLanguage;
using GameData.CoreLanguage.Collections;
using GameData.Profile;
using GameData.RunTime.Common;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.DecorationCollection;

using SgrYuki;

namespace MetaMystia;

public static partial class ResourceExManager
{
    /// <summary>
    /// 小鸽子挂坠的装饰 id；落在 9000+ 段以避让原生占用区（原生 8 个装饰为低值 id，符卡已占用 9001-9005）。
    /// internal 以便 Patch 引用。
    /// </summary>
    internal const int DovePendantDecorationId = 9006;

    /// <summary>
    /// 小鸽子挂坠在展示柜中显示的图标占位路径；正式美术资源待补，先以占位 Sprite 跑通注册链路。
    /// </summary>
    private const string DovePendantSpriteUri = "rex://ResourceExample/assets/Decoration/9006.png";

    /// <summary>
    /// 白天移动速度加成数值：装备后本地玩家移动速度 +0.12（叠加于基础移速之上）。
    /// </summary>
    private const float DovePendantDaySpeedBonus = 0.12f;

    /// <summary>
    /// 夜间效果倍率：玩家与伙伴移速、伙伴工作效率均为基础值 × 1.2（即 +20%）。
    /// </summary>
    private const float DovePendantNightSpeedFactor = 1.2f;

    /// <summary>
    /// il2cpp 类型注入幂等标记：同一进程生命周期内只注入一次，重复注入会抛异常。
    /// </summary>
    private static bool _dovePendantTypeInjected;

    /// <summary>
    /// 注册小鸽子挂坠装饰，使其出现在展示柜并被勾选生效。
    /// 同一 Decoration 实例须同时写入 Items 与 Decorations 两字典，以满足展示柜列举与 IsDecoration/RefDecorations 的要求。
    /// </summary>
    public static void RegisterDovePendantDecoration()
    {
        if (!_dovePendantTypeInjected)
        {
            ClassInjector.RegisterTypeInIl2Cpp<DovePendantDecoration>();
            _dovePendantTypeInjected = true;
        }

        var specialBuff = ScriptableObject.CreateInstance<DovePendantDecoration>();
        RexAssetRegistry.TryGetSprite(DovePendantSpriteUri, out var overrideSprite);
        if (overrideSprite == null)
        {
            Log.Warning($"[DovePendant] 图标资源加载失败，使用空占位：{DovePendantSpriteUri}");
        }

        var decoration = new Decoration(
            DovePendantDecorationId,
            overrideSprite,
            specialBuff,
            Decoration.DecorationType.Outdoor,
            System.Array.Empty<int>());

        DataBaseCore.Items[DovePendantDecorationId] = decoration;
        DataBaseCore.Decorations[DovePendantDecorationId] = decoration;

        Log.Info($"[DovePendant] 已注册小鸽子挂坠（id={DovePendantDecorationId}）");
    }

    /// <summary>
    /// 应用小鸽子挂坠的白天移速加成：装备且在白天时，本地玩家移动速度 = 基础移速 + 0.12（绝对值设定，幂等可重复调用）。
    /// 设计为响应式：装饰在白天任意时刻被装备都会触发（见 DovePendantDecorationDayScenePatch 对 RunTimeAlbum.TryRecordUsedDecoration 的 Hook）；
    /// 角色未就绪时延迟到 unit 可用后再施加，避免被初始化基准值覆盖。
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
    /// 卸下小鸽子挂坠时撤销白天移速加成，将本地玩家移速恢复为基础值（幂等）。
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
    /// 进入夜晚时清掉白天移速加成，将本地玩家移速恢复为基础值，无视装饰当前是否仍装备。
    /// </summary>
    public static void ClearDovePendantDaytimeSpeed()
    {
        ApplyDaytimeSpeed(RunTimePlayerData.LevelProfile.MoveSpeedMultiplier);
    }

    /// <summary>
    /// 进入夜晚时施加小鸽子挂坠夜间效果：玩家与全体伙伴移速、伙伴工作效率 +20%（基础值 × 1.2）。
    /// 由 DecorationBuffEnterNight 在夜间开业时调用；装饰在夜间被装备时也经响应式钩子触发。
    /// </summary>
    /// <param name="eventManager">夜间事件管理器实例（SceneManager 传入）。</param>
    public static void ApplyDovePendantNightBuffs(NightScene.EventUtility.EventManager eventManager)
    {
        if (eventManager == null) return;
        try
        {
            ApplyNightPlayerSpeed();
            // 先 Remove 再 Set 保证幂等：夜间重入不会叠加成 1.2×1.2。
            eventManager.RemovePartnerExtraWorkSpeed(DovePendantNightSpeedFactor, false);
            eventManager.SetPartnerExtraWorkSpeed(DovePendantNightSpeedFactor, false);
            eventManager.RemovePartnerExtraMoveSpeed(DovePendantNightSpeedFactor, false);
            eventManager.SetPartnerExtraMoveSpeed(DovePendantNightSpeedFactor, false);
            Log.Info("[DovePendant] 已施加夜间效果：玩家移速+20%，伙伴效率+20%、移速+20%");
        }
        catch (Exception ex)
        {
            Log.Error($"[DovePendant] 夜间效果施加异常: {ex}");
        }
    }

    /// <summary>
    /// 卸下装饰或夜场结束时撤销夜间效果（伙伴 extra 需显式 Remove；玩家移速由 ClearDovePendantDaytimeSpeed 复位）。
    /// </summary>
    public static void RemoveDovePendantNightBuffs()
    {
        var eventManager = NightScene.EventUtility.EventManager.Instance;
        if (eventManager == null) return;
        try
        {
            eventManager.RemovePartnerExtraWorkSpeed(DovePendantNightSpeedFactor, false);
            eventManager.RemovePartnerExtraMoveSpeed(DovePendantNightSpeedFactor, false);
            Log.Info("[DovePendant] 已撤销夜间伙伴效果");
        }
        catch (Exception ex)
        {
            Log.Error($"[DovePendant] 夜间效果撤销异常: {ex}");
        }
    }

    private static void ApplyNightPlayerSpeed()
    {
        var target = RunTimePlayerData.LevelProfile.MoveSpeedMultiplier * DovePendantNightSpeedFactor;
        if (PlayerManager.Local.unit != null)
        {
            PlayerManager.Local.Speed = target;
            Log.Info($"[DovePendant] 已应用夜间玩家移速（目标={target}）");
            return;
        }
        CommandScheduler.Enqueue(
            executeWhen: () => PlayerManager.Local.unit != null,
            execute: () =>
            {
                PlayerManager.Local.Speed = target;
                Log.Info($"[DovePendant] 已应用夜间玩家移速（目标={target}）");
            },
            timeoutSeconds: 30);
    }

    private static void ApplyDaytimeSpeed(float targetSpeed)
    {
        if (PlayerManager.Local.unit != null)
        {
            PlayerManager.Local.Speed = targetSpeed;
            Log.Info($"[DovePendant] 已应用白天移速（目标={targetSpeed}，当前={PlayerManager.Local.Speed}）");
            return;
        }
        CommandScheduler.Enqueue(
            executeWhen: () => PlayerManager.Local.unit != null,
            execute: () =>
            {
                PlayerManager.Local.Speed = targetSpeed;
                Log.Info($"[DovePendant] 已应用白天移速（目标={targetSpeed}，当前={PlayerManager.Local.Speed}）");
            },
            timeoutSeconds: 30);
    }

    /// <summary>
    /// 注册小鸽子挂坠的展示文案（名称与描述），写入 DataBaseLanguage.Items 供展示柜读取。
    /// </summary>
    public static void RegisterDovePendantDecorationLanguage()
    {
        RexAssetRegistry.TryGetSprite(DovePendantSpriteUri, out var sprite);
        DataBaseLanguage.Items[DovePendantDecorationId] = new ObjectLanguageBase(
            name: "小鸽子挂坠",
            Description: "激活后，白天时玩家移动速度增加0.12；夜间营业时移动速度增加20%，并增加20%的伙伴工作效率与伙伴移动速度。",
            visual: sprite);
        Log.Info($"[DovePendant] 已注册文案（id={DovePendantDecorationId}）");
    }
}
