#nullable enable

namespace MetaMystia.MystiaLib
{
    public static class Bootstrap
    {
        public static class Log
        {
            public static readonly List<string> Warnings = [];
            public static void LogWarning(string message) => Warnings.Add(message);
        }
    }
}

namespace MetaMystia.MystiaLib.Saving
{
    internal static class SaveCarrier
    {
        private static string[]? _current, _seen;

        /// <summary>当前存档文件会得到的记录。</summary>
        public static string[] Saved => [.. _current!];
        public static int Writes;

        /// <summary>读档：换成新的载体对象。</summary>
        public static void Load(params string[] records) => _current = [.. records, .. new string[0]];

        public static bool TryTake([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string[]? records)
        {
            records = null;
            if (_current == null || ReferenceEquals(_current, _seen)) return false;
            records = _seen = _current;
            return true;
        }

        public static void Write(string[] records)
        {
            Writes++;
            _current = _seen = records;
        }
    }
}
