#nullable enable
using System.Text;

namespace MetaMystia.ConsoleSystem;

/// <summary>TMP 富文本辅助：转义外部文本、提取纯文本。</summary>
public static class ConsoleText
{
    private const string NoParseOpen = "<noparse>";
    private const string NoParseClose = "</noparse>";

    /// <summary>把玩家输入等外部文本包进 noparse，阻止富文本注入。</summary>
    public static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.IndexOf('<') < 0) return text;
        return NoParseOpen + text.Replace(NoParseClose, "</no​parse>") + NoParseClose;
    }

    /// <summary>去掉富文本标签，noparse 内的内容原样保留。</summary>
    public static string Strip(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            if (Match(text, i, NoParseOpen))
            {
                int start = i + NoParseOpen.Length;
                int end = text.IndexOf(NoParseClose, start, System.StringComparison.Ordinal);
                if (end < 0) end = text.Length;
                sb.Append(text, start, end - start);
                i = end + NoParseClose.Length;
                continue;
            }
            if (text[i] == '<')
            {
                int close = text.IndexOf('>', i + 1);
                int nextOpen = text.IndexOf('<', i + 1);
                if (close > i + 1 && (nextOpen < 0 || close < nextOpen))
                {
                    i = close + 1;
                    continue;
                }
            }
            if (text[i] != '​') sb.Append(text[i]);
            i++;
        }
        return sb.ToString();
    }

    private static bool Match(string text, int index, string token) =>
        string.CompareOrdinal(text, index, token, 0, token.Length) == 0;
}
