using GrillMaster.Application.Settings;

namespace GrillMaster.UnitTests.Helpers;

/// <summary>
/// Planner settings for the tests: the API URL and logging do not affect planning, so only the
/// node budget and the search mode vary. Serial is the default.
/// </summary>
internal sealed record TestGrillSettings(long MaxNodes = 1_000_000, bool EnableParallelism = false) : IGrillMasterSettings
{
    public Uri GrillMenuApiUrl => new("http://localhost");

    public bool VerboseLogging => false;
}
