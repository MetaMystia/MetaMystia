using System;
using MemoryPack;

namespace MetaMystia.Network;

/// <summary>线层信封：每帧恰好承载一个 <see cref="NetAction"/>。</summary>
[MemoryPackable]
public partial class NetPacket
{
    public NetAction Action { get; set; }

    public byte[] ToBytesWithLength()
    {
        byte[] body = MemoryPackSerializer.Serialize(this);
        byte[] result = new byte[4 + body.Length];
        BitConverter.GetBytes(body.Length).CopyTo(result, 0);
        Buffer.BlockCopy(body, 0, result, 4, body.Length);
        return result;
    }

    public static NetPacket FromBytes(byte[] data) =>
        MemoryPackSerializer.Deserialize<NetPacket>(data)!;

    public static NetPacket FromAction(NetAction action) => new(action);

    public NetPacket(NetAction action) => Action = action;
}
