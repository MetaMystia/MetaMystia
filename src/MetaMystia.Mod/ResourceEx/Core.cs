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
    public static string ResourceRoot { get; set; } = Path.Combine(ModRuntime.Paths.GameRoot, "ResourceEx");

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

    /// <summary>
    /// 数据注入前置，幂等：包加载（含版本/DLC/签名校验）与包内与游戏表无关的能力初始化。
    /// 由 <c>ModDatabaseExtension</c> 在框架收集注入数据前调用，时机与原 DataBaseCore.Initialize 后缀相同；
    /// 其它数据注入扩展同样应先调用本方法，不要依赖扩展之间的先后顺序。
    /// </summary>
    public static void PrepareDatabaseInjection()
    {
        if (_injectionPrepared) return;
        _injectionPrepared = true;
        OnDataBaseCoreInitialized();
    }
    private static bool _injectionPrepared;

    public static void OnDataBaseCoreInitialized()
    {
        // 兜底：若 GetActiveKeys Hook 未触发（如非 Steam 平台），此时 DLC 状态已确定，补做加载
        OnDlcFlagsDetermined();

        AssetBundleRegistry.LoadAll(); // 先于依赖特效的符卡
        // 符卡的效果实现由模组提供（ISpell，框架按 SpellId 匹配）；这里检查实现依赖并保管特效包。
        SpellRegistry.InitializeAll();
        // 特殊客人的刷客池（Izakayas[].SpecialGuestPool）由框架按 OnInjectSpecialGuests 的 Spawns 写入，
        // 此处不再直接写表
        // 食材/饮料/菜谱/食物/服装由 ModDatabaseExtension 经 IDatabaseExtension 注入，此处不再直接写表
        // 商人与白天地图同理；地图本体的引擎对象由 OnInjectDayMaps 在收集阶段构建（DayMapRegistry.BuildAll）
    }
    public static void OnDataBaseDayInitialized()
    {
        // 地图数据面（mapData／刷新点与采集点标签／地图语言／映射）、商人与对话包由框架按 OnInject* 写入；
        // 这里只补框架无法表达的部分：内存地图的地址引用与礼物邮箱校验。
        GiftRegistry.ValidateAllGifts();

        // RegisterAllSpawnMarkers(); // DO NOT DELETE
        DayMapRegistry.RegisterMapReferences();
    }
    public static void OnDataBaseLanguageInitialized()
    {
        // 任务名与符卡语言的表由框架在注入时一并写入（OnInjectMissionNodes／OnInjectSpells），此处不再写表
        // 食材/饮料/食物/服装/Buff 语言由 ModDatabaseExtension 经 IDatabaseExtension 注入，此处不再直接写表
        // 特殊客人的角色文本与点单请求行同样由 OnInjectSpecialGuests 注入，此处不再直接写表
    }

    /// <summary>
    /// 迁移前由 <c>Patches/Compat/DataBaseCharacterPatch</c> 在 <c>DataBaseCharacter.Initialize</c> 后缀调用。
    /// 该补丁已随 E2 删除：特殊客人记录、像素集与立绘、对话包构建都改由
    /// <c>ModDatabaseExtension</c>（<c>OnInjectSpecialGuests</c>／<c>OnInjectDialogs</c>）交给框架写入。
    /// 框架在 <c>DataBaseCharacter.Initialize</c> 之后没有给模组的回调，因此本入口当前没有调用点，
    /// 保留给仍需「晚于角色表重建」的装配（见迁移缺口的接线说明）。
    /// </summary>
    public static void OnDataBaseCharacterInitialized()
    {
        // 任务／事件节点表、节点映射与「是否拥有符卡」标记由框架写入（OnInjectMissionNodes／
        // OnInjectEventNodes／OnInjectSpells）；节点的对话包由框架在 DataBaseDay 写入后再回填。
    }

    public static void OnDaySceneLanguageInitialized()
    {
        // 地图名与描述（DaySceneLanguage.MapLanguageData）由框架按 OnInjectDayMaps 写入，此处不再写表。
        // 迁移前由 Patches/Compat/DaySceneLanguagePatch.cs 调用；该补丁已随 E2 删除，本入口保留给后续接线。
    }

    public static void OnDaySceneAwake()
    {
        // 迁移前由 Patches/Compat/DataBaseDayPatch.cs（已随 E2 删除）在 DataBaseDay.Initialize 后缀调用。
        // 框架在 DataBaseDay 的写入之后没有给模组的回调，这里退到白天场景唤醒时补做框架无法表达的部分
        // （内存地图的地址引用要早于换图，礼物邮箱只在这里用到）。待确认：mapReference 的写入时机。
        OnDataBaseDayInitialized();

        SpecialGuestRegistry.RefreshAllDayNpcs();
        SchedulerDataRecovery.CheckAndReloadSchedulerData();
        EventNodeRegistry.ActivateAllKizunaEventNodes(); // 依赖 CheckAndReloadSchedulerData
        SpecialGuestRegistry.ResetTrackedNpcDialog();
        // 商人的运行时追踪记录由框架在注入时建立；这里只清理已移除资源包留下的孤儿记录，
        // 防止游戏调用 DataBaseDay.RefMerchant 抛 KeyNotFoundException。
        MerchantRegistry.CheckAndCleanOrphanedMerchants();
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
        DayMapRegistry.Merge(package);

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
        SpellRegistry.Merge(config, packageName);
        BuffRegistry.Merge(config, packageName);
        AssetBundleRegistry.Merge(config, packageName);
    }

    private static void NormalizePackageResourceUris(ResourceConfig config, string packageLabel)
    {
        if (config == null)
            return;

        foreach (var spell in config.spells ?? [])
        {
            spell.vfxBundle = ResolveAssetUriOrSelf(spell.vfxBundle, packageLabel);
            foreach (var card in new[] { spell.positive, spell.negative })
                if (card != null)
                    card.portrait = ResolveAssetUriOrSelf(card.portrait, packageLabel);
        }

        foreach (var buff in config.buffs ?? [])
            buff.icon = ResolveAssetUriOrSelf(buff.icon, packageLabel);

        foreach (var bundle in config.assetBundles ?? [])
            bundle.path = ResolveAssetUriOrSelf(bundle.path, packageLabel);

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
