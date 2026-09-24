namespace GrillMaster.Verification.Cases;

/// <summary>
/// Deterministic splitmix64 PRNG for reproducible randomised fixtures. Deliberately not
/// <see cref="Random"/>: CA5394 flags it under the repo's all-rules-enabled analysis, and
/// cryptographic strength is not needed for test fixtures.
/// </summary>
internal sealed class SplitMix64(ulong seed)
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

/// <summary>
/// Builds the verification corpus. Every corpus is deterministic for a given seed, so a
/// failure is reproducible run after run. Three flavours:
/// <list type="bullet">
/// <item><see cref="SmallRandom"/> — tiny grills with a few pieces, cheap enough to run in CI.</item>
/// <item><see cref="EdgeCases"/> — hand-built cases that target the corners a random generator
/// misses: exact-fit pieces, many identical pieces, thin strips, tight tilings, same shape
/// under different names.</item>
/// <item><see cref="AttackRandom"/> — larger, denser instances for the explicit stress run,
/// aimed at the planner's pruning rules (symmetry breaking, lower bounds, rotation, ordering).</item>
/// </list>
/// All generated pieces are guaranteed to fit on an empty grill in at least one orientation,
/// which is the planner's input contract.
/// </summary>
public static class CaseGenerator
{
    private static readonly string[] Names =
    [
        "Steak", "Sausage", "Patty", "Wing", "Kebab", "Prawn", "Mushroom", "Pepper",
    ];

    private static readonly (int Length, int Width)[] SmallShapes =
    [
        (1, 1), (1, 2), (2, 2), (1, 3), (2, 3), (3, 3), (2, 4), (3, 4), (2, 5), (1, 4),
    ];

    private static readonly (int Length, int Width)[] AttackShapes =
    [
        (1, 1), (1, 2), (2, 2), (1, 3), (2, 3), (3, 3), (2, 4), (3, 4), (4, 4), (2, 5), (3, 5), (1, 4), (4, 2), (5, 3),
    ];

    /// <summary>
    /// Small random cases: grills of 5-9 by 4-7 centimetres with 2-6 pieces from a small shape
    /// pool. The pool deliberately reuses shapes and names so identical pieces — the
    /// symmetry-breaking case — occur frequently.
    /// </summary>
    public static IReadOnlyList<GrillTestCase> SmallRandom(int count, ulong seed)
    {
        var rng = new SplitMix64(seed);
        var cases = new List<GrillTestCase>(count);
        for (var i = 0; i < count; i++)
        {
            cases.Add(RandomCase(rng, rng.Next(5, 10), rng.Next(4, 8), minPieces: 2, maxPieces: 6, poolSize: 4, shapes: SmallShapes));
        }

        return cases;
    }

    /// <summary>
    /// Larger random cases for the explicit stress run: grills up to 14 by 11 centimetres with
    /// 4-8 pieces from a wider shape pool.
    /// </summary>
    public static IReadOnlyList<GrillTestCase> AttackRandom(int count, ulong seed)
    {
        var rng = new SplitMix64(seed);
        var cases = new List<GrillTestCase>(count);
        for (var i = 0; i < count; i++)
        {
            cases.Add(RandomCase(rng, rng.Next(6, 15), rng.Next(5, 12), minPieces: 4, maxPieces: 8, poolSize: 6, shapes: AttackShapes));
        }

        return cases;
    }

    /// <summary>
    /// Hand-built corner cases. Each builder is a separate probe at one known-subtle behaviour
    /// of exact packing, so a regression localises to a family instead of a random seed.
    /// </summary>
    public static IReadOnlyList<GrillTestCase> EdgeCases(ulong seed)
    {
        var rng = new SplitMix64(seed);
        var cases = new List<GrillTestCase>();

        // 1. A piece exactly the size of the grill: it fills the whole round and forces every
        // other piece into its own round.
        for (var i = 0; i < 3; i++)
        {
            var w = rng.Next(6, 11);
            var h = rng.Next(5, 9);
            var extras = Fillers(rng, w, h, rng.Next(2, 5));
            cases.Add(new GrillTestCase(w, h, [new CasePiece("Slab", w, h), .. extras]));
        }

        // 2. Many identical pieces: the symmetry-breaking rules must not prune the optimum.
        cases.Add(new GrillTestCase(8, 6, Enumerable.Repeat(new CasePiece("Patty", 2, 3), 8).ToList()));
        cases.Add(new GrillTestCase(8, 8, Enumerable.Repeat(new CasePiece("Steak", 3, 3), 5).ToList()));
        cases.Add(new GrillTestCase(10, 10, Enumerable.Repeat(new CasePiece("Patty", 4, 2), 9).ToList()));

        // 3. Thin strips in both orientations against a narrow grill.
        cases.Add(new GrillTestCase(6, 6,
        [
            .. Enumerable.Repeat(new CasePiece("Wing", 1, 5), 4),
            .. Enumerable.Repeat(new CasePiece("Patty", 2, 2), 2),
        ]));
        cases.Add(new GrillTestCase(10, 4,
        [
            .. Enumerable.Repeat(new CasePiece("Slab", 9, 3), 2),
            .. Enumerable.Repeat(new CasePiece("Patty", 2, 2), 3),
        ]));

        // 4. Squares only, mixed sizes.
        cases.Add(new GrillTestCase(7, 7,
        [
            .. Enumerable.Repeat(new CasePiece("Steak", 3, 3), 3),
            .. Enumerable.Repeat(new CasePiece("Patty", 2, 2), 2),
            .. Enumerable.Repeat(new CasePiece("Wing", 1, 1), 4),
        ]));
        cases.Add(new GrillTestCase(9, 9,
        [
            .. Enumerable.Repeat(new CasePiece("Slab", 4, 4), 4),
            .. Enumerable.Repeat(new CasePiece("Wing", 1, 1), 6),
        ]));

        // 5. Tiling pressure: the area lower bound is tight and geometry has to agree with it.
        // 3x(5x4) = 60 plus 2x(2x2) = 8 on a 30 cm^2 grill: area forces 3, but each 5x4 needs
        // its own round (two never share a 6x5 grill), so the true answer is well above the
        // area bound — the kind of gap a wrong bound or a wrong prune would hide.
        cases.Add(new GrillTestCase(6, 5,
        [
            .. Enumerable.Repeat(new CasePiece("Slab", 5, 4), 3),
            .. Enumerable.Repeat(new CasePiece("Patty", 2, 2), 2),
        ]));

        // 6. Same shape under different names: the planner treats these as distinct types,
        // which must not change the optimum.
        cases.Add(new GrillTestCase(8, 8,
        [
            .. Enumerable.Repeat(new CasePiece("Steak", 3, 2), 3),
            .. Enumerable.Repeat(new CasePiece("Patty", 3, 2), 3),
        ]));

        // 7. One piece that is exactly the grill, alone.
        cases.Add(new GrillTestCase(7, 5, [new CasePiece("Slab", 7, 5)]));

        return cases;
    }

    // Random pieces of random names, all fitting on an empty grill of the given size.
    private static GrillTestCase RandomCase(SplitMix64 rng, int width, int height, int minPieces, int maxPieces, int poolSize, (int Length, int Width)[] shapes)
    {
        var pool = PickShapes(rng, width, height, poolSize, shapes);
        var count = rng.Next(minPieces, maxPieces + 1);
        var pieces = new List<CasePiece>(count);
        for (var i = 0; i < count; i++)
        {
            var (length, widthShape) = pool[rng.Next(pool.Length)];
            var name = Names[rng.Next(Names.Length)];
            pieces.Add(new CasePiece(name, length, widthShape));
        }

        return new GrillTestCase(width, height, pieces);
    }

    // A few small filler pieces that fit beside the given grill size.
    private static List<CasePiece> Fillers(SplitMix64 rng, int width, int height, int count)
    {
        var pool = PickShapes(rng, width, height, poolSize: 3, shapes: SmallShapes);
        var pieces = new List<CasePiece>(count);
        for (var i = 0; i < count; i++)
        {
            var (length, widthShape) = pool[rng.Next(pool.Length)];
            var name = Names[rng.Next(Names.Length)];
            pieces.Add(new CasePiece(name, length, widthShape));
        }

        return pieces;
    }

    // poolSize distinct shapes from the pool that fit on an empty grill, chosen by a
    // deterministic shuffle (random order, fixed seed).
    private static (int Length, int Width)[] PickShapes(SplitMix64 rng, int width, int height, int poolSize, (int Length, int Width)[] shapes)
    {
        var fitting = shapes.Where(s => FitsOnEmptyGrill(s, width, height)).ToArray();
        return fitting
            .OrderBy(_ => rng.Next(int.MaxValue), Comparer<int>.Default)
            .Take(poolSize)
            .ToArray();
    }

    // Fits in at least one orientation on an empty grill of the given size.
    private static bool FitsOnEmptyGrill((int Length, int Width) shape, int width, int height) =>
        (shape.Length <= width && shape.Width <= height) ||
        (shape.Width <= width && shape.Length <= height);
}
