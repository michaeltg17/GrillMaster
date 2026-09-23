namespace GrillMaster.UnitTests.Helpers;

/// <summary>
/// Deterministic splitmix64 PRNG for reproducible randomised test fixtures. Deliberately not
/// <see cref="Random"/>: CA5394 flags it as insecure under the repo's all-rules-enabled
/// analysis, and cryptographic strength is not needed for test fixtures.
/// </summary>
internal struct SplitMix64(ulong seed)
{
    private ulong _state = seed;

    public int Next(int maxExclusive)
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (int)(z % (ulong)maxExclusive);
    }

    public int Next(int minInclusive, int maxExclusive) => minInclusive + Next(maxExclusive - minInclusive);
}
