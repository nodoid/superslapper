using System.Text.Json.Serialization;

namespace SuperSlapper.Core.Random;

public interface IRandomSource
{
    /// <summary>[0,1) with 32 bits of precision.</summary>
    double NextDouble();

    /// <summary>[minInclusive, maxExclusive)</summary>
    int Next(int minInclusive, int maxExclusive);

    /// <summary>True with the given probability (0..1).</summary>
    bool Chance(double probability);
}

/// <summary>
/// xorshift128+ generator. Deterministic across platforms and .NET versions,
/// which is what lets a saved career resume with the exact same future, and
/// lets the test suite pin random outcomes.
/// </summary>
public sealed class RandomSource : IRandomSource
{
    private ulong _s0;
    private ulong _s1;

    public RandomSource() : this(Environment.TickCount64)
    {
    }

    public RandomSource(long seed)
    {
        var state = unchecked((ulong)seed * 0x2545F4914F6CDD1DUL + 0x9E3779B97F4A7C15UL);
        _s0 = Mix(state);
        _s1 = Mix(state ^ 0xD1B54A32D192ED03UL);

        // xorshift128+ must never start all-zero.
        if (_s0 == 0 && _s1 == 0)
        {
            _s0 = 0x9E3779B97F4A7C15UL;
            _s1 = 0xBF58476D1CE4E5B9UL;
        }
    }

    public RandomSource(ulong s0, ulong s1)
    {
        _s0 = s0;
        _s1 = s1;
    }

    public long Seed { get; set; }

    [JsonIgnore]
    public ulong State0 => _s0;

    [JsonIgnore]
    public ulong State1 => _s1;

    public void Restore(ulong s0, ulong s1)
    {
        _s0 = s0;
        _s1 = s1;
    }

    private static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public ulong NextULong()
    {
        var s1 = _s0;
        var s0 = _s1;
        _s0 = s0;
        s1 ^= s1 << 23;
        _s1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
        return _s1 + s0;
    }

    public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

    public int Next(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        var range = (ulong)((long)maxExclusive - minInclusive);
        return (int)(minInclusive + (long)(NextULong() % range));
    }

    public bool Chance(double probability) => NextDouble() < probability;
}

/// <summary>A generator that returns a fixed script of values, for tests.</summary>
public sealed class ScriptedRandomSource : IRandomSource
{
    private readonly Queue<double> _values;

    public ScriptedRandomSource(IEnumerable<double> values) => _values = new Queue<double>(values);

    public ScriptedRandomSource(params double[] values) : this((IEnumerable<double>)values)
    {
    }

    public double NextDouble() => _values.Count > 0 ? _values.Dequeue() : 0.5;

    public int Next(int minInclusive, int maxExclusive) =>
        maxExclusive <= minInclusive
            ? minInclusive
            : minInclusive + (int)(NextDouble() * (maxExclusive - minInclusive));

    public bool Chance(double probability) => NextDouble() < probability;
}
