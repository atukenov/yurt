namespace Yurt.Infrastructure.Monitoring;

/// <summary>
/// Decides which errors become a message: one per kind of error per <c>RepeatMinutes</c>, and no more than
/// <c>MaxPerHour</c> in total. Whatever is held back is counted and mentioned in the next message that goes out.
/// Not thread-safe; used by the single alert sender loop.
/// </summary>
public sealed class AlertThrottle(int repeatMinutes, int maxPerHour)
{
    private readonly Dictionary<string, (DateTimeOffset LastSent, int Held)> _byKey = new();
    private readonly Queue<DateTimeOffset> _sentLastHour = new();
    private int _overCap;

    public sealed record Decision(bool Send, int RepeatsSinceLast, int SkippedOverCap);

    public Decision Check(string key, DateTimeOffset now)
    {
        while (_sentLastHour.Count > 0 && now - _sentLastHour.Peek() >= TimeSpan.FromHours(1))
            _sentLastHour.Dequeue();

        _byKey.TryGetValue(key, out var seen);
        if (seen.LastSent != default && now - seen.LastSent < TimeSpan.FromMinutes(repeatMinutes))
        {
            _byKey[key] = seen with { Held = seen.Held + 1 };
            return new Decision(false, 0, 0);
        }
        if (_sentLastHour.Count >= maxPerHour)
        {
            _overCap++;
            return new Decision(false, 0, 0);
        }

        var skipped = _overCap;
        _overCap = 0;
        _sentLastHour.Enqueue(now);
        _byKey[key] = (now, 0);
        if (_byKey.Count > 500) Prune(now);
        return new Decision(true, seen.Held, skipped);
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var stale in _byKey.Where(kv => now - kv.Value.LastSent > TimeSpan.FromMinutes(repeatMinutes)).Select(kv => kv.Key).ToList())
            _byKey.Remove(stale);
    }
}
