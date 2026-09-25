using GrillMaster.Application.Settings;

namespace GrillMaster.UnitTests.Helpers;

/// <summary>
/// Planner settings for the tests: the API URL and logging do not affect planning, so only the
/// node budgets and the search mode vary. Serial and the composition-proof phase disabled are
/// the defaults.
/// </summary>
internal sealed record TestGrillSettings(long MaxNodes = 1_000_000, long CompositionProofNodes = 0, bool EnableParallelism = false, int Parallelism = 0) : IGrillMasterSettings
{
    public Uri GrillMenuApiUrl => new("http://localhost");

    public bool VerboseLogging => false;
}
