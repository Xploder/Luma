namespace Luma;

internal sealed class WheelScrollState
{
    private bool hasPacket;
    private ulong lastTimestamp;
    private double target;

    internal bool TryAdvance(ulong timestamp, int delta, double currentOffset, double maximum, out double nextOffset)
    {
        nextOffset = target;
        if (delta == 0 || hasPacket && timestamp == lastTimestamp) return false;
        if (!hasPacket || timestamp < lastTimestamp || timestamp - lastTimestamp > 250_000)
            target = currentOffset;
        hasPacket = true;
        lastTimestamp = timestamp;
        target = Math.Clamp(target - delta / 120.0 * 48, 0, Math.Max(0, maximum));
        nextOffset = target;
        return true;
    }
}
