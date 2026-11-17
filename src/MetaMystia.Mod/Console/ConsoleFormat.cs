using System.Text;
using System.Text.RegularExpressions;

namespace MetaMystia.ConsoleSystem;

/// <summary>
/// 控制台输出的 TMP 富文本格式。
/// </summary>
public static class ConsoleFormat
{
    /// <summary>将 &lt; 和 &gt; 换成形近字符，避免被解析为富文本标签。</summary>
    private static string EscapeRichText(string text) => text.Replace("<", "‹").Replace(">", "›");

    public static string Cmd(string text) => $"<color=#7CC8FF>{text}</color>";
    public static string Arg(string text) => $"<color=#E2B97F>{EscapeRichText(text)}</color>";
    public static string Current(string text) => $"<b><u><color=#FFD480>{EscapeRichText(text)}</color></u></b>";
    public static string Dim(string text) => $"<color=#8C8698>{text}</color>";
    public static string Warn(string text) => $"<color=#FFB060>{text}</color>";
    public static string Err(string text) => $"<color=#FF7B7B>{text}</color>";
    public static string Ok(string text) => $"<color=#7EE0A0>{text}</color>";
    public static string Event(string text) => $"<color=#FFD480>{text}</color>";
    public static string Line => "<color=#4A4556>────────────────────────────────</color>";
    public static string Header(string title) => $"<color=#4A4556>── </color><color=#C9C2D6>{title}</color><color=#4A4556> ──</color>";

    public static string SubCmd(string cmd, string args, string desc)
        => string.IsNullOrEmpty(args)
            ? $"  {Cmd(cmd)} {Dim("— " + desc)}"
            : $"  {Cmd(cmd)} {args} {Dim("— " + desc)}";

    /// <summary>不可信的纯文本（聊天）：禁用富文本并去掉控制字符。</summary>
    public static string Plain(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char c in text)
            builder.Append(char.IsControl(c) ? ' ' : c);
        return $"<noparse>{Regex.Replace(builder.ToString(), "</noparse", "</ noparse", RegexOptions.IgnoreCase)}</noparse>";
    }
}
