using System;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

using Common.UI;
using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage;
using GameData.CoreLanguage.Collections;
using NightScene.EventUtility;

using SgrYuki.Utils;

namespace MetaMystia.ResourceEx.SpellCollection;

[AutoLog]
public partial class Spell_Koakuma : SpellBase
{
    private const int MaxEchoCount = 3;
    private const string EchoBuffTitle = "灵符「遗失典籍的回响」";

    private static bool _echoActive;
    private static bool _chaosActive;
    private static Sprite _buffIcon;
    private static readonly System.Random _rng = new();
    private static Il2CppSystem.Action _echoInterruptCallback;

    // 持有委托引用防止 GC 回收（IL2CPP 需要托管侧保持引用）

    public static bool IsEchoActive => _echoActive;
    public static bool IsChaosActive => _chaosActive;

    // ================================================================================
    // Buff 图标加载
    // ================================================================================

    public static void LoadBuffIcon()
    {
        if (_buffIcon != null) return;
        if (ResourceExManager.TryGetSprite("rex://ResourceExample/assets/Buff/9001_1.png", out var sprite) && sprite != null)
        {
            _buffIcon = sprite;
            Log.LogInfo("[Koakuma] Buff icon loaded");
        }
        else
        {
            Log.LogWarning("[Koakuma] Buff icon load failed");
        }
    }

    // ================================================================================
    // 红卡 Buff 注册（使用原生 RegisterCountedBuff + contextOverride）
    // ================================================================================

    internal static void RegisterEchoBuff()
    {
        // 注入 BuffDescription（静态描述）
        SpellHelper.RegisterBuffDescription(
            (EventManager.BuffType)NativeBuffHelper_BuffType_KoakumaEcho,
            EchoBuffTitle,
            "小恶魔从图书馆搬来一本百科全书，接下来3次稀客点单会告诉你具体tag",
            _buffIcon);

        // 原生计数型 buff：3 次计数，无 contextOverride
        EventManager.Instance.RegisterCountedBuff(
            (EventManager.BuffType)NativeBuffHelper_BuffType_KoakumaEcho,
            MaxEchoCount,
            EventManager.MathOperation.Set,
            null,
            Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(
                new System.Action(() =>
                {
                    _echoActive = false;
                    _echoInterruptCallback = null;
                    Log.LogInfo("[Koakuma] 红卡 Echo buff 计数归零，自动结束");
                })),
            null);
    }

    // ================================================================================
    // 清理
    // ================================================================================

    public static void CleanupAll()
    {
        if (_echoActive)
        {
            _echoActive = false;
            if (_echoInterruptCallback != null)
            {
                try { _echoInterruptCallback.Invoke(); } catch { }
                _echoInterruptCallback = null;
            }
        }
        _chaosActive = false;
        _chaosActiveCount = 0;
    }

    // ================================================================================
    // SpellBase overrides
    // ================================================================================

    public override string OnGettingSpellOwnerIdentifier() => "_ResourceExample_Koakuma";
    public override bool HasPositiveSpell => true;
    public override bool HasNegativeSpell => true;
    public override bool ShouldCallSpellDeclarationAuto(bool isPositiveSpell)
    {
        SpellHelper.SetCutinShift(OnGettingSpellOwnerIdentifier());
        return true;
    }

    public override Il2CppSystem.Collections.IEnumerator OnPositiveBuffExecute(SpellExecutionContext ctx)
    {
        Log.LogInfo("[Koakuma] OnPositiveBuffExecute called by game native");
        try { return PositiveBuffRoutine(ctx).WrapToIl2Cpp(); }
        catch (Exception ex)
        {
            Log.LogError($"[Koakuma] OnPositiveBuffExecute threw: {ex}");
            throw;
        }
    }

    public override Il2CppSystem.Collections.IEnumerator OnNegativeBuffExecute(SpellExecutionContext ctx)
    {
        Log.LogInfo("[Koakuma] OnNegativeBuffExecute called by game native");
        try { return NegativeBuffRoutine(ctx).WrapToIl2Cpp(); }
        catch (Exception ex)
        {
            Log.LogError($"[Koakuma] OnNegativeBuffExecute threw: {ex}");
            throw;
        }
    }

    /// <summary>
    /// 黑卡兜底激活入口。由 SpellExecutionDiagnosticPatch 的 Harmony Postfix 调用，
    /// 因为 SpellBase.ScheduleNegativeBuffExecution 不是 virtual 方法，无法 override。
    /// 【CRITICAL】所有逻辑内联于此方法中，防止 IL2CPP 剥离 ActivateChaosInternal 子方法。
    /// </summary>
    public static void ActivateChaosFallback()
    {
        UnityEngine.Debug.Log($"[Koakuma] ActivateChaosFallback: START, _chaosActiveCount={_chaosActiveCount}");

        // 第1步：设置混沌激活标志 + 计数
        _chaosActive = true;
        _chaosActiveCount++;
        UnityEngine.Debug.Log($"[Koakuma] ActivateChaosFallback: _chaosActive=true, _chaosActiveCount={_chaosActiveCount}");

        // 第2步：注册 Buff 描述（幂等，多次调用无害）
        SpellHelper.RegisterBuffDescription(
            (EventManager.BuffType)NativeBuffHelper_BuffType_KoakumaChaos,
            "幻符「献给巴瓦鲁的镇魂曲」",
            "30秒内料理面板的食材顺序被打乱，交互的厨具变为随机厨具",
            _buffIcon);

        SpellHelper.TimedBuffDurations[NativeBuffHelper_BuffType_KoakumaChaos] = (int)ChaosDuration;

        // 第3步：每次触发都注册独立的 buff 图标（纯显示，不含逻辑）
        // 参照大妖精：onBuffEnd = null，图标只负责倒计时显示
        if (EventManager.Instance != null)
        {
            EventManager.Instance.RegisterTimedBuff(
                (int)ChaosDuration,
                (EventManager.BuffType)NativeBuffHelper_BuffType_KoakumaChaos,
                out _,
                null);  // ← 游戏逻辑由独立协程管理，不绑在 UI 回调上
            UnityEngine.Debug.Log("[Koakuma] ActivateChaosFallback: RegisterTimedBuff done (onBuffEnd=null)");
        }
        else
        {
            UnityEngine.Debug.Log("[Koakuma] ActivateChaosFallback: WARNING EventManager.Instance is null");
        }

        // 第4步：启动独立的 30s 倒计时协程（管理游戏逻辑，与 buff 图标解耦）
        PluginManager.Instance.StartCoroutine(
            ChaosTimerRoutine(_chaosActiveCount).WrapToIl2Cpp());

        // 第5步：通知玩家
        if (ReceivedObjectDisplayerController.Instance != null)
        {
            ReceivedObjectDisplayerController.Instance.NotifyTextMessage("小恶魔的恶作剧开始了！食材和厨具都乱套了！");
        }

        UnityEngine.Debug.Log($"[Koakuma] ActivateChaosFallback: COMPLETE, _chaosActiveCount={_chaosActiveCount}");
        Log.LogInfo($"[Koakuma] 黑卡：混沌效果激活 #{_chaosActiveCount}，持续{ChaosDuration}秒");
    }

    /// <summary>
    /// 独立的混沌持续时间协程。到期时递减计数器，仅当所有实例都结束时才关闭混沌。
    /// 参照大妖精 ScreenFogDestroyRoutine 的设计：游戏逻辑与 buff 图标完全解耦。
    /// </summary>
    [HideFromIl2Cpp]
    private static System.Collections.IEnumerator ChaosTimerRoutine(int triggerId)
    {
        yield return new UnityEngine.WaitForSeconds(ChaosDuration);

        _chaosActiveCount--;
        UnityEngine.Debug.Log($"[Koakuma] ChaosTimer #{triggerId} 到期, _chaosActiveCount={_chaosActiveCount}");

        if (_chaosActiveCount <= 0)
        {
            _chaosActiveCount = 0;
            _chaosActive = false;
            UnityEngine.Debug.Log("[Koakuma] 黑卡：所有混沌效果结束");
            Log.LogInfo("[Koakuma] 黑卡：混沌效果结束");
        }
        else
        {
            Log.LogInfo($"[Koakuma] 黑卡：还有 {_chaosActiveCount} 个混沌效果存活");
        }
    }

    // ================================================================================
    // 红卡：灵符【遗失典籍的回响】
    // 使用游戏原生 RegisterCountedBuff 管理计数，
    // contextOverride 回调动态更新描述中的剩余次数。
    // ================================================================================

    [HideFromIl2Cpp]
    private IEnumerator PositiveBuffRoutine(SpellExecutionContext ctx)
    {
        Log.LogInfo("[Koakuma] PositiveBuffRoutine: START");
        _echoActive = true;
        Log.LogInfo($"[Koakuma] PositiveBuffRoutine: _echoActive = {_echoActive}");
        RegisterEchoBuff();
        Log.LogInfo("[Koakuma] PositiveBuffRoutine: RegisterEchoBuff done");

        Log.LogInfo($"[Koakuma] 红卡：Echo buff 激活，{MaxEchoCount}次（tag 将在传菜界面显示）");

        yield break;
    }

    // ================================================================================
    // 黑卡：幻符【献给巴瓦鲁的镇魂曲】
    // 30 秒混沌效果：食材顺序打乱 + 厨具随机化
    //
    // 重要：游戏 native 侧在 Spell Queue 中可能跳过 OnNegativeBuffExecute，
    //       因此覆写 ScheduleNegativeBuffExecution 直接激活混沌效果。
    //       NegativeBuffRoutine 保留作为 fallback（若 OnNegativeBuffExecute 被调用）。
    // ================================================================================

    private const float ChaosDuration = 30f;
    private static int _chaosActiveCount;

    [HideFromIl2Cpp]
    private IEnumerator NegativeBuffRoutine(SpellExecutionContext ctx)
    {
        Log.LogInfo("[Koakuma] NegativeBuffRoutine: START (fallback path)");
        ActivateChaosFallback();
        yield break;
    }

    // ================================================================================
    // BuffType 常量（与 NativeBuffHelper.BT 一致，直接用于原生 API）
    // ================================================================================

    private const int NativeBuffHelper_BuffType_KoakumaEcho = 101;
    private const int NativeBuffHelper_BuffType_KoakumaChaos = 102;

}
