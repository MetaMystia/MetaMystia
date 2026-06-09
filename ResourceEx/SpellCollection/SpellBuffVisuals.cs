using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using GameData.CoreLanguage.Collections;
using NightScene.EventUtility;

namespace MetaMystia.ResourceEx.SpellCollection;

internal static class SpellBuffVisuals
{
    private const string DefaultBuffIconPattern = "rex://ResourceExample/assets/Buff/{0}_1.png";
    private static readonly Dictionary<int, Sprite> BuffIconCache = [];
    private static readonly HashSet<int> MissingBuffIcons = [];

    public static Sprite GetBuffIconOrFallback(int buffId, params string[] candidateUris)
    {
        if (BuffIconCache.TryGetValue(buffId, out var cached) && cached != null)
        {
            return cached;
        }

        if (!MissingBuffIcons.Contains(buffId))
        {
            foreach (var uri in GetCandidateUris(buffId, candidateUris))
            {
                if (ResourceExManager.TryGetSprite(uri, out var sprite) && sprite != null)
                {
                    BuffIconCache[buffId] = sprite;
                    return sprite;
                }
            }

            MissingBuffIcons.Add(buffId);
        }

        return DataBaseLanguage.BuffDescription[EventManager.BuffType.PhilosopherStone]?.Visual;
    }

    private static IEnumerable<string> GetCandidateUris(int buffId, string[] candidateUris)
    {
        foreach (var uri in candidateUris.Where(static x => !string.IsNullOrWhiteSpace(x)))
        {
            yield return uri;
        }

        yield return string.Format(DefaultBuffIconPattern, buffId);
    }
}
