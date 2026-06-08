using System;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using NightScene.EventUtility;
using UnityEngine;

using GameData.CoreLanguage;

namespace MetaMystia.Patch;

/// <summary>
/// 封装游戏原生 RegisterTimedBuff API。
/// 由于 IL2CPP 的 RegisterTimedBuff 签名复杂（含 out Action + 多个枚举参数 + 可选参数），
/// Harmony ReversePatch 难以在编译期精确匹配，因此采用运行时反射发现并调用。
/// Token: 0x06003233 RID: 13091
/// </summary>
[HarmonyPatch(typeof(EventManager))]
public static class NativeBuffHelper
{
    // ---- 枚举常量（dnSpy 导出） ----

    public static class BT
    {
        public const int Null = 0;
        public const int LockDailyRecipe = 1;
        public const int UnlockInfo = 2;
        public const int Stun = 3;
        public const int CookTimeOnTargetTag = 4;
        public const int FreeCook = 5;
        public const int LockGuestTable = 6;
        public const int NormalGuestSpawnMultiplier = 7;
        public const int InstantEvaluation = 8;
        public const int PatientFreeze = 9;
        public const int ThrowDeliver = 10;
        public const int SpawnNorm = 11;
        public const int Fever = 12;
        // 自定义 buff type，不与游戏原生值冲突
        public const int DaiyouseiFog = 100;
    }

    // ---- 公开 API ----

    private static bool _registered;
    private static MethodInfo _rtbMethod;
    private static Type _buffTypeEnum;
    private static bool _rtbResolved;
    private static System.Func<int, string, string> _contextOverride;

    public static bool IsRegistered => _registered;

    //不需要，无参数
    //重置内部注册状态标记，传送门关闭时调用
    //不返回
    public static void Reset() => _registered = false;

    //需要(int buffTypeInt) 枚举值(如BT.Null=0), (float durationSeconds) 持续秒数, 默认float.MaxValue
    //反射调用游戏原生RegisterTimedBuff显示右下角buff图标，文本需提前通过RegisterCustomBuffDescription注入
    //返回(bool) true=注册成功
    public static bool Register(int buffTypeInt, float durationSeconds = float.MaxValue, System.Func<int, string, string> contextOverride = null)
    {
        try
        {
            if (EventManager.Instance == null)
            {
                Debug.LogWarning("[MM] NBH.Register: EventManager.Instance is null");
                return false;
            }

            if (!ResolveRegisterTimedBuff())
            {
                Debug.LogError("[MM] NBH.Register: failed to resolve RegisterTimedBuff method");
                return false;
            }

            int dur = durationSeconds >= float.MaxValue ? int.MaxValue : (int)Math.Round(durationSeconds);
            _contextOverride = contextOverride;
            Debug.Log($"[MM] NBH.Register: calling RegisterTimedBuff type={buffTypeInt} dur={dur}s hasContextOverride={contextOverride != null}");

            var args = BuildRegisterTimedBuffArgs(dur, buffTypeInt);
            var result = _rtbMethod.Invoke(EventManager.Instance, args);
            var ok = result == null; // RegisterTimedBuff returns void, null = success
            if (ok) _registered = true;
            Debug.Log($"[MM] NBH.Register: RegisterTimedBuff returned {ok}");
            _contextOverride = null;
            return ok;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MM] NBH.Register failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            _contextOverride = null;
            return false;
        }
    }

    // ---- 运行时方法解析 ----

    //不需要，无参数
    //运行时反射发现RegisterTimedBuff真实签名并缓存，仅执行一次
    //返回(bool) true=解析成功
    private static bool ResolveRegisterTimedBuff()
    {
        if (_rtbResolved && _rtbMethod != null) return true;

        try
        {
            _rtbMethod = typeof(EventManager).GetMethod(
                "RegisterTimedBuff",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (_rtbMethod == null)
            {
                Debug.LogError("[MM] NBH: RegisterTimedBuff not found via reflection");
                _rtbResolved = true;
                return false;
            }

            // 诊断：打印完整签名
            var parms = _rtbMethod.GetParameters();
            Debug.Log($"[MM] NBH: RegisterTimedBuff has {parms.Length} params, returns {_rtbMethod.ReturnType.Name}");
            foreach (var p in parms)
                Debug.Log($"  [{p.Position}] {p.ParameterType.Name} {p.Name} (out={p.IsOut}, optional={p.IsOptional})");

            // 缓存 BuffType 枚举类型（从第一个枚举参数推断）
            foreach (var p in parms)
            {
                if (p.ParameterType.IsEnum)
                {
                    _buffTypeEnum = p.ParameterType;
                    Debug.Log($"[MM] NBH: enum type for buffType = {_buffTypeEnum.FullName}");
                    break;
                }
            }

            _rtbResolved = true;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MM] NBH: ResolveRegisterTimedBuff failed: {ex.Message}");
            _rtbResolved = true;
            return false;
        }
    }

    //需要(int duration) 持续时间, (int buffTypeInt) BuffType枚举值
    //根据运行时发现的方法签名构建完整参数数组，处理枚举/out/Action/可选参数
    //返回(object[]) 与方法签名匹配的参数数组
    private static object[] BuildRegisterTimedBuffArgs(int duration, int buffTypeInt)
    {
        var parms = _rtbMethod.GetParameters();
        var args = new object[parms.Length];

        // 构造 onEnd 回调（buff 到期清理标记）
        System.Action onEndManaged = () =>
        {
            _registered = false;
            Debug.Log($"[MM] NBH: buff expired");
        };
        var onEndCb = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(onEndManaged);

        for (int i = 0; i < parms.Length; i++)
        {
            var p = parms[i];

            if (p.Name == "duration" || (p.ParameterType == typeof(int) && i == 0))
            {
                args[i] = duration;
            }
            else if (p.ParameterType.IsEnum)
            {
                // BuffType / BuffRegisterType 等枚举 → int 转枚举值
                args[i] = Enum.ToObject(p.ParameterType, buffTypeInt);
            }
            else if (p.IsOut)
            {
                // out Action onInterruptCb → 用 null 占位
                args[i] = null;
            }
            else if (p.ParameterType.FullName?.Contains("Action") == true)
            {
                // Action onEnd → Il2CppSystem.Action 回调
                // 只给第一个非 out 的 Action 参数赋值 onEndCb
                if (p.Name == "onEnd" || p.Name.Contains("End"))
                    args[i] = onEndCb;
                else
                    args[i] = null;
            }
            else if (p.IsOptional)
            {
                // currentBuffContextOverride (Func<int,string,string>) — 动态描述回调
                if (p.Name == "currentBuffContextOverride" && _contextOverride != null)
                {
                    try
                    {
                        var convertMethod = typeof(DelegateSupport).GetMethod("ConvertDelegate");
                        var genericConvert = convertMethod.MakeGenericMethod(p.ParameterType);
                        args[i] = genericConvert.Invoke(null, new object[] { _contextOverride });
                        Debug.Log($"[MM] NBH: injected contextOverride for param [{i}]");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MM] NBH: failed to convert contextOverride: {ex.Message}");
                        args[i] = p.DefaultValue ?? Type.Missing;
                    }
                }
                else if (p.ParameterType == typeof(int[]))
                {
                    args[i] = null;
                }
                else
                {
                    args[i] = p.DefaultValue ?? Type.Missing;
                }
            }
            else
            {
                // 未知参数 → null / 默认
                Debug.LogWarning($"[MM] NBH: unhandled param [{i}] {p.Name}:{p.ParameterType.Name} → setting null");
                args[i] = null;
            }
        }

        return args;
    }

    // ---- BuffDescription 自定义文本注入 ----

    //需要(int buffType) BuffType枚举值, (string title) 标题, (string description) 描述, (Sprite visual) 图标可选
    //用反射向游戏BuffDescription字典注入自定义buff文本，RegisterTimedBuff调用时自动读取
    //不返回
    public static void RegisterCustomBuffDescription(int buffType, string title, string description, Sprite visual = null)
    {
        try
        {
            var dict = GameData.CoreLanguage.Collections.DataBaseLanguage.BuffDescription;
            if (dict == null)
            {
                Debug.LogWarning($"[MM] NBH: BuffDescription dict is null, cannot register custom text for buffType={buffType}");
                return;
            }

            var lang = new ObjectLanguageBase(
                name: title,
                Description: description,
                visual: visual);

            // 用反射调用 dict indexer 避免编译期枚举类型匹配问题
            var indexer = dict.GetType().GetProperty("Item");
            if (indexer != null)
            {
                // 尝试将 int 转为字典 key 的实际枚举类型
                var keyParamType = indexer.GetIndexParameters()[0].ParameterType;
                object key = Enum.ToObject(keyParamType, buffType);
                indexer.SetValue(dict, lang, new object[] { key });
            }
            else
            {
                Debug.LogWarning("[MM] NBH: BuffDescription has no Item indexer");
            }
            Debug.Log($"[MM] NBH: Registered custom buff desc for type={buffType}, title='{title}'");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MM] NBH: Failed to register custom buff desc for type={buffType}: {ex.Message}");
        }
    }
}
