using System.Text.Json;

namespace MetaMystia.Gateway;

/// <summary>Gateway 数据目录下的 JSON 数组文件；先写临时文件再替换。</summary>
internal static class JsonFile
{
    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static T[] Read<T>(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<T[]>(File.ReadAllText(path), json) ?? throw new InvalidDataException("Invalid " + Path.GetFileName(path))
        : [];

    public static void Write<T>(string path, T[] values)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(values, json));
        File.Move(path + ".tmp", path, true);
    }
}
