using System.Linq;
using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
public partial class SellableFoodData
{
    public WireSellableType Type { get; set; }
    public int Id { get; set; }
    public int Level { get; set; }
    public int[] ModifierIds { get; set; } = []; // 附加原料
    public int[] AdditiveTags { get; set; } = [];
    public int CookId { get; set; }

    /// <summary>
    /// 按内容比较两个 <see cref="SellableFoodData"/> 是否相等（用于联机冲突仲裁，不放入字典/集合）。
    /// </summary>
    public static bool ContentEquals(SellableFoodData a, SellableFoodData b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.Type == b.Type
            && a.Id == b.Id
            && a.Level == b.Level
            && a.CookId == b.CookId
            && (a.ModifierIds ?? []).SequenceEqual(b.ModifierIds ?? [])
            && (a.AdditiveTags ?? []).SequenceEqual(b.AdditiveTags ?? []);
    }
}
