using System;
using System.Linq;

namespace MetaMystia;

public static class PlayerIdentity
{
    public static string Name { get => ConfigManager.GetPlayerId(); set => ConfigManager.SetPlayerId(value); }
    public static bool IsValid(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 64
        && name.All(c => c is not '<' and not '>' && !char.IsWhiteSpace(c) && !char.IsControl(c));
    public static string Sanitize(string name, string fallback = null)
    {
        var value = new string((name ?? "").Where(c => c is not '<' and not '>' && !char.IsWhiteSpace(c) && !char.IsControl(c)).Take(64).ToArray());
        return value.Length == 0 ? fallback ?? Environment.MachineName : value;
    }
}
