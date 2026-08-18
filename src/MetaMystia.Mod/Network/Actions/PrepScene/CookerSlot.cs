using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
public partial class CookerSlot
{
    public const int SlotsLength = 16; // TODO: 根据实际情况调整最大值，重构 PrepSceneManager 相关代码

    public int Id { get; set; } = -1;
    public long Timestamp { get; set; } = 0;

    public CookerSlot Clone()
    {
        return new CookerSlot
        {
            Id = Id,
            Timestamp = Timestamp,
        };
    }

    public static CookerSlot[] CreateDefaultArray()
    {
        var slots = new CookerSlot[SlotsLength];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new CookerSlot();
        }
        return slots;
    }
}
