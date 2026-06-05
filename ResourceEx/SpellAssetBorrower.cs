using Il2CppInterop.Runtime;
using UnityEngine;

using GameData.Core.Collections.NightSceneUtility;

namespace MetaMystia.ResourceEx;

/// <summary>
/// 通用符卡资源借用器。
/// 通过 Resources.FindObjectsOfTypeAll 从内存中找到已加载的符卡实例，
/// 借出它们的 Prefab / AudioClip / 参数。
///
/// 用法：
///   var cirno  = SpellAssetBorrower.Borrow&lt;Spell_Cirno&gt;();
///   var wriggle = SpellAssetBorrower.Borrow&lt;Spell_Wriggle&gt;();
///   cirno?.giveIceItem           // 琪露诺的飞行道具
///   wriggle?.firefliesParent     // 莉格露的萤火虫
/// </summary>
public static class SpellAssetBorrower
{
    /// <summary>从内存中找指定类型的符卡实例。</summary>
    /// <typeparam name="T">符卡类型，如 Spell_Cirno</typeparam>
    /// <returns>第一个找到的实例，找不到则 null</returns>
    public static T Borrow<T>() where T : SpellBase
    {
        // ⚠️ Resources.FindObjectsOfTypeAll 只能找到"已加载"的对象。
        // 如果还没遇到过该角色（游戏没加载她的 Spell ScriptableObject），
        // 会返回空数组，此时返回 null。
        var type = Il2CppType.Of<T>();
        var all = Resources.FindObjectsOfTypeAll(type);
        if (all == null || all.Length == 0) return null;

        return all[0].TryCast<T>();
    }

    /// <summary>带日志的版本，找不到时会打印警告。</summary>
    public static T Borrow<T>(string logTag) where T : SpellBase
    {
        var result = Borrow<T>();
        if (result == null)
            UnityEngine.Debug.LogWarning($"[{logTag}] 未找到 {typeof(T).Name} 实例（还没遇到过该角色？）");
        return result;
    }
}
