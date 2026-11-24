using System.Text.Encodings.Web;
using System.Text.Json;

namespace MetaMystia.Logging;

public static class LogSanitizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // 保留中文，转义换行和终端控制字符，避免外部文本伪造日志行。
    public static string Quote(string? text) => JsonSerializer.Serialize(text ?? string.Empty, JsonOptions);
}
