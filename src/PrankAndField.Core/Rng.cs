namespace PrankAndField.Core;

/// <summary>Small deterministic xorshift generator so simulations are reproducible in tests.</summary>
public sealed class Rng
{
    private uint _state;

    public Rng(uint seed = 0x9E3779B9u)
    {
        _state = seed == 0 ? 0x9E3779B9u : seed;
    }

    public uint NextUInt()
    {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x;
    }

    /// <summary>Uniform double in [0, 1).</summary>
    public double NextDouble() => (NextUInt() >> 8) / (double)(1u << 24);

    public double Range(double min, double max) => min + (max - min) * NextDouble();

    public int Next(int maxExclusive) => (int)(NextUInt() % (uint)maxExclusive);
}
