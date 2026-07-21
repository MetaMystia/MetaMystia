using System.Collections.Generic;

using GameData.Core.Collections.DaySceneUtility;
using GameData.Profile;

namespace MetaMystia;

public static partial class StoryReplayRecentHistory
{
    private const int MaxEntries = 128;
    private static readonly List<string> History = new();
    private static readonly Dictionary<string, DialogPackage> LoadedPackages = new();

    public static IReadOnlyList<string> Dialogs => History;

    public static bool IsKnownDialog(string dialogName) =>
        !string.IsNullOrEmpty(dialogName) &&
        (LoadedPackages.ContainsKey(dialogName) ||
         DataBaseDay.allDialogPackages.ContainsKey(dialogName));

    public static bool TryResolvePackage(string dialogName, out DialogPackage package)
    {
        package = null;
        if (string.IsNullOrEmpty(dialogName))
            return false;

        if (LoadedPackages.TryGetValue(dialogName, out package) && package != null)
            return true;

        return DataBaseDay.allDialogPackages.TryGetValue(dialogName, out package) && package != null;
    }

    public static void Record(string dialogName)
    {
        if (string.IsNullOrEmpty(dialogName))
            return;

        History.Remove(dialogName);
        History.Insert(0, dialogName);

        if (History.Count > MaxEntries)
        {
            for (var i = MaxEntries; i < History.Count; i++)
                LoadedPackages.Remove(History[i]);
            History.RemoveRange(MaxEntries, History.Count - MaxEntries);
        }
    }

    public static void Record(DialogPackage dialogPackage)
    {
        if (dialogPackage == null)
            return;

        var name = dialogPackage.name;
        if (string.IsNullOrEmpty(name))
            name = ResolveDialogName(dialogPackage);
        if (string.IsNullOrEmpty(name))
            return;

        LoadedPackages[name] = dialogPackage;
        Record(name);
    }

    private static string ResolveDialogName(DialogPackage dialogPackage)
    {
        foreach (var pair in DataBaseDay.allDialogPackages)
        {
            if (ReferenceEquals(pair.Value, dialogPackage))
                return pair.Key;
        }

        foreach (var pair in LoadedPackages)
        {
            if (ReferenceEquals(pair.Value, dialogPackage))
                return pair.Key;
        }

        return null;
    }
}
