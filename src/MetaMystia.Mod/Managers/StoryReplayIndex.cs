using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using GameData.Core.Collections.DaySceneUtility;

namespace MetaMystia;

public static partial class StoryReplayIndex
{
    public const string RecentPack = "Recent";
    public const string ResourceExPack = "ResourceEx";

    private static readonly HashSet<string> GenericNpcPrefixes = new(StringComparer.Ordinal)
    {
        "Human", "Yokai", "Goblin", "Tengu", "Daiyousei", "Lunar", "Yousei",
    };

    private static readonly Regex DlcPrefix = new(@"^DLC(\d+)_(.+)$", RegexOptions.Compiled);
    private static readonly Regex LvPattern = new(@"_LV\d+_", RegexOptions.Compiled);
    private static readonly Regex StandalonePattern = new(@"^[A-Za-z]+_\d+$", RegexOptions.Compiled);

    // pack -> category -> group -> dialog names
    private static Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>> _gameTree = new();
    // packageName -> dialog names
    private static Dictionary<string, List<string>> _resourceExTree = new();
    private static bool _built;

    public static IReadOnlyList<string> Packs
    {
        get
        {
            EnsureBuilt();
            var packs = _gameTree.Keys.ToList();
            if (StoryReplayRecentHistory.Dialogs.Count > 0)
                packs.Add(RecentPack);
            if (_resourceExTree.Count > 0)
                packs.Add(ResourceExPack);
            return packs.OrderBy(PackSortKey).ToList();
        }
    }

    public static void Rebuild() => Build(force: true);

    public static IReadOnlyList<string> GetRecentDialogs() => StoryReplayRecentHistory.Dialogs;

    public static IReadOnlyList<string> GetCategories(string pack)
    {
        EnsureBuilt();
        if (pack == RecentPack)
            return Array.Empty<string>();
        if (pack == ResourceExPack)
            return _resourceExTree.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();

        return _gameTree.TryGetValue(pack, out var categories)
            ? categories.Keys.OrderBy(CategorySortKey).ToList()
            : Array.Empty<string>();
    }

    public static IReadOnlyList<string> GetGroups(string pack, string category)
    {
        EnsureBuilt();
        if (pack == ResourceExPack)
            return Array.Empty<string>();

        if (!_gameTree.TryGetValue(pack, out var categories) ||
            !categories.TryGetValue(category, out var groups))
            return Array.Empty<string>();

        return groups.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    public static IReadOnlyList<string> GetDialogs(string pack, string category, string group = null)
    {
        EnsureBuilt();
        if (pack == ResourceExPack)
        {
            return _resourceExTree.TryGetValue(category, out var dialogs)
                ? dialogs.OrderBy(x => x, StringComparer.Ordinal).ToList()
                : Array.Empty<string>();
        }

        if (group == null ||
            !_gameTree.TryGetValue(pack, out var categories) ||
            !categories.TryGetValue(category, out var groups) ||
            !groups.TryGetValue(group, out var list))
            return Array.Empty<string>();

        return list.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    public static string GetDialogDisplayTitle(string dialogName)
    {
        if (TryParseResourceEx(dialogName, out var package, out var path))
            return string.IsNullOrEmpty(path) ? package : path;

        return dialogName;
    }

    public static bool IsDialogAvailable(string dialogName) =>
        StoryReplayRecentHistory.IsKnownDialog(dialogName);

    private static void EnsureBuilt()
    {
        if (!_built)
            Build(force: false);
    }

    private static void Build(bool force)
    {
        if (_built && !force)
            return;

        _gameTree.Clear();
        _resourceExTree.Clear();

        foreach (var dialogName in DataBaseDay.allDialogPackages.Keys)
        {
            if (string.IsNullOrEmpty(dialogName))
                continue;

            if (dialogName.StartsWith("_"))
            {
                if (!TryParseResourceEx(dialogName, out var package, out _))
                    continue;
                if (!_resourceExTree.TryGetValue(package, out var list))
                {
                    list = new List<string>();
                    _resourceExTree[package] = list;
                }
                list.Add(dialogName);
                continue;
            }

            var (pack, category, group) = ClassifyGameDialog(dialogName);
            if (!_gameTree.TryGetValue(pack, out var categories))
            {
                categories = new Dictionary<string, Dictionary<string, List<string>>>();
                _gameTree[pack] = categories;
            }
            if (!categories.TryGetValue(category, out var groups))
            {
                groups = new Dictionary<string, List<string>>();
                categories[category] = groups;
            }
            if (!groups.TryGetValue(group, out var dialogs))
            {
                dialogs = new List<string>();
                groups[group] = dialogs;
            }
            dialogs.Add(dialogName);
        }

        _built = true;
    }

    private static (string pack, string category, string group) ClassifyGameDialog(string name)
    {
        var pack = "Core";
        var rest = name;

        var dlcMatch = DlcPrefix.Match(name);
        if (dlcMatch.Success)
        {
            pack = $"DLC{dlcMatch.Groups[1].Value}";
            rest = dlcMatch.Groups[2].Value;
        }

        var parts = rest.Split('_');
        if (parts.Length == 0)
            return (pack, "Other", name);

        var head = parts[0];

        if (head == "Kizuna" && parts.Length >= 2)
            return (pack, "Kizuna", parts[1]);

        if (head == "Main")
        {
            var group = parts.Length >= 3 ? $"{parts[1]}_{parts[2]}" : rest;
            if (parts.Length >= 4)
                group = $"{group}_{parts[3]}";
            return (pack, "Main", group);
        }

        if (head == "Side")
            return (pack, "Side", parts.Length >= 2 ? string.Join('_', parts.Skip(1)) : "(ungrouped)");

        if (head == "NormalNPC" && parts.Length >= 2)
            return (pack, "NormalNPC", parts[1]);

        if (head == "Partner" && parts.Length >= 2)
            return (pack, "Partner", parts[1]);

        if (name.EndsWith("_EA_Dialog", StringComparison.Ordinal))
            return (pack, "EA_Dialog", head);

        if (GenericNpcPrefixes.Contains(head))
            return (pack, "GenericNPC", head);

        if (head == "ContradictionMission")
            return (pack, "Mission", "ContradictionMission");

        if (head == "HakureiFestival" || rest.StartsWith("HakureiFestival_", StringComparison.Ordinal))
            return (pack, "Event", ClassifyEventGroup(rest));

        if (LvPattern.IsMatch(rest))
            return (pack, "SpecialGuest", head);

        if (StandalonePattern.IsMatch(name))
            return (pack, "Standalone", head);

        return (pack, "Other", head);
    }

    private static string ClassifyEventGroup(string rest)
    {
        // HakureiFestival_Normal_BambooForest_Tewi_00
        var parts = rest.Split('_');
        if (parts.Length >= 4)
            return $"{parts[2]}_{parts[3]}";
        if (parts.Length >= 3)
            return parts[2];
        return rest;
    }

    private static bool TryParseResourceEx(string name, out string package, out string path)
    {
        package = string.Empty;
        path = string.Empty;
        if (!name.StartsWith("_") || name.Length <= 1)
            return false;

        var body = name[1..];
        var split = body.IndexOf('_');
        if (split < 0)
        {
            package = body;
            return !string.IsNullOrEmpty(package);
        }

        package = body[..split];
        path = body[(split + 1)..];
        return !string.IsNullOrEmpty(package);
    }

    private static int PackSortKey(string pack) => pack switch
    {
        RecentPack => -1,
        "Core" => 0,
        ResourceExPack => 9,
        _ when pack.StartsWith("DLC", StringComparison.Ordinal) &&
               int.TryParse(pack.AsSpan(3), out var n) => n,
        _ => 99,
    };

    private static int CategorySortKey(string category) => category switch
    {
        "Kizuna" => 0,
        "Main" => 1,
        "Side" => 2,
        "Partner" => 3,
        "EA_Dialog" => 4,
        "SpecialGuest" => 5,
        "Event" => 6,
        "NormalNPC" => 7,
        "GenericNPC" => 8,
        "Mission" => 9,
        "Standalone" => 10,
        _ => 11,
    };
}
