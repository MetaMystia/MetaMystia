using System;
using System.Collections.Generic;
using System.IO;

namespace MetaMystia.Network;

public sealed class PacketBuffer
{
    public const int MaxBodyLength = 1024 * 1024;
    private MemoryStream buffer = new();

    public void Write(byte[] data, int offset, int count)
    {
        buffer.Position = buffer.Length;
        buffer.Write(data, offset, count);
        buffer.Position = 0;
    }

    public List<NetPacket> ExtractPackets()
    {
        var packets = new List<NetPacket>();
        while (true)
        {
            if (buffer.Length - buffer.Position < 4) break;
            byte[] lenBytes = new byte[4];
            buffer.Read(lenBytes, 0, 4);
            int bodyLength = BitConverter.ToInt32(lenBytes, 0);
            if (bodyLength <= 0 || bodyLength > MaxBodyLength)
                throw new InvalidDataException($"Invalid packet body length: {bodyLength}");

            if (buffer.Length - buffer.Position < bodyLength)
            {
                buffer.Position -= 4;
                break;
            }
            byte[] body = new byte[bodyLength];
            buffer.Read(body, 0, bodyLength);
            packets.Add(NetPacket.FromBytes(body));
        }

        if (buffer.Position < buffer.Length)
        {
            byte[] leftover = buffer.ToArray()[(int)buffer.Position..];
            buffer = new MemoryStream();
            buffer.Write(leftover, 0, leftover.Length);
            buffer.Position = 0;
        }
        else
        {
            buffer = new MemoryStream();
        }

        return packets;
    }
}
