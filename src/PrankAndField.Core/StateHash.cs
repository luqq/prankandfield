namespace PrankAndField.Core;

/// <summary>
/// FNV-1a over the raw bits of each value: the accumulator of the lockstep checksum. Every piece of
/// state that can change the outcome of a credit goes through it, so two peers that drift apart notice.
/// </summary>
public struct StateHash
{
    public ulong Value = 14695981039346656037UL;

    public StateHash() { }

    public void Add(long v)
    {
        for (int i = 0; i < 8; i++)
        {
            Value ^= (byte)(v >> (8 * i));
            Value *= 1099511628211UL;
        }
    }

    public void Add(int v) => Add((long)v);
    public void Add(uint v) => Add((long)v);
    public void Add(bool v) => Add(v ? 1L : 0L);
    public void Add(double v) => Add(BitConverter.DoubleToInt64Bits(v));
    public void Add(double? v) => Add(v ?? double.NaN);
}
