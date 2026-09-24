using GrillMaster.Verification.Cases;

namespace GrillMaster.Verification.Shrink;

/// <summary>
/// Delta-debugging minimizer for failing cases: repeatedly tries to drop one piece at a time
/// and keeps a drop when the discrepancy survives, until no single piece can be removed
/// anymore. The result is a minimal-by-piece-count counterexample, which is what you actually
/// want to paste into a regression test.
/// </summary>
public static class CaseShrinker
{
    public static GrillTestCase Shrink(GrillTestCase failing, Func<GrillTestCase, bool> stillFails)
    {
        var current = failing;
        var changed = true;
        while (changed && current.PieceCount > 1)
        {
            changed = false;
            for (var i = 0; i < current.Pieces.Count; i++)
            {
                var reduced = current with { Pieces = current.Pieces.Where((_, j) => j != i).ToList() };
                if (stillFails(reduced))
                {
                    current = reduced;
                    changed = true;
                    break;
                }
            }
        }

        return current;
    }
}
