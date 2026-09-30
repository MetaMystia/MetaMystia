using System;
using System.Text.Json;

namespace MetaMystia.MetaLib;

internal static class SampleData
{
    internal const string ModuleA = "metalib.sample.a";
    internal const string ModuleB = "metalib.sample.b";
    internal const string ModuleLarge = "metalib.sample.large";
    internal const string ModuleCallback = "metalib.sample.callback";
    internal static readonly string[] ModuleIds = { ModuleA, ModuleB, ModuleLarge, ModuleCallback };

    internal static JsonElement Create(int counter) => JsonSerializer.SerializeToElement(new
    {
        counter,
        text = "夜雀食堂 / 中文 / 日本語 / 🐦 / \"引号\" / \\路径\n第二行\t制表符",
        integer = long.MaxValue,
        fraction = 0.1234567890123456789012345678m,
        numbers = new[] { -1, 0, 42 },
        nested = new { enabled = true, missing = (string?)null, magnitude = 1.5, sqrMagnitude = 2 },
        regexText = ",\n  \"magnitude\": 1.5,\n  \"sqrMagnitude\": 2",
        empty = Array.Empty<string>()
    });
}
