namespace MetaMystia.ConsoleSystem;

/// <summary>
/// Rich text formatting helpers for in-game console output (IMGUI).
/// </summary>
public static class ConsoleFormat
{
    /// <summary>Escape &lt; and &gt; that would conflict with IMGUI rich text tags.</summary>
    private static string EscapeRichText(string text) => text.Replace("<", "\u2039").Replace(">", "\u203A");

    public static string Cmd(string text) => $"<color=#66CCFF>{text}</color>";
    public static string Arg(string text) => $"<color=#FFCC66>{EscapeRichText(text)}</color>";
    public static string Dim(string text) => $"<color=#888899>{text}</color>";
    public static string Warn(string text) => $"<color=#FFAA44>{text}</color>";
    public static string Err(string text) => $"<color=#FF6666>{text}</color>";
    public static string Ok(string text) => $"<color=#66FF88>{text}</color>";
    public static string Line => $"<color=#444455>────────────────────────────────────</color>";
    public static string Header(string title) => $"<color=#444455>── </color><color=#AABBDD>{title}</color><color=#444455> ──────────────────────────</color>";

    public static string SubCmd(string cmd, string args, string desc)
        => string.IsNullOrEmpty(args)
            ? $"  {Cmd(cmd)} {Dim("─")} {Dim(desc)}"
            : $"  {Cmd(cmd)} {Arg(args)} {Dim("─")} {Dim(desc)}";
}
