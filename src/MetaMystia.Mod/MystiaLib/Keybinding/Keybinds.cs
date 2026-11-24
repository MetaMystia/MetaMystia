using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine.InputSystem;

using Common.UI.EscapeUtility;
using DEYU.AdpUISystem.Managers;
using GameData;

namespace MetaMystia.MystiaLib.Keybinding;

/// <summary>一个按键：动作名（同一模组内唯一）、默认键（如 <c>&lt;Keyboard&gt;/t</c>）、面板标题。</summary>
public sealed record Keybind(string Name, string Key, Func<string> Title);

/// <summary>
/// 向原版按键设置面板注册模组按键，每个模组一个映射。面板只在启动加载时生成一次，须在插件 <c>Load</c> 中注册。
/// 映射始终启用，菜单、对话和文本输入期间同样触发，使用方自行判断上下文。
/// </summary>
public static class Keybinds
{
    internal const string MapPrefix = Bootstrap.Id + ".";

    private sealed record Owner(string Name, string Category, Keybind[] Keybinds, InputActionMap Map);

    private static readonly List<Owner> Owners = [];
    private static readonly List<InputActionMap> Pending = [];
    private static bool _panelBuilt;

    /// <summary>已加入游戏输入资源的映射。</summary>
    internal static IEnumerable<InputActionMap> AddedMaps => Owners.Select(o => o.Map).Where(m => m.asset != null);

    /// <summary>注册一个模组的全部按键，返回其映射，按动作名取 <see cref="InputAction"/>。</summary>
    /// <param name="owner">模组标识，通常为插件 GUID。</param>
    /// <param name="category">面板标题的类别行，通常为模组名。</param>
    public static InputActionMap Register(string owner, string category, params Keybind[] keybinds)
    {
        if (string.IsNullOrEmpty(owner) || owner.Contains('/') || Owners.Any(o => o.Name == owner))
            throw new ArgumentException($"Invalid or duplicate owner '{owner}'", nameof(owner));
        if (keybinds.Length == 0 || keybinds.Any(k => string.IsNullOrEmpty(k.Name) || k.Name.Contains('/'))
            || keybinds.Select(k => k.Name).Distinct().Count() != keybinds.Length)
            throw new ArgumentException("Keybind names must be unique, non-empty and contain no '/'", nameof(keybinds));

        var map = InputActionMap.FromJson(MapJson(owner, keybinds))[0];
        Owners.Add(new Owner(owner, category, keybinds, map));
        Pending.Add(map);
        if (_panelBuilt)
            Bootstrap.Log.LogWarning($"{owner} registered after the binding panel was built; its rows are not shown");
        if (UnityEngine.Object.FindObjectOfType<AdpUIPanelManager>() != null)
            AddPendingMaps();
        return map;
    }

    // 映射、动作、绑定的 ID 由模组与动作名确定性生成，绑定覆盖按 ID 保存，生成方式不得修改。
    private static string MapJson(string owner, Keybind[] keybinds) => JsonSerializer.Serialize(new
    {
        maps = new[]
        {
            new
            {
                name = MapPrefix + owner,
                id = Id(owner),
                actions = keybinds.Select(k => new { name = k.Name, type = "Button", id = Id(owner, k.Name), expectedControlType = "Button" }),
                bindings = keybinds.Select(k => new
                {
                    name = "", id = Id(owner, k.Name, "0"), path = k.Key, interactions = "", processors = "", groups = "",
                    action = k.Name, isComposite = false, isPartOfComposite = false,
                }),
            },
        },
    });

    private static string Id(params string[] parts) =>
        new Guid(MD5.HashData(Encoding.UTF8.GetBytes(string.Join("/", parts.Prepend(Bootstrap.Id))))).ToString();

    /// <summary>
    /// 加入游戏的输入资源。Input System 1.3.0 的 <c>AddActionMap</c> 不刷新共享状态：先停用整个资源，加入后重新解析，再恢复原有启用状态。
    /// 原版切换输入模式只开关当前映射，模组映射保持启用。
    /// </summary>
    internal static void AddPendingMaps()
    {
        if (Pending.Count == 0) return;
        var asset = AdpUIPanelManager.PlayerInput.actions;
        var enabled = new List<InputActionMap>();
        foreach (var map in asset.actionMaps)
            if (map.enabled) enabled.Add(map);
        asset.Disable();
        foreach (var map in Pending)
            InputActionSetupExtensions.AddActionMap(asset, map);
        asset.actionMaps[0].ResolveBindings();
        foreach (var map in enabled)
            map.Enable();
        KeybindStore.Apply(Pending);
        foreach (var map in Pending)
            map.Enable();
        Bootstrap.Log.LogInfo($"Added keybinding maps: {string.Join(", ", Pending.Select(m => m.name))}");
        Pending.Clear();
    }

    /// <summary>在原版生成行之前追加模组行，所有模组的行同属一个冲突组。</summary>
    internal static void AppendRows(EscBindingPannel panel)
    {
        _panelBuilt = true;
        if (Owners.Count == 0) return;
        foreach (var data in panel.inputBindingDatas)
            if (data.titleKey.StartsWith(MapPrefix)) return;
        AddPendingMaps();
        SetTitles();

        var rows = new List<EscBindingPannel.InputBindingData>();
        foreach (var owner in Owners)
            foreach (var keybind in owner.Keybinds)
            {
                var pair = new EscBindingPannel.InputBindingData.InputActionReferencePair();
                pair.focusingInputAction = InputActionReference.Create(owner.Map[keybind.Name]);
                pair.overrideBindingIndex = -1;
                var row = new EscBindingPannel.InputBindingData();
                row.titleKey = TitleKey(owner, keybind);
                row.focusingInputAction = new Il2CppReferenceArray<EscBindingPannel.InputBindingData.InputActionReferencePair>([pair]);
                rows.Add(row);
            }
        var group = new EscBindingPannel.StringArray();
        group.array = new Il2CppStringArray(rows.Select(r => r.titleKey).ToArray());

        panel.inputBindingDatas = Append(panel.inputBindingDatas, rows);
        panel.groups = Append(panel.groups, [group]);
    }

    /// <summary>标题表在语言切换时重建，打开面板前按当前语言重新设置。</summary>
    internal static void SetTitles()
    {
        foreach (var owner in Owners)
            foreach (var keybind in owner.Keybinds)
                MultiLanguageTextMeshTitle.SetTitlePhrases(TitleKey(owner, keybind), $"{owner.Category}\n - {keybind.Title()}");
    }

    private static string TitleKey(Owner owner, Keybind keybind) => $"{owner.Map.name}/{keybind.Name}";

    private static Il2CppReferenceArray<T> Append<T>(Il2CppReferenceArray<T> source, List<T> items) where T : Il2CppObjectBase
    {
        var result = new Il2CppReferenceArray<T>(source.Length + items.Count);
        for (int i = 0; i < source.Length; i++)
            result[i] = source[i];
        for (int i = 0; i < items.Count; i++)
            result[source.Length + i] = items[i];
        return result;
    }
}
