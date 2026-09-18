using System;

using MetaMystia.Multiplayer.Messages;

namespace MetaMystia.Multiplayer;

/// <summary>本地计时和房间延迟估算，存活检测由网络模块负责。</summary>
public static class RoomClock
{
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public static long Latency { get; private set; }
    private static long sent;
    private static int request;

    public static void Tick()
    {
        if (!GameSession.IsRoomClient || Now - sent < 3000) return;
        sent = Now;
        PingMessage.Send(++request);
    }

    public static void Receive(int id)
    {
        if (id != request || sent == 0) return;
        Latency = Math.Max(0, (Now - sent) / 2);
    }

    public static void Reset() { sent = 0; Latency = 0; }
}
