namespace MetaMystia.Network;

public sealed record QueueActivity(int Received, int ReceivedMotion, int Sent, int SentMotion, long? LastPollMsAgo);

internal sealed class ClientQueueActivity
{
    private struct Bucket
    {
        internal long Time;
        internal int Received, ReceivedMotion, Sent, SentMotion;
    }

    // 同一毫秒的入队尝试共用一个槽，流量突增时也不会额外分配。
    private readonly Bucket[] buckets = new Bucket[1000];
    private long lastPoll = -1;

    internal void Poll() => Volatile.Write(ref lastPoll, Environment.TickCount64);

    internal void Record(Kind kind, bool sending)
    {
        lock (buckets)
        {
            long now = Environment.TickCount64;
            ref var bucket = ref buckets[now % buckets.Length];
            if (bucket.Time != now) bucket = new() { Time = now };
            bool motion = kind is Kind.Motion or Kind.RoomMotion;
            if (sending)
            {
                bucket.Sent++;
                if (motion) bucket.SentMotion++;
            }
            else
            {
                bucket.Received++;
                if (motion) bucket.ReceivedMotion++;
            }
        }
    }

    internal QueueActivity Capture()
    {
        lock (buckets)
        {
            long poll = Volatile.Read(ref lastPoll);
            long now = Environment.TickCount64;
            int received = 0, receivedMotion = 0, sent = 0, sentMotion = 0;
            foreach (ref readonly var bucket in buckets.AsSpan())
            {
                if (now - bucket.Time >= buckets.Length) continue;
                received += bucket.Received;
                receivedMotion += bucket.ReceivedMotion;
                sent += bucket.Sent;
                sentMotion += bucket.SentMotion;
            }
            return new(received, receivedMotion, sent, sentMotion, poll < 0 ? null : now - poll);
        }
    }
}
