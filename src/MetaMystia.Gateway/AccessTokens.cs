using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace MetaMystia.Gateway;

public sealed record Caller(string Id, bool Admin);
public sealed record TokenRecord(string Id, string Name, string Hash, DateTimeOffset CreatedAt);
public sealed record TokenInfo(string Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt);
public sealed record TokenRequest(string? Name);
public sealed record TokenCreated(string Id, string Name, string Token);

/// <summary>管理令牌来自启动配置；只读令牌由管理令牌签发，文件只保存 SHA-256，明文仅在创建时返回一次。</summary>
public sealed class AccessTokens
{
    private const int MaxTokens = 100;
    private readonly SemaphoreSlim gate = new(1);
    private readonly ConcurrentDictionary<string, DateTimeOffset> lastUsed = new();
    private readonly byte[] adminHash;
    private readonly string path;
    private Dictionary<string, TokenRecord> records;

    public AccessTokens(string path, string adminToken)
    {
        this.path = Path.GetFullPath(path);
        adminHash = Hash(adminToken);
        records = JsonFile.Read<TokenRecord>(this.path).ToDictionary(t => t.Hash);
    }

    public Caller? Authenticate(string authorization)
    {
        if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
        var hash = Hash(authorization["Bearer ".Length..]);
        if (CryptographicOperations.FixedTimeEquals(hash, adminHash)) return new("admin", true);
        if (!Volatile.Read(ref records).TryGetValue(Convert.ToHexString(hash), out var record)) return null;
        lastUsed[record.Id] = DateTimeOffset.UtcNow;
        return new(record.Id, false);
    }

    public TokenInfo[] List() => Volatile.Read(ref records).Values.OrderBy(t => t.CreatedAt)
        .Select(t => new TokenInfo(t.Id, t.Name, t.CreatedAt, lastUsed.TryGetValue(t.Id, out var time) ? time : null)).ToArray();

    public async Task<TokenCreated> CreateAsync(string? name, CancellationToken token)
    {
        name = name?.Trim() ?? "";
        if (name.Length is 0 or > 64 || name.Any(char.IsControl)) throw new ArgumentException("Invalid token name");
        var secret = "mmr_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var record = new TokenRecord(Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant(), name,
            Convert.ToHexString(Hash(secret)), DateTimeOffset.UtcNow);
        await gate.WaitAsync(token);
        try
        {
            if (records.Count >= MaxTokens) throw new InvalidOperationException("Too many tokens");
            Save(new(records) { [record.Hash] = record });
        }
        finally { gate.Release(); }
        return new(record.Id, record.Name, secret);
    }

    public async Task DeleteAsync(string id, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var record = records.Values.SingleOrDefault(t => t.Id == id) ?? throw new KeyNotFoundException();
            var next = new Dictionary<string, TokenRecord>(records);
            next.Remove(record.Hash);
            Save(next);
            lastUsed.TryRemove(id, out _);
        }
        finally { gate.Release(); }
    }

    private void Save(Dictionary<string, TokenRecord> next)
    {
        JsonFile.Write(path, next.Values.ToArray());
        Volatile.Write(ref records, next);
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
