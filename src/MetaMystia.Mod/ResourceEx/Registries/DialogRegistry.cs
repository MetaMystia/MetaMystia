using System;
using System.Collections.Generic;

using Mystia.Assets;
using Mystia.Data;

using Common.UI;
using GameData.Profile;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 对话包领域注册器：持有资源包的对话配置，构建交给框架的对话构建器
/// （<see cref="IGameDataBuilder.TryBuildDialog"/>）。
/// <para>
/// 框架侧建包、把包写进 <c>DataBaseDay.allDialogPackages</c>，并在游戏重建该表后重新写一遍；
/// 模组侧只记住哪些包被拒，需要实例时经框架的对话目录（<c>ICommonServices.Dialogs</c>）按包名取回
/// 同一个实例——礼物信箱、剧情回放、商人迎宾等链路仍按包名取用，接口与迁移前一致。
/// 行文本与分支选项文本由框架按包名写进游戏交给面板的替换字典，模组不再自填引擎引用、
/// 也不再持有文本覆盖回调。
/// </para>
/// </summary>
[AutoLog]
public static partial class DialogRegistry
{
    private static readonly Dictionary<string, DialogPackageConfig> _dialogPackageConfigs = new();

    /// <summary>被框架拒绝构建的包名（描述本身的问题，重试没有意义）。</summary>
    private static readonly HashSet<string> _refusedDialogPackages = new(StringComparer.Ordinal);

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.dialogPackages == null) return;

        foreach (var pkgConfig in config.dialogPackages)
        {
            // 框架按名字索引对话包（数据面也按名跳过无名条目），无名配置没有可用的名字。
            if (string.IsNullOrWhiteSpace(pkgConfig.name))
            {
                Log.LogWarning($"[{packageName}] Skipped a dialog package without a name.");
                continue;
            }

            _dialogPackageConfigs[pkgConfig.name] = pkgConfig;
            Log.LogInfo($"[{packageName}] Loaded dialog package: {pkgConfig.name}");
        }
    }

    public static bool ExistsDialogPackage(string name)
    {
        return _dialogPackageConfigs.ContainsKey(name);
    }

    /// <summary>已加载的对话包配置，供 <c>ModDatabaseExtension.OnInjectDialogs</c> 转成框架代理结构。</summary>
    internal static IEnumerable<DialogPackageConfig> Configs => _dialogPackageConfigs.Values;

    public static DialogPackageConfig GetDialogPackage(string name)
    {
        if (_dialogPackageConfigs.TryGetValue(name, out var pkg))
            return pkg;

        return null;
    }

    /// <summary>
    /// 按包名取框架构建好的对话包实例（幂等）。实例由框架写进 <c>DataBaseDay.allDialogPackages</c>，
    /// 这里经框架的对话目录读回同一个实例；名字不是资源包的对话包、或框架没能构建时返回 null
    /// （与迁移前一致）。
    /// </summary>
    public static DialogPackage GetBuiltDialogPackage(string name)
    {
        if (string.IsNullOrEmpty(name) || !_dialogPackageConfigs.ContainsKey(name))
        {
            Log.Warning($"Dialog package not built: {name}");
            return null;
        }

        BuildAllDialogPackages();

        if (ModRuntime.Dialogs is { } dialogs && dialogs.TryResolve(name, out var package))
            return package;

        Log.Warning($"Dialog package not built: {name}");
        return null;
    }

    /// <summary>
    /// 构建全部资源包对话包（幂等）。框架的构建器要求用游戏自己的对话包做模板
    /// （<c>DataBaseDay.allDialogPackages</c> 非空），而本方法在 <c>OnInjectDialogs</c> 时机被调用时
    /// 该表通常还没初始化：此时只记下配置、不构建，等按名取包（礼物信箱校验、商人迎宾）的时机再补建，
    /// 构建器自己会在表重建后重新写表。
    /// </summary>
    internal static void BuildAllDialogPackages()
    {
        var dialogs = ModRuntime.Dialogs;
        if (dialogs is null || dialogs.Names.Count == 0)
            return;

        foreach (var (name, config) in _dialogPackageConfigs)
            Build(name, config);
    }

    /// <summary>
    /// 把一个对话包交给框架的构建器（幂等）：已由框架构建过的、已被拒的都不再尝试。
    /// 框架拒绝的原因（缺模板、行无文本、取值越界等）写在框架日志里。
    /// </summary>
    private static void Build(string name, DialogPackageConfig config)
    {
        if (_refusedDialogPackages.Contains(name) || GameDataBuilders.Builder.IsDialogBuilt(name))
            return;

        if (ToSpec(config) is { } spec && GameDataBuilders.Builder.TryBuildDialog(spec, out _))
        {
            Log.Info($"Built dialog package: {name}");
            return;
        }

        // 对话表已就绪仍被拒 = 描述本身的问题：记下来，避免每次按名取包都重试并重复报告同一原因。
        _refusedDialogPackages.Add(name);
    }

    /// <summary>
    /// 迁移前的文本覆盖回调（自建包没有文本资源，行文本必须由回调按行号填进面板的替换字典）。
    /// 现在行文本与分支选项文本都由框架按包名写进同一个字典（框架桥接的 <c>DialogScripts</c>／
    /// <c>AssetBuilderTexts</c>），模组没有可交出的东西；保留该方法是因为礼物信箱与剧情回放
    /// 仍把它的返回值交给 <c>OpenDialogMenu</c>。
    /// </summary>
    public static Action<Il2CppSystem.Collections.Generic.Dictionary<int, string>> GetOverrideReplaceTextCallback(DialogPackage dialogPackage) => null;

    /// <summary>按包名打开资源包的对话包（实例由框架构建）。</summary>
    public static void ShowResourceExPackage(string packageName, Action onFinishCallback = null)
    {
        if (GetDialogPackage(packageName) is null)
        {
            Log.LogWarning($"Dialog package {packageName} not found in ResourceExManager.");
            return;
        }

        Log.LogInfo("Calling OpenDialogMenu...");
        UniversalGameManager.OpenDialogMenu(
            GetBuiltDialogPackage(packageName),
            onFinishCallback: onFinishCallback,
            previousPanelVisualMode: 0
        );
    }

    // ── 配置到框架描述 ──

    /// <summary>
    /// 一个包的对话描述；有空行条目时返回 null（框架同样会按「line N is empty」拒绝），
    /// 由调用方记为该包被拒。
    /// </summary>
    private static DialogSpec? ToSpec(DialogPackageConfig config)
    {
        var lines = new List<DialogLineSpec>(config.Count);
        for (int index = 0; index < config.Count; index++)
        {
            var dialog = config[index];
            if (dialog is null)
            {
                Log.LogWarning($"Dialog package {config.name} has an empty line #{index + 1}; it is not built.");
                return null;
            }

            lines.Add(new DialogLineSpec
            {
                // SpeakerKind／DialogSide 与游戏 SpeakerIdentity.Identity／Position 逐值一致。
                Speaker = new SpeakerSpec((SpeakerKind)(int)dialog.characterType, dialog.characterId, dialog.pid),
                Side = (DialogSide)(int)dialog.position,
                Text = dialog.text,
                // 与迁移前写死的行级 flag 相同：前景说话、不压暗、替换姓名、不用行内立绘。
                IsSpeakInForeground = true,
                IsDark = false,
                UseNameInText = true,
                Actions = Actions(config, index, dialog.actions),
            });
        }

        return new DialogSpec { Name = config.name, Lines = lines };
    }

    /// <summary>一行里的行内动作，按原顺序；框架不表达的动作已被丢弃（见 <see cref="Action"/>）。</summary>
    private static IReadOnlyList<DialogActionSpec> Actions(DialogPackageConfig config, int dialogIndex, DialogActionConfig[] actionConfigs)
    {
        var actions = new List<DialogActionSpec>();
        if (actionConfigs is null)
            return actions;

        for (int index = 0; index < actionConfigs.Length; index++)
        {
            if (actionConfigs[index] is { } actionConfig && Action(config, dialogIndex, index, actionConfig) is { } action)
                actions.Add(action);
        }

        return actions;
    }

    /// <summary>
    /// 一个行内动作。框架按动作类型校验载荷（该有的要有、不该有的不能有），因此资源包表达不了或取不到
    /// 载荷的动作返回 null：调用方丢掉这条动作，而不是让整个包被拒。
    /// </summary>
    private static DialogActionSpec? Action(DialogPackageConfig config, int dialogIndex, int actionIndex, DialogActionConfig actionConfig)
    {
        // 取值与游戏 DialogPannel.ActionType 一致（框架的 DialogActionKind 按同一顺序）。
        var kind = (DialogActionKind)(int)actionConfig.actionType;
        var at = $"{config.name}[{dialogIndex + 1}] action #{actionIndex + 1}";

        switch (kind)
        {
            case DialogActionKind.BG:
            case DialogActionKind.CG:
                if (ResolveSprite(actionConfig.sprite) is { } sprite)
                    return new DialogActionSpec { Kind = kind, ShouldSet = actionConfig.shouldSet, Sprite = sprite };

                // 迁移前取不到的图写成空引用；面板对「要设置但图为空」的处理就是清掉该层
                // （DialogPannel 的 Run：shouldSet 为假或图为空都置空），所以这里用「清层」的表达法。
                if (actionConfig.shouldSet)
                    Log.LogWarning($"{at} sets an image that is not registered ({actionConfig.sprite}); it clears the layer instead.");
                return new DialogActionSpec { Kind = kind, ShouldSet = false };

            case DialogActionKind.Sound:
                if (ResolveSound(actionConfig.sound) is { } sound)
                    return new DialogActionSpec { Kind = kind, ShouldSet = actionConfig.shouldSet, Sound = sound };

                // 空引用等于不播（面板的声音槽直接取资产句柄），丢弃动作的结果相同。
                Log.LogWarning($"{at} plays a sound that is not registered ({actionConfig.sound}); the action is dropped.");
                return null;

            case DialogActionKind.Branch:
                if (Options(config, dialogIndex, actionIndex, actionConfig) is { Count: > 0 } options)
                    return new DialogActionSpec { Kind = kind, ShouldSet = actionConfig.shouldSet, Options = options };

                // 迁移前的空分支写下空数组；面板要求三个数组都非空才当分支处理，等于没有分支。
                Log.LogWarning($"{at} is a branch without options; the action is dropped.");
                return null;

            case DialogActionKind.Goto:
                return new DialogActionSpec
                {
                    Kind = kind,
                    ShouldSet = actionConfig.shouldSet,
                    Index = ResolveLineNumber(config, dialogIndex, actionConfig.index, $"{at} Goto"),
                };

            case DialogActionKind.End:
                // End 的 index 是原版退出码（面板把它交给 ExitCode），框架拒绝负退出码。
                var exitCode = actionConfig.exitCode ?? actionConfig.index ?? 0;
                if (exitCode < 0)
                {
                    Log.LogWarning($"{at} leaves with the exit code {exitCode}; it is treated as 0.");
                    exitCode = 0;
                }

                return new DialogActionSpec { Kind = kind, ShouldSet = actionConfig.shouldSet, Index = exitCode };

            case DialogActionKind.PlayBGM:
                // 资源包不表达 BGM 包，迁移前写下的也是空引用（等于不播）；框架的 PlayBGM 必须要 BGM 包。
                Log.LogWarning($"{at} plays a BGM package, which the resource pack format does not carry; the action is dropped.");
                return null;

            case DialogActionKind.SwitchBranch:
                // 资源包既不表达调度条件也不表达该动作的跳转表，迁移前的自建动作两者都是空的。
                Log.LogWarning($"{at} is a switch branch, which the resource pack format does not carry; the action is dropped.");
                return null;

            default:
                // ForegroundCleaning／CameraShake／PauseResumeBGM／StopBGM／TutorialSFX／Null 只带类型与 shouldSet。
                return new DialogActionSpec { Kind = kind, ShouldSet = actionConfig.shouldSet };
        }
    }

    /// <summary>
    /// 分支选项：文本为空的选项被丢弃（框架拒绝空选项文本，面板还按选项文本 id 直接索引替换字典）；
    /// 价格为空按 0，为负按 0 并告警（框架拒绝负价格）。一个都不剩时调用方丢弃整个分支动作。
    /// </summary>
    private static IReadOnlyList<DialogBranchOptionData> Options(DialogPackageConfig config, int dialogIndex, int actionIndex, DialogActionConfig actionConfig)
    {
        var options = new List<DialogBranchOptionData>();
        var optionConfigs = actionConfig.options;
        if (optionConfigs is null || optionConfigs.Count == 0)
            return options;

        for (int index = 0; index < optionConfigs.Count; index++)
        {
            var option = optionConfigs[index];
            var at = $"{config.name}[{dialogIndex + 1}] action #{actionIndex + 1} option #{index + 1}";
            if (option is null || string.IsNullOrWhiteSpace(option.text))
            {
                Log.LogWarning($"{at} has no text; it is dropped.");
                continue;
            }

            var price = option.price ?? 0;
            if (price < 0)
            {
                Log.LogWarning($"{at} costs {price}; it is treated as 0.");
                price = 0;
            }

            options.Add(new DialogBranchOptionData
            {
                Text = option.text,
                Jump = ResolveLineNumber(config, dialogIndex, option.jump, at),
                Price = price,
            });
        }

        return options;
    }

    /// <summary>
    /// 目标对话序号：1 起始，行数 + 1 表示结束。框架的 <c>Jump</c>／<c>Index</c> 与游戏
    /// <c>DialogPannel</c> 的 <c>i = JumpId - 1</c>／<c>i = Index - 1</c> 一致，即资源包文档里的「第 N 个对话」。
    /// 缺失或越界时回退到下一行（最后一行的下一行即结束），与迁移前 <c>ResolveDialogJumpIndex</c>
    /// 的日志与意图相同。
    /// </summary>
    private static int ResolveLineNumber(DialogPackageConfig config, int dialogIndex, int? targetIndex, string context)
    {
        var fallback = Math.Min(dialogIndex + 2, config.Count + 1);
        if (!targetIndex.HasValue)
        {
            Log.LogWarning($"{context} target is missing in dialog package {config.name}[{dialogIndex + 1}], falling back to dialog #{fallback}.");
            return fallback;
        }

        if (targetIndex.Value < 1 || targetIndex.Value > config.Count + 1)
        {
            Log.LogWarning($"{context} target dialog #{targetIndex.Value} is out of range in dialog package {config.name}[{dialogIndex + 1}], falling back to dialog #{fallback}.");
            return fallback;
        }

        return targetIndex.Value;
    }

    /// <summary>CG/BG 的图：取资源包登记在框架资产管线里的精灵句柄（与迁移前的引用指向同一份资产）。</summary>
    private static SpriteHandle? ResolveSprite(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return null;

        return RexAssetRegistry.TryGetSprite(uri, out var sprite) ? sprite : null;
    }

    /// <summary>Sound 的剪辑：取资源包登记在框架资产管线里的剪辑句柄。</summary>
    private static AudioClipHandle? ResolveSound(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return null;

        return ModRuntime.Locator is { } locator && locator.TryResolveAudioClip(uri, out var clip) ? clip : null;
    }
}
