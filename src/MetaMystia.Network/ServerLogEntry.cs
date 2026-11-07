using System.Text.Encodings.Web;
using System.Text.Json;

namespace MetaMystia.Network;

public enum ServerLogLevel { Info, Warning, Error, Chat }
public sealed record ServerLogEntry(ServerLogLevel Level, string Message)
{
    private static readonly JsonSerializerOptions json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    // 保留中文，转义换行和终端控制字符，避免玩家文本伪造日志行。
    public static string Quote(string text) => JsonSerializer.Serialize(text, json);
}
