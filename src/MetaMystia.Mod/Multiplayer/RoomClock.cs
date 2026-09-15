using System;

using MetaMystia.Multiplayer.Actions;

namespace MetaMystia.Multiplayer;

/// <summary>备菜变更使用房主时钟，存活检测由网络模块负责。</summary>
public static class RoomClock
{
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public static long Latency { get; private set; }
    public static long Offset { get; private set; }
    public static long SynchronizedNow => Now - Offset;
    private static long sent;
    private static int request;

    public static void Tick()
    {
        if (!GameSession.IsRoomClient || Now - sent < 3000) return;
        sent = Now;
        PingAction.Send(++request);
    }

    public static void Receive(int id, long hostReceived)
    {
        if (id != request || sent == 0) return;
        Latency = Math.Max(0, (Now - sent) / 2);
        Offset = sent + Latency - hostReceived;
    }

    public static void Reset() { sent = 0; Latency = 0; Offset = 0; }
}
