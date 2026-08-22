using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

using GameData.Core.Collections.DaySceneUtility.Collections;
using GameData.Core.Collections.CharacterUtility;
using GameData.Profile;

using MetaMystia.ConsoleSystem;
using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;
using MetaMystia.UI;

namespace MetaMystia;


[AutoLog]
public static partial class ResourceExManager
{
    // Abstracted resource root path
    public static string ResourceRoot { get; set; } = Path.Combine(Paths.GameRootPath, "ResourceEx");

    private static Dictionary<(int id, string type), CharacterConfig> _characterConfigs = new Dictionary<(int id, string type), CharacterConfig>();
    private static Dictionary<string, CharacterSpriteSetCompact> _characterSpriteSets = new Dictionary<string, CharacterSpriteSetCompact>();
    private static Dictionary<string, DialogPackageConfig> _dialogPackageConfigs = new Dictionary<string, DialogPackageConfig>();
    private static Dictionary<string, BuiltDialogPackage> _builtDialogPackages = new Dictionary<string, BuiltDialogPackage>();
    private static Dictionary<string, Merchant> _builtMerchants = new Dictionary<string, Merchant>();

    // Loaded package metadata for console queries
    private static readonly List<LoadedResourcePackage> _loadedPackages = new List<LoadedResourcePackage>();
    private static readonly List<(string packageName, string reason)> _rejectedPackages = new List<(string, string)>();
    private static readonly List<Func<string>> _pendingConsoleLogs = new List<Func<string>>();
    private static bool _packagesLoaded;

    public static IReadOnlyList<LoadedResourcePackage> LoadedPackages => _loadedPackages;
    public static IReadOnlyList<(string packageName, string reason)> RejectedPackages => _rejectedPackages;

    /// <summary>
    /// 当前激活的 DLC / 已加载包标签（如 "CORE"、"DLC1"），由 DLC 检测 Hook 更新，
    /// 资源包加载成功后把自身 label 加入。
    /// </summary>
    public static string[] ActivePackTags { get; private set; } = ["CORE"];

    /// <summary>
    /// 由 DLC 检测 Hook 调用：CORE 恒激活，加上 GetActiveKeys 返回的激活 DLC keys。
    /// DLC 状态在会话内不变，仅首次调用生效，避免覆盖已加载的包标签。
    /// </summary>
    public static void SetActiveDlcTags(IEnumerable<string> dlcKeys)
    {
        if (_dlcTagsSet) return;
        _dlcTagsSet = true;
        ActivePackTags = ["CORE", .. dlcKeys];
    }
    private static bool _dlcTagsSet;

    private static void AddActivePackTag(string tag)
    {
        if (ActivePackTags.Contains(tag)) return;
        ActivePackTags = [.. ActivePackTags, tag];
    }

    /// <summary>
    /// Flush pending resource pack load messages to InGameConsole's deferred queue.
    /// Call once after InGameConsole.Initialize() (e.g. PluginHost.Awake).
    /// </summary>
    public static void FlushPendingConsoleLogs()
    {
        foreach (var factory in _pendingConsoleLogs)
            InGameConsole.LogDeferred(factory);
        _pendingConsoleLogs.Clear();
    }

    private static Dictionary<int, IngredientConfig> IngredientConfigs = new Dictionary<int, IngredientConfig>();
    private static Dictionary<int, FoodConfig> FoodConfigs = new Dictionary<int, FoodConfig>();
    private static Dictionary<int, BeverageConfig> BeverageConfigs = new Dictionary<int, BeverageConfig>();
    private static Dictionary<int, RecipeConfig> RecipeConfigs = new Dictionary<int, RecipeConfig>();
    private static List<MissionNodeConfig> MissionNodeConfigs = new List<MissionNodeConfig>();
    private static List<EventNodeConfig> EventNodeConfigs = new List<EventNodeConfig>();
    private static Dictionary<string, MerchantConfig> MerchantConfigs = new Dictionary<string, MerchantConfig>();
    private static Dictionary<int, ClothConfig> ClothConfigs = new Dictionary<int, ClothConfig>();

    // Public ID set accessors for ResourceDataBase rEx integration
    public static HashSet<int> LoadedRecipeIds => [.. RecipeConfigs.Keys];
    public static HashSet<int> LoadedFoodIds => [.. FoodConfigs.Keys];
    public static HashSet<int> LoadedBeverageIds => [.. BeverageConfigs.Keys];
    public static HashSet<int> LoadedIngredientIds => [.. IngredientConfigs.Keys];
    public static HashSet<int> LoadedSpecialGuestIds => [.. _characterConfigs.Where(kv => kv.Key.type == "Special").Select(kv => kv.Key.id)];

    // Cloth portrait cache: clothId -> Sprite (loaded lazily or during preload)
    private static Dictionary<int, Sprite> _clothPortraitCache = new Dictionary<int, Sprite>();
    // Cloth pixel full cache: skinIndex -> CharacterSpriteSetFull (built during character init)
    private static Dictionary<int, CharacterSpriteSetFull> _clothPixelFullCache = new Dictionary<int, CharacterSpriteSetFull>();

    public static void Initialize()
    {
        // 目录准备；包加载延迟到 DLC flags 确定后（见 OnDlcFlagsDetermined）
        if (!Directory.Exists(ResourceRoot))
            Directory.CreateDirectory(ResourceRoot);
    }

    /// <summary>
    /// DLC flags 确定后加载资源包（依赖检查需要 DLC 激活状态），由 DLC 检测 Hook 调用
    /// </summary>
    public static void OnDlcFlagsDetermined()
    {
        if (_packagesLoaded) return;
        _packagesLoaded = true;
        LoadAllResourcePackages();
        // 加载发生在 PluginHost.Awake 之后，这里直接 flush；PluginHost 再 flush 时队列已空
        FlushPendingConsoleLogs();
    }

    // 加载逻辑
    // DataBaseCore -> DataBaseScheduler -> DataBaseCharacter -> DataBaseLanguage -> DataBaseDay

    public static void OnDataBaseCoreInitialized()
    {
        // 兜底：若 GetActiveKeys Hook 未触发（如非 Steam 平台），此时 DLC 状态已确定，补做加载
        OnDlcFlagsDetermined();

        RegisterAllSpawnConfigs();
        RegisterAllIngredients();
        RegisterAllBeverages();
        RegisterAllRecipes();
        RegisterAllFoods();
        RegisterAllClothItems();
        RegisterAllClothProfiles();
    }
    public static void OnDataBaseDayInitialized()
    {
        RegisterAllDialogPackages();

        RegisterNPCs();
        // RegisterAllSpawnMarkers(); // DO NOT DELETE
        BuildAllMerchants();
    }
    public static void OnDataBaseLanguageInitialized()
    {
        RegisterAllFoodRequests();
        RegisterAllBevRequests();
        RegisterSpecialPortraits();
        RegisterAllIngredientLanguages();
        RegisterAllBeverageLanguages();
        RegisterAllFoodLanguages();
        RegisterAllMissionNodeLanguages();
        RegisterAllClothLanguages();
    }

    public static void OnDataBaseCharacterInitialized()
    {
        BuildAllDialogPackages();
        RegisterAllSpecialGuestPairs();
        RegisterAllSpecialGuests(); // 依赖 Dialog

        RegisterAllMissionNodes(); // 依赖 Dialog
        RegisterAllEventNodes(); // 依赖 Dialog

        RegisterAllClothPixelSprites(); // 依赖 DataBaseCharacter
    }

    public static void OnDataBaseAchievementInitialized()
    {
        // Currently no actions needed here
    }
    public static void OnDataBaseSchedulerInitialized()
    {
        // RegisterAllMissionNodes(); // 依赖 Dialog
        // RegisterAllEventNodes(); // 依赖 Dialog
        RegisterAllMissionNodesMapping();
        RegisterAllEventNodesMapping();
    }
    public static void OnNightSceneLanguageInitialized()
    {
        RegisterAllConversations();
        RegisterAllEvaluations();
    }

    public static void OnDaySceneLanguageInitialized()
    {
        // Currently no actions needed here
    }

    public static void OnDaySceneAwake()
    {
        RefreshAllDayNpcs();
        CheckAndReloadSchedulerData();
        ActivateAllKizunaEventNodes(); // 依赖 CheckAndReloadSchedulerData
        ResetTrackedNpcDialog();
        CheckAndCleanOrphanedMerchants(); // 清理孤儿商人数据，防止 RefMerchant KeyNotFoundException
        RegisterAllTrackedMerchant();
    }

    /// <summary>
    /// Loads all resource packages from the ResourceEx directory
    /// </summary>
    private static void LoadAllResourcePackages()
    {
        var packages = ResourcePackageLoader.LoadAllPackages(ResourceRoot, out var rejected);

        var accepted = new List<LoadedResourcePackage>();
        bool anyDependencyRejected = false;
        foreach (var package in packages)
        {
            var missingDeps = GetMissingDependencies(package);
            if (missingDeps.Count > 0)
            {
                anyDependencyRejected = true;
                string reason = TextId.ResourcePackageDependencyMissingReason.Get(string.Join(", ", missingDeps));
                Log.LogWarning($"[{package.PackageName}] Rejected: {reason}");
                InGameConsole.LogDeferred(() => TextId.ResourcePackageDependencyMissing.Get(package.PackageName, string.Join(", ", missingDeps)));
                rejected.Add((package.PackageName, reason));
                continue;
            }

            accepted.Add(package);
            _loadedPackages.Add(package);
            AddActivePackTag(package.PackageLabel);
            RexAssetRegistry.RegisterPackage(package);
        }

        foreach (var package in accepted)
        {
            MergeResourcePackage(package);
        }

        _rejectedPackages.AddRange(rejected);

        Log.LogInfo($"Loaded {accepted.Count} resource package(s) successfully.");

        // Queue console messages — will be flushed to InGameConsole after it becomes available
        foreach (var pkg in _loadedPackages)
        {
            var info = pkg.Config?.packInfo;
            var captured = pkg;
            if (info != null)
            {
                _pendingConsoleLogs.Add(() =>
                    ConsoleFormat.Ok(TextId.ResourceExConsoleLoaded.Get(
                        info.name ?? captured.PackageName,
                        info.version ?? "?",
                        info.authors != null ? string.Join(", ", info.authors) : "Unknown")));
            }
            else
            {
                _pendingConsoleLogs.Add(() =>
                    ConsoleFormat.Ok(TextId.ResourceExConsoleLoadedNoInfo.Get(captured.PackageName)));
            }
        }
        foreach (var (name, reason) in _rejectedPackages)
        {
            var capturedName = name;
            var capturedReason = reason;
            _pendingConsoleLogs.Add(() =>
                ConsoleFormat.Err(TextId.ResourceExConsoleRejected.Get(capturedName, capturedReason)));
        }

        // 所有警告输出完毕后，额外补充正版提示与禁用检查指引
        if (anyDependencyRejected)
        {
            Log.LogWarning(TextId.DlcMissingDependencyNotice.Get(nameof(ConfigManager.IgnoreDlcDependencyCheck), "true"));
            _pendingConsoleLogs.Add(() => TextId.DlcMissingDependencyNotice.Get(nameof(ConfigManager.IgnoreDlcDependencyCheck), "true"));
        }
    }

    /// <summary>
    /// 返回包声明的依赖中当前未激活的部分（如 "DLC2"、"DLC5"）
    /// </summary>
    private static List<string> GetMissingDependencies(LoadedResourcePackage package)
    {
        if (ConfigManager.IgnoreDlcDependencyCheck.Value)
            return new List<string>();

        var deps = package.Config?.packInfo?.dependencies;
        if (deps == null || deps.Count == 0)
            return new List<string>();

        return deps.Where(dep => !ActivePackTags.Contains(dep)).ToList();
    }

    /// <summary>
    /// Merges a loaded resource package into the manager's internal data structures
    /// </summary>
    private static void MergeResourcePackage(LoadedResourcePackage package)
    {
        var config = package.Config;
        string packageName = package.PackageName;
        string packageLabel = package.PackageLabel;

        NormalizePackageResourceUris(config, packageLabel);

        if (config?.characters != null)
        {
            foreach (var charConfig in config.characters)
            {
                _characterConfigs[(charConfig.id, charConfig.type)] = charConfig;
                Log.LogInfo($"[{packageName}] Loaded config for character {charConfig.name} ({charConfig.id}, {charConfig.type})");
            }
        }

        if (config?.dialogPackages != null)
        {
            foreach (var pkgConfig in config.dialogPackages)
            {
                _dialogPackageConfigs[pkgConfig.name] = pkgConfig;
                Log.LogInfo($"[{packageName}] Loaded dialog package: {pkgConfig.name}");
            }
        }

        if (config?.ingredients != null)
        {
            foreach (var ingredientConfig in config.ingredients)
            {
                IngredientConfigs[ingredientConfig.id] = ingredientConfig;
                Log.LogInfo($"[{packageName}] Loaded config for ingredient {ingredientConfig.id}");
            }
        }

        if (config?.foods != null)
        {
            foreach (var foodConfig in config.foods)
            {
                FoodConfigs[foodConfig.id] = foodConfig;
                Log.LogInfo($"[{packageName}] Loaded config for food {foodConfig.name} ({foodConfig.id})");
            }
        }

        if (config?.beverages != null)
        {
            foreach (var beverageConfig in config.beverages)
            {
                BeverageConfigs[beverageConfig.id] = beverageConfig;
                Log.LogInfo($"[{packageName}] Loaded config for beverage {beverageConfig.name} ({beverageConfig.id})");
            }
        }
        if (config?.recipes != null)
        {
            foreach (var recipeConfig in config.recipes)
            {
                RecipeConfigs[recipeConfig.id] = recipeConfig;
                Log.LogInfo($"[{packageName}] Loaded config for recipe {recipeConfig.id}");
            }
        }

        if (config?.missionNodes != null)
        {
            foreach (var missionNodeConfig in config.missionNodes)
            {
                MissionNodeConfigs.Add(missionNodeConfig);
                Log.LogInfo($"[{packageName}] Loaded config for mission node {missionNodeConfig.title}");
            }
        }

        if (config?.eventNodes != null)
        {
            foreach (var eventNodeConfig in config.eventNodes)
            {
                EventNodeConfigs.Add(eventNodeConfig);
                Log.LogInfo($"[{packageName}] Loaded config for event node {eventNodeConfig.debugLabel}");
            }
        }

        if (config?.merchants != null)
        {
            foreach (var merchantConfig in config.merchants)
            {
                MerchantConfigs[merchantConfig.key] = merchantConfig;
                Log.LogInfo($"[{packageName}] Loaded config for merchant {merchantConfig.key}");
            }
        }

        if (config?.clothes != null)
        {
            foreach (var clothConfig in config.clothes)
            {
                ClothConfigs[clothConfig.id] = clothConfig;
                Log.LogInfo($"[{packageName}] Loaded config for cloth {clothConfig.name} ({clothConfig.id})");
            }
        }
    }

    private static void NormalizePackageResourceUris(ResourceConfig config, string packageLabel)
    {
        if (config == null)
            return;

        if (config.characters != null)
        {
            foreach (var charConfig in config.characters)
            {
                if (charConfig.portraits != null)
                {
                    foreach (var portrait in charConfig.portraits)
                        portrait.path = ResolveAssetUriOrSelf(portrait.path, packageLabel);
                }

                if (charConfig.characterSpriteSetCompact != null)
                {
                    var pixelConfig = charConfig.characterSpriteSetCompact;
                    NormalizeConfigAssetUris(pixelConfig.mainSprite, packageLabel);
                    NormalizeConfigAssetUris(pixelConfig.eyeSprite, packageLabel);
                }
            }
        }

        if (config.dialogPackages != null)
        {
            foreach (var dialogPackage in config.dialogPackages)
            {
                if (dialogPackage.dialogList == null) continue;
                for (int dialogIndex = 0; dialogIndex < dialogPackage.dialogList.Count; dialogIndex++)
                {
                    var dialog = dialogPackage.dialogList[dialogIndex];
                    if (dialog?.actions == null) continue;

                    for (int actionIndex = 0; actionIndex < dialog.actions.Length; actionIndex++)
                    {
                        var action = dialog.actions[actionIndex];
                        if (action == null) continue;

                        action.sprite = ResolveAssetUriOrSelf(action.sprite, packageLabel);
                        action.sound = ResolveAssetUriOrSelf(action.sound, packageLabel);
                    }
                }
            }
        }

        if (config.ingredients != null)
        {
            foreach (var ingredientConfig in config.ingredients)
                ingredientConfig.spritePath = ResolveAssetUriOrSelf(ingredientConfig.spritePath, packageLabel);
        }

        if (config.foods != null)
        {
            foreach (var foodConfig in config.foods)
                foodConfig.spritePath = ResolveAssetUriOrSelf(foodConfig.spritePath, packageLabel);
        }

        if (config.beverages != null)
        {
            foreach (var beverageConfig in config.beverages)
                beverageConfig.spritePath = ResolveAssetUriOrSelf(beverageConfig.spritePath, packageLabel);
        }

        if (config.clothes != null)
        {
            foreach (var clothConfig in config.clothes)
            {
                clothConfig.spritePath = ResolveAssetUriOrSelf(clothConfig.spritePath, packageLabel);
                clothConfig.portraitPath = ResolveAssetUriOrSelf(clothConfig.portraitPath, packageLabel);

                if (clothConfig.pixelFullConfig == null) continue;
                NormalizeConfigAssetUris(clothConfig.pixelFullConfig.mainSprite, packageLabel);
                NormalizeConfigAssetUris(clothConfig.pixelFullConfig.eyeSprite, packageLabel);
                NormalizeConfigAssetUris(clothConfig.pixelFullConfig.hairSprite, packageLabel);
                NormalizeConfigAssetUris(clothConfig.pixelFullConfig.backSprite, packageLabel);
            }
        }
    }

    private static void NormalizeConfigAssetUris(List<string> paths, string packageLabel)
    {
        if (paths == null)
            return;

        for (int i = 0; i < paths.Count; i++)
            paths[i] = ResolveAssetUriOrSelf(paths[i], packageLabel);
    }

    private static string ResolveAssetUriOrSelf(string path, string packageLabel)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        return ResolveAssetUri(path, packageLabel) ?? path;
    }
}
