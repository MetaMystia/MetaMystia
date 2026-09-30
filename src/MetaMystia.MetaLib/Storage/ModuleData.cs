using System.Text.Json;

namespace MetaMystia.MetaLib.Storage;

public sealed record ModuleData(int Version, JsonElement Data);
