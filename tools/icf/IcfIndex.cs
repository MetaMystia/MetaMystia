using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>
/// 折叠表：同一 RVA 上有多个方法（含泛型实例）时，列出其中的方法定义，按 il2cpp 映像与 token 索引。
/// 来源是 Il2CppDumper 输出（DummyDll 需开启 DummyDllAddToken）。
/// </summary>
sealed class IcfIndex
{
    public const string Header = "rva\tshared\timage\ttoken\tmethod";

    public sealed record Entry(long Rva, int Shared, string Image, uint Token, string Method);

    readonly Dictionary<(string, uint), Entry> byKey = [];
    readonly Dictionary<long, List<Entry>> byRva = [];

    public static IcfIndex Load(string path)
    {
        var index = new IcfIndex();
        bool header = false;
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith('#'))
                continue;
            if (!header)
            {
                if (line != Header)
                    throw new InvalidDataException($"{path}: 表头不是 {Header}");
                header = true;
                continue;
            }
            var f = line.Split('\t');
            var entry = new Entry(Convert.ToInt64(f[0], 16), int.Parse(f[1]), f[2], Convert.ToUInt32(f[3], 16), f[4]);
            index.byKey[(entry.Image, entry.Token)] = entry;
            if (!index.byRva.TryGetValue(entry.Rva, out var list))
                index.byRva[entry.Rva] = list = [];
            list.Add(entry);
        }
        return index;
    }

    public Entry Find(string image, uint token) => byKey.GetValueOrDefault((image, token));

    public IReadOnlyList<Entry> At(long rva) => byRva[rva];

    public static int Generate(string dumperDir, TextWriter output)
    {
        var shared = CountByRva(Path.Combine(dumperDir, "script.json"));
        var entries = new List<Entry>();
        foreach (var path in Directory.GetFiles(Path.Combine(dumperDir, "DummyDll"), "*.dll"))
            entries.AddRange(ReadDummyDll(path, shared));
        if (entries.Count == 0)
            throw new InvalidDataException("DummyDll 中没有带 Token 与 RVA 的折叠方法，确认 Il2CppDumper 开启了 DummyDllAddToken");

        output.NewLine = "\n";
        output.WriteLine("# ICF 折叠表：同一 RVA 上有多个方法时列出其中的方法定义，shared 为该 RVA 上的方法数（含泛型实例）。");
        output.WriteLine("# 由 tools/icf index 从 Il2CppDumper 输出生成。");
        foreach (var file in new[] { "GameAssembly.dll", "global-metadata.dat" })
        {
            var path = Path.Combine(dumperDir, file);
            if (File.Exists(path))
                output.WriteLine($"# {file} sha256 {Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))}");
        }
        output.WriteLine(Header);
        foreach (var e in entries.OrderBy(e => e.Rva).ThenBy(e => e.Image, StringComparer.Ordinal).ThenBy(e => e.Token))
            output.WriteLine($"0x{e.Rva:x}\t{e.Shared}\t{e.Image}\t0x{e.Token:x8}\t{e.Method}");

        Console.Error.WriteLine($"折叠 RVA {shared.Count(kv => kv.Value > 1)} 个，方法定义 {entries.Count} 个");
        return 0;
    }

    /// <summary>script.json 中每个 RVA 上的不同方法数（名称与签名均相同的视为同一个）。</summary>
    static Dictionary<long, int> CountByRva(string scriptJson)
    {
        var names = new Dictionary<long, HashSet<string>>();
        using var stream = File.OpenRead(scriptJson);
        using var doc = JsonDocument.Parse(stream);
        foreach (var m in doc.RootElement.GetProperty("ScriptMethod").EnumerateArray())
        {
            var rva = m.GetProperty("Address").GetInt64();
            if (!names.TryGetValue(rva, out var set))
                names[rva] = set = [];
            set.Add(m.GetProperty("Name").GetString() + "\n" + m.GetProperty("Signature").GetString());
        }
        return names.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
    }

    static IEnumerable<Entry> ReadDummyDll(string path, Dictionary<long, int> shared)
    {
        using var pe = new PEReader(File.OpenRead(path));
        var md = pe.GetMetadataReader();
        var image = Path.GetFileName(path);
        var result = new List<Entry>();
        foreach (var handle in md.MethodDefinitions)
        {
            var method = md.GetMethodDefinition(handle);
            long rva = -1;
            uint token = 0;
            foreach (var attributeHandle in method.GetCustomAttributes())
            {
                var attribute = md.GetCustomAttribute(attributeHandle);
                var name = AttributeName(md, attribute);
                if (name is not ("AddressAttribute" or "TokenAttribute"))
                    continue;
                foreach (var (field, value) in NamedStrings(md, attribute))
                {
                    if (name == "AddressAttribute" && field == "RVA")
                        rva = Convert.ToInt64(value, 16);
                    else if (name == "TokenAttribute" && field == "Token")
                        token = Convert.ToUInt32(value, 16);
                }
            }
            if (token != 0 && rva >= 0 && shared.GetValueOrDefault(rva) > 1)
                result.Add(new Entry(rva, shared[rva], image, token, $"{TypeName(md, method.GetDeclaringType())}::{md.GetString(method.Name)}"));
        }
        return result;
    }

    static string TypeName(MetadataReader md, TypeDefinitionHandle handle)
    {
        var type = md.GetTypeDefinition(handle);
        var name = md.GetString(type.Name);
        var outer = type.GetDeclaringType();
        if (!outer.IsNil)
            return TypeName(md, outer) + "." + name;
        var ns = md.GetString(type.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    static string AttributeName(MetadataReader md, CustomAttribute attribute)
    {
        EntityHandle type = attribute.Constructor.Kind switch
        {
            HandleKind.MethodDefinition => md.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
            HandleKind.MemberReference => md.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
            _ => default,
        };
        return type.Kind switch
        {
            HandleKind.TypeDefinition => md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)type).Name),
            HandleKind.TypeReference => md.GetString(md.GetTypeReference((TypeReferenceHandle)type).Name),
            _ => null,
        };
    }

    /// <summary>读取无参构造的特性中字符串类型的命名参数。</summary>
    static List<(string, string)> NamedStrings(MetadataReader md, CustomAttribute attribute)
    {
        var result = new List<(string, string)>();
        var reader = md.GetBlobReader(attribute.Value);
        if (reader.ReadUInt16() != 1)
            return result;
        int count = reader.ReadUInt16();
        for (int i = 0; i < count; i++)
        {
            reader.ReadByte();
            if (reader.ReadByte() != (byte)SerializationTypeCode.String)
                break;
            var name = reader.ReadSerializedString();
            result.Add((name, reader.ReadSerializedString()));
        }
        return result;
    }
}
