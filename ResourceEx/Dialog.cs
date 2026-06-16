using System;
using System.Collections.Generic;

using Il2CppInterop.Runtime.InteropTypes.Arrays;

using Common.DialogUtility;
using Common.UI;
using GameData.Core.Collections.DaySceneUtility;
using GameData.Profile;

using MetaMystia.ResourceEx.Models;

using UnityEngine.AddressableAssets;

namespace MetaMystia;

public static partial class ResourceExManager
{
    public static DialogPackage ExampleDialog { get; private set; }

    private sealed class BuiltDialogPackage
    {
        public DialogPackage Package { get; }
        public Dictionary<int, string> OverrideTexts { get; }
        public System.Action<Il2CppSystem.Collections.Generic.Dictionary<int, string>> OverrideReplaceTextCallback { get; }

        public BuiltDialogPackage(DialogPackage package, Dictionary<int, string> overrideTexts)
        {
            Package = package;
            OverrideTexts = overrideTexts;
            OverrideReplaceTextCallback = replaceDict =>
            {
                foreach (var kvp in OverrideTexts)
                    replaceDict[kvp.Key] = kvp.Value;
            };
        }
    }

    public static bool ExistsDialogPackage(string name)
    {
        return _dialogPackageConfigs.ContainsKey(name);
    }

    public static DialogPackageConfig GetDialogPackage(string name)
    {
        if (_dialogPackageConfigs.TryGetValue(name, out var pkg))
            return pkg;

        return null;
    }

    public static DialogPackage GetBuiltDialogPackage(string name)
    {
        if (_builtDialogPackages.TryGetValue(name, out var built))
            return built.Package;

        Log.Warning($"Dialog package not built: {name}");
        return null;
    }

    private static DialogAction BuildDialogAction(DialogPackageConfig dialogPackageConfig, int dialogIndex, int actionIndex, DialogActionConfig actionConfig, Dictionary<int, string> overrideTexts, ref int nextVirtualTextId)
    {
        var action = new DialogAction();
        action.actionType = actionConfig.actionType;
        action.shouldSet = actionConfig.shouldSet;

        // Keep native dialog loading paths stable: all asset refs must be non-null.
        action.m_SpriteAsset = new AssetReferenceSprite("");
        action.m_SpriteENAsset = new AssetReferenceSprite("");
        action.m_SpriteJPAsset = new AssetReferenceSprite("");
        action.m_SpriteKOAsset = new AssetReferenceSprite("");
        action.m_SpriteCNTAsset = new AssetReferenceSprite("");
        action.m_MaterialAsset = new AssetReferenceT<UnityEngine.Material>("");
        action.m_BgmPackageAsset = new AssetReferenceT<GameData.Profile.LoopedBGMPackage>("");
        action.m_AudioAsset = new AssetReferenceT<UnityEngine.AudioClip>("");

        if (actionConfig.actionType == ActionType.CG || actionConfig.actionType == ActionType.BG)
            action.m_SpriteAsset = ResolveDialogSpriteReference(actionConfig);

        if (actionConfig.actionType == ActionType.Sound)
            action.m_AudioAsset = ResolveDialogAudioReference(actionConfig);

        if (actionConfig.actionType == ActionType.Branch)
            ConfigureBranchAction(action, dialogPackageConfig, dialogIndex, actionIndex, actionConfig, overrideTexts, ref nextVirtualTextId);

        if (actionConfig.actionType == ActionType.Goto)
            action.index = ResolveDialogJumpIndex(dialogPackageConfig, dialogIndex, actionConfig.index, "Goto");

        if (actionConfig.actionType == ActionType.End)
            action.index = actionConfig.exitCode ?? actionConfig.index ?? 0;

        return action;
    }

    private static void ConfigureBranchAction(DialogAction action, DialogPackageConfig dialogPackageConfig, int dialogIndex, int actionIndex, DialogActionConfig actionConfig, Dictionary<int, string> overrideTexts, ref int nextVirtualTextId)
    {
        var selections = new List<int>();
        var jumps = new List<int>();
        var prices = new List<int>();

        var options = actionConfig.options;
        if (options == null || options.Count == 0)
        {
            Log.Warning($"Dialog branch has no options: {dialogPackageConfig.name}[{dialogIndex + 1}] action #{actionIndex + 1}");
            action.selections = Array.Empty<int>();
            action.jumps = Array.Empty<int>();
            action.prices = Array.Empty<int>();
            return;
        }

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            var option = options[optionIndex];
            if (option == null)
            {
                Log.Warning($"Dialog branch option is null: {dialogPackageConfig.name}[{dialogIndex + 1}] action #{actionIndex + 1}, option #{optionIndex + 1}");
                continue;
            }

            var selectionTextId = nextVirtualTextId--;
            overrideTexts[selectionTextId] = option.text ?? "";
            var jump = ResolveDialogJumpIndex(dialogPackageConfig, dialogIndex, option.jump, $"Branch option #{optionIndex + 1}");
            var price = option.price ?? 0;
            if (price < 0)
            {
                Log.Warning($"Dialog branch option price is negative and will be treated as 0: {dialogPackageConfig.name}[{dialogIndex + 1}] action #{actionIndex + 1}, option #{optionIndex + 1}");
                price = 0;
            }

            selections.Add(selectionTextId);
            jumps.Add(jump);
            prices.Add(price);
        }

        action.selections = selections.ToArray();
        action.jumps = jumps.ToArray();
        action.prices = prices.ToArray();
    }

    private static int ResolveDialogJumpIndex(DialogPackageConfig dialogPackageConfig, int dialogIndex, int? targetIndex, string context)
    {
        var fallback = Math.Min(dialogIndex + 1, dialogPackageConfig.Count);
        if (!targetIndex.HasValue)
        {
            Log.Warning($"{context} target is missing in dialog package {dialogPackageConfig.name}[{dialogIndex + 1}], falling back to dialog #{fallback + 1}.");
            return fallback;
        }

        if (targetIndex.Value < 1 || targetIndex.Value > dialogPackageConfig.Count + 1)
        {
            Log.Warning($"{context} target dialog #{targetIndex.Value} is out of range in dialog package {dialogPackageConfig.name}[{dialogIndex + 1}], falling back to dialog #{fallback + 1}.");
            return fallback;
        }

        return targetIndex.Value - 1;
    }

    private static BuiltDialogPackage BuildDialogPackage(DialogPackageConfig dialogPackageConfig)
    {
        if (dialogPackageConfig == null)
        {
            Log.LogWarning("BuildDialogPackage called with null dialog package config.");
            return null;
        }

        var newDialogPackage = UnityEngine.ScriptableObject.CreateInstance<DialogPackage>();
        var length = dialogPackageConfig.Count;
        var newMeta = new Il2CppReferenceArray<DialogMeta>(length);
        var overrideTexts = new Dictionary<int, string>();
        var nextVirtualTextId = -1;

        for (int i = 0; i < length; i++)
        {
            var dialog = dialogPackageConfig[i];
            overrideTexts[i] = dialog.text;

            var meta = new DialogMeta();
            var si = new SpeakerIdentity();
            si.speakerType = dialog.characterType;
            si.speakerId = dialog.characterId;
            si.speakerPortrayalVariationId = dialog.pid;
            meta.speakerIdentity = si;

            meta.dialogId = i;
            meta.speakerPosition = dialog.position;

            if (dialog.actions != null && dialog.actions.Length > 0)
            {
                meta.dialogAction = new Il2CppReferenceArray<DialogAction>(dialog.actions.Length);
                for (int j = 0; j < dialog.actions.Length; j++)
                    meta.dialogAction[j] = BuildDialogAction(dialogPackageConfig, i, j, dialog.actions[j], overrideTexts, ref nextVirtualTextId);
            }
            else
            {
                meta.dialogAction = new Il2CppReferenceArray<DialogAction>(0);
            }

            meta.isSpeakInForeground = true;
            meta.isDark = false;
            meta.useNameInText = true;
            meta.useOverrideSprite = false;
            meta.m_OverrideSpriteAsset = null;

            newMeta[i] = meta;
        }

        newDialogPackage.dialogMeta = newMeta;
        newDialogPackage.name = dialogPackageConfig.name;

        return new BuiltDialogPackage(newDialogPackage, overrideTexts);
    }

    private static AssetReferenceSprite ResolveDialogSpriteReference(DialogActionConfig actionConfig)
    {
        if (actionConfig == null || string.IsNullOrEmpty(actionConfig.sprite))
            return new AssetReferenceSprite("");

        if (!TryGetSpriteReference(actionConfig.sprite, out var reference))
        {
            Log.LogWarning($"Dialog sprite URI is not registered in Addressables: {actionConfig.sprite}");
            return new AssetReferenceSprite("");
        }

        return reference;
    }

    private static AssetReferenceT<UnityEngine.AudioClip> ResolveDialogAudioReference(DialogActionConfig actionConfig)
    {
        if (actionConfig == null || string.IsNullOrEmpty(actionConfig.sound))
            return new AssetReferenceT<UnityEngine.AudioClip>("");

        if (!TryGetAudioReference(actionConfig.sound, out var reference))
        {
            Log.LogWarning($"Dialog sound URI is not registered in Addressables: {actionConfig.sound}");
            return new AssetReferenceT<UnityEngine.AudioClip>("");
        }

        return reference;
    }

    private static void BuildAndShowDialog(DialogPackageConfig dialogPackageConfig, System.Action onFinishCallback = null)
    {
        var builtDialogPackage = BuildDialogPackage(dialogPackageConfig);
        if (builtDialogPackage == null)
        {
            UniversalGameManager.OpenDialogMenu(
                null,
                onFinishCallback: onFinishCallback
            );
            return;
        }

        Log.LogInfo("Calling OpenDialogMenu...");
        UniversalGameManager.OpenDialogMenu(
            builtDialogPackage.Package,
            onFinishCallback: onFinishCallback,
            overrideReplaceTextCallback: builtDialogPackage.OverrideReplaceTextCallback,
            previousPanelVisualMode: 0
        );
    }

    private static void BuildAllDialogPackages()
    {
        foreach (var kvp in _dialogPackageConfigs)
        {
            _builtDialogPackages[kvp.Key] = BuildDialogPackage(kvp.Value);
            Log.Info($"Built dialog package: {kvp.Key}");
        }
    }

    private static void RegisterAllDialogPackages()
    {
        foreach (var kvp in _builtDialogPackages)
        {
            DataBaseDay.allDialogPackages[kvp.Key] = kvp.Value.Package;
            Log.Info($"Registered dialog package to DataBaseDay: {kvp.Key}");
        }
    }

    public static System.Action<Il2CppSystem.Collections.Generic.Dictionary<int, string>> GetOverrideReplaceTextCallback(GameData.Profile.DialogPackage dialogPackage)
    {
        if (dialogPackage == null) return null;

        string name;
        try
        {
            name = dialogPackage.name;
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrEmpty(name)) return null;

        if (_builtDialogPackages.TryGetValue(name, out var built))
            return built.OverrideReplaceTextCallback;

        return null;
    }

    public static void DumpExampleDialog()
    {
        Utils.FindAndProcessResources<DialogPackage>(dialogPackage =>
        {
            var packageName = dialogPackage.name;
            if (packageName == "OnTransitionToNight")
            {
                ExampleDialog = dialogPackage;
                Log.LogInfo("Stored ExampleDialog(OnTransitionToNight) package.");
            }
            Log.LogDebug($"id={dialogPackage.name}, package={packageName}");
        });

        if (ExampleDialog == null)
            Log.LogWarning("ExampleDialog(OnTransitionToNight) package not found among loaded assets.");
    }

    public static void ShowResourceExPackage(string packageName, System.Action onFinishCallback = null)
    {
        var dialogPackageConfig = GetDialogPackage(packageName);
        if (dialogPackageConfig != null)
        {
            BuildAndShowDialog(dialogPackageConfig, onFinishCallback);
        }
        else
        {
            Log.LogWarning($"Dialog package {packageName} not found in ResourceExManager.");
        }
    }
}
