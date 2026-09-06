using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MetaMystia.ConsoleSystem;
using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;
using MetaMystia.ResourceEx.Registries;
using MetaMystia.UI;

namespace MetaMystia;

/// <summary>
/// ResourceEx 资源包加载与生命周期编排入口：包加载、DLC 依赖检查、游戏数据库初始化钩子。
/// 各内容领域的配置持有与注册逻辑见对应的 *Registry 类。
/// </summary>
[AutoLog]
public static partial class ResourceExManager
{
    // Abstracted resource root path
    public static string ResourceRoot { get; set; } = Path.Combine(Paths.GameRootPath, "ResourceEx");

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

        SpecialGuestRegistry.RegisterAllSpawnConfigs();
        IngredientRegistry.RegisterAllIngredients();
        BeverageRegistry.RegisterAllBeverages();
        RecipeRegistry.RegisterAllRecipes();
        FoodRegistry.RegisterAllFoods();
        ClothRegistry.RegisterAllClothItems();
        ClothRegistry.RegisterAllClothProfiles();
    }
    public static void OnDataBaseDayInitialized()
    {
        DialogRegistry.RegisterAllDialogPackages();
        GiftRegistry.ValidateAllGifts();

        SpecialGuestRegistry.RegisterNPCs();
        // RegisterAllSpawnMarkers(); // DO NOT DELETE
        MerchantRegistry.BuildAllMerchants();
    }
    public static void OnDataBaseLanguageInitialized()
    {
        SpecialGuestRegistry.RegisterAllFoodRequests();
        SpecialGuestRegistry.RegisterAllBevRequests();
        SpecialGuestRegistry.RegisterSpecialPortraits();
        IngredientRegistry.RegisterAllIngredientLanguages();
        BeverageRegistry.RegisterAllBeverageLanguages();
        FoodRegistry.RegisterAllFoodLanguages();
        MissionNodeRegistry.RegisterAllMissionNodeLanguages();
        ClothRegistry.RegisterAllClothLanguages();
    }

    public static void OnDataBaseCharacterInitialized()
    {
        DialogRegistry.BuildAllDialogPackages();
        SpecialGuestRegistry.RegisterAllSpecialGuestPairs();
        SpecialGuestRegistry.RegisterAllSpecialGuests(); // 依赖 Dialog

        MissionNodeRegistry.RegisterAllMissionNodes(); // 依赖 Dialog
        EventNodeRegistry.RegisterAllEventNodes(); // 依赖 Dialog

        ClothRegistry.RegisterAllClothPixelSprites(); // 依赖 DataBaseCharacter
    }

    public static void OnDataBaseAchievementInitialized()
    {
        // Currently no actions needed here
    }
    public static void OnDataBaseSchedulerInitialized()
    {
        // RegisterAllMissionNodes(); // 依赖 Dialog
        // RegisterAllEventNodes(); // 依赖 Dialog
        MissionNodeRegistry.RegisterAllMissionNodesMapping();
        EventNodeRegistry.RegisterAllEventNodesMapping();
    }
    public static void OnNightSceneLanguageInitialized()
    {
        SpecialGuestRegistry.RegisterAllConversations();
        SpecialGuestRegistry.RegisterAllEvaluations();
    }

    public static void OnDaySceneLanguageInitialized()
    {
        // Currently no actions needed here
    }

    public static void OnDaySceneAwake()
    {
        SpecialGuestRegistry.RefreshAllDayNpcs();
        SchedulerDataRecovery.CheckAndReloadSchedulerData();
        EventNodeRegistry.ActivateAllKizunaEventNodes(); // 依赖 CheckAndReloadSchedulerData
        SpecialGuestRegistry.ResetTrackedNpcDialog();
        MerchantRegistry.CheckAndCleanOrphanedMerchants(); // 清理孤儿商人数据，防止 RefMerchant KeyNotFoundException
        MerchantRegistry.RegisterAllTrackedMerchant();
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
    /// Merges a loaded resource package into the per-domain registries
    /// </summary>
    private static void MergeResourcePackage(LoadedResourcePackage package)
    {
        var config = package.Config;
        string packageName = package.PackageName;
        string packageLabel = package.PackageLabel;

        NormalizePackageResourceUris(config, packageLabel);

        SpecialGuestRegistry.Merge(config, packageName);
        DialogRegistry.Merge(config, packageName);
        GiftRegistry.Merge(package);
        IngredientRegistry.Merge(config, packageName);
        FoodRegistry.Merge(config, packageName);
        BeverageRegistry.Merge(config, packageName);
        RecipeRegistry.Merge(config, packageName);
        MissionNodeRegistry.Merge(config, packageName);
        EventNodeRegistry.Merge(config, packageName);
        MerchantRegistry.Merge(config, packageName);
        ClothRegistry.Merge(config, packageName);
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

        return RexAssetRegistry.TryResolveUri(path, packageLabel, out var uri) ? uri : path;
    }
}
