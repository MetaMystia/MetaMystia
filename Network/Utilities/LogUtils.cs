using System.Reflection;
using System.Text.Json;
using MetaMystia.Protocol.Logging;
using MetaMystia.Protocol.Messages;

namespace MetaMystia.Network.Utilities;

[AutoLog]
public static partial class LogUtils
{
    private static JsonSerializerOptions _jsonSerializerOptions =
        new()
        {
            WriteIndented = false,
            IncludeFields = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

    public static string ToLogString(this NetworkMessage message)
    {
        return JsonSerializer.Serialize(message, message.GetType(), _jsonSerializerOptions);
    }

    private static void LogMessageInternal(string logStr, MessageLogLevel logLevel)
    {
        switch (logLevel)
        {
            case MessageLogLevel.Debug:
                Log.Debug(logStr, false);
                break;
            case MessageLogLevel.Warning:
                Log.Warning(logStr, false);
                break;
            case MessageLogLevel.Error:
                Log.Error(logStr, false);
                break;
            case MessageLogLevel.Fatal:
                Log.Fatal(logStr, false);
                break;
            case MessageLogLevel.Message:
                Log.Message(logStr, false);
                break;
            case MessageLogLevel.None:
            case MessageLogLevel.Info:
            case MessageLogLevel.All:
            default:
                Log.Info(logStr, false);
                break;
        }
    }

    public static void LogMessageReceived(NetworkMessage message)
    {
        var attr = message.GetType().GetCustomAttribute<MessageLogLevelAttribute>();
        var onReceiveLogOnlyAction = attr?.OnReceiveLogOnlyAction ?? false;
        var logStr = $"{MpManager.RoleTag} Received {message}{(onReceiveLogOnlyAction ? "" : $": {message.ToLogString()}")}";
        LogMessageInternal(logStr, attr?.OnReceive ?? MessageLogLevel.Info);
    }

    public static void LogMessageSent(NetworkMessage message)
    {
        var attr = message.GetType().GetCustomAttribute<MessageLogLevelAttribute>();
        var onSendLogOnlyAction = attr?.OnSendLogOnlyAction ?? false;
        var logStr = $"{MpManager.RoleTag} Sent {message}{(onSendLogOnlyAction ? "" : $": {message.ToLogString()}")}";
        LogMessageInternal(logStr, attr?.OnSend ?? MessageLogLevel.Info);
    }
}
