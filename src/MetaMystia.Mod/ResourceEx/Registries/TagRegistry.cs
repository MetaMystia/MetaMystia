using System;
using System.Collections.Generic;
using System.Linq;

using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;

using static GameData.Core.Collections.Sellable;
using static GameData.Profile.SchedulerNodeCollection.MissionNode.FinishCondition;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 自定义 Tag 注册器：持有料理与酒水 Tag 声明、料理 Tag 冲突规则，并校验各配置对 Tag 的引用。
/// 料理 Tag 与食材 Tag 共用编号，酒水 Tag 独立编号。
/// </summary>
[AutoLog]
public static partial class TagRegistry
{
    private static readonly Dictionary<int, TagConfig> FoodTagConfigs = new();
    private static readonly Dictionary<int, TagConfig> BeverageTagConfigs = new();
    private static readonly List<(string packageName, int index, int[] tags)> FoodTagRuleConfigs = new();
    // 游戏保留范围内的引用，语言数据就绪后核对是否存在
    private static readonly List<(int tag, bool isFood, string owner)> NativeTagReferences = new();

    internal static IReadOnlyCollection<TagConfig> FoodTags => FoodTagConfigs.Values;
    internal static IReadOnlyCollection<TagConfig> BeverageTags => BeverageTagConfigs.Values;

    internal static void Merge(ResourceConfig config, string packageName)
    {
        MergeTags(config?.foodTags, FoodTagConfigs, "foodTags", packageName);
        MergeTags(config?.beverageTags, BeverageTagConfigs, "beverageTags", packageName);

        var rules = config?.foodTagRules;
        for (int i = 0; i < (rules?.Count ?? 0); i++)
            FoodTagRuleConfigs.Add((packageName, i, rules[i]?.tags?.Distinct().ToArray() ?? []));
    }

    private static void MergeTags(List<TagConfig> tags, Dictionary<int, TagConfig> target, string field, string packageName)
    {
        foreach (var tag in tags ?? [])
        {
            if (string.IsNullOrWhiteSpace(tag?.name))
            {
                Log.LogWarning($"[{packageName}] {field} {tag?.id}: 缺少 name，已跳过");
                continue;
            }
            if (target.ContainsKey(tag.id))
                Log.LogWarning($"[{packageName}] {field} {tag.id} 已被声明，由后加载者覆盖");
            target[tag.id] = tag;
            Log.LogInfo($"[{packageName}] Loaded config for {field} {tag.name} ({tag.id})");
        }
    }

    /// <summary>
    /// 所有包合并后调用：移除对未声明自定义 Tag 的引用，任务条件只警告。必须早于数据库注册。
    /// </summary>
    internal static void ValidateReferences(IEnumerable<LoadedResourcePackage> packages)
    {
        foreach (var package in packages)
        {
            var config = package.Config;
            if (config == null) continue;
            var pkg = package.PackageName;

            foreach (var food in config.foods ?? [])
            {
                RemoveUndeclared(food.tags, x => x, true, $"[{pkg}] foods[{food.id}].tags");
                RemoveUndeclared(food.banTags, x => x, true, $"[{pkg}] foods[{food.id}].banTags");
            }
            foreach (var ingredient in config.ingredients ?? [])
                RemoveUndeclared(ingredient.tags, x => x, true, $"[{pkg}] ingredients[{ingredient.id}].tags");
            foreach (var beverage in config.beverages ?? [])
                RemoveUndeclared(beverage.tags, x => x, false, $"[{pkg}] beverages[{beverage.id}].tags");

            foreach (var character in config.characters ?? [])
            {
                var guest = character.guest;
                if (guest == null) continue;
                var owner = $"[{pkg}] characters[{character.id}].guest";
                RemoveUndeclared(guest.hateFoodTag, x => x, true, $"{owner}.hateFoodTag");
                RemoveUndeclared(guest.likeFoodTag, x => x.tagId, true, $"{owner}.likeFoodTag");
                RemoveUndeclared(guest.likeBevTag, x => x.tagId, false, $"{owner}.likeBevTag");
                RemoveUndeclared(guest.foodRequests, x => x.tagId, true, $"{owner}.foodRequests");
                RemoveUndeclared(guest.bevRequests, x => x.tagId, false, $"{owner}.bevRequests");
            }

            foreach (var mission in config.missionNodes ?? [])
            {
                var conditions = mission.finishConditions ?? [];
                for (int i = 0; i < conditions.Count; i++)
                {
                    var condition = conditions[i];
                    var tags = condition?.conditionType switch
                    {
                        ConditionType.SubmitByTag => condition.tag is int tag ? [tag] : [],
                        ConditionType.SubmitByTags or ConditionType.SubmitByAnyOneTag => condition.tags ?? [],
                        _ => [],
                    };
                    bool isFood = condition?.sellableType != SellableType.Beverage;
                    var owner = $"[{pkg}] missionNodes[{mission.label}].finishConditions[{i}]";
                    var undeclared = InspectReferences(tags, isFood, owner);
                    if (undeclared.Count > 0)
                        Log.LogWarning($"{owner}: 引用未声明的{Kind(isFood)} Tag {string.Join(", ", undeclared)}，条件无法完成");
                }
            }
        }
    }

    private static void RemoveUndeclared<T>(List<T> references, Func<T, int> tagOf, bool isFood, string owner)
    {
        if (references == null) return;
        var undeclared = InspectReferences(references.Select(tagOf), isFood, owner);
        if (undeclared.Count == 0) return;
        references.RemoveAll(x => undeclared.Contains(tagOf(x)));
        Log.LogWarning($"{owner}: 已移除未声明的{Kind(isFood)} Tag {string.Join(", ", undeclared)}");
    }

    /// <summary>
    /// 返回未声明的自定义 Tag；游戏保留范围内的引用留待语言数据就绪后核对。
    /// </summary>
    private static List<int> InspectReferences(IEnumerable<int> tags, bool isFood, string owner)
    {
        var undeclared = new List<int>();
        foreach (var tag in tags.Distinct())
        {
            if (tag <= IdRangeValidator.GameIdMax)
                NativeTagReferences.Add((tag, isFood, owner));
            else if (!(isFood ? FoodTagConfigs : BeverageTagConfigs).ContainsKey(tag))
                undeclared.Add(tag);
        }
        return undeclared;
    }

    /// <summary>
    /// 追加料理 Tag 冲突规则。原版 SolveTagPriority 只按首条包含某 Tag 的规则分组，
    /// 与已有规则重叠的 Tag 不会受新规则约束，因此整条跳过。
    /// </summary>
    internal static void RegisterAllFoodTagRules()
    {
        var rules = DataBaseCore.TagRules;
        var occupied = new HashSet<int>();
        int nextKey = 0;
        foreach (var rule in rules)
        {
            nextKey = Math.Max(nextKey, rule.Key + 1);
            occupied.UnionWith(rule.Value);
        }

        foreach (var (packageName, index, tags) in FoodTagRuleConfigs)
        {
            var owner = $"[{packageName}] foodTagRules[{index}]";
            var undeclared = InspectReferences(tags, true, owner);
            var overlapped = tags.Where(occupied.Contains).ToList();
            var error = tags.Length < 2 ? "至少需要两个不同的 Tag"
                : undeclared.Count > 0 ? $"Tag 未声明: {string.Join(", ", undeclared)}"
                : overlapped.Count > 0 ? $"Tag 已属于其他规则: {string.Join(", ", overlapped)}"
                : null;
            if (error != null)
            {
                Log.LogWarning($"{owner}: {error}，已跳过");
                continue;
            }

            rules[nextKey++] = tags;
            occupied.UnionWith(tags);
            Log.Info($"Registered food tag rule {owner}: {string.Join(" > ", tags)}");
        }
    }

    internal static void RegisterAllTagLanguages()
    {
        foreach (var tag in FoodTagConfigs.Values)
        {
            DataBaseLanguage.FoodTags[tag.id] = tag.name;
            DataBaseLanguage.FoodTagsDLCMapping[tag.id] = "ResourceEx";
        }
        foreach (var tag in BeverageTagConfigs.Values)
            DataBaseLanguage.BeverageTags[tag.id] = tag.name;
        Log.Info($"Registered {FoodTagConfigs.Count} food tag(s), {BeverageTagConfigs.Count} beverage tag(s)");

        foreach (var (tag, isFood, owner) in NativeTagReferences)
            if (!(isFood ? DataBaseLanguage.FoodTags : DataBaseLanguage.BeverageTags).ContainsKey(tag))
                Log.LogWarning($"{owner}: 游戏中不存在{Kind(isFood)} Tag {tag}");
        NativeTagReferences.Clear();
    }

    private static string Kind(bool isFood) => isFood ? "料理" : "酒水";
}
