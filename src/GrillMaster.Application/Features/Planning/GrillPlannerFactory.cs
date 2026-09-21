using GrillMaster.CrossCutting.Settings;

namespace GrillMaster.Application.Features.Planning;

/// <summary>
/// Resolves the grilling planner selected by <see cref="IGrillMasterSettings.Planner"/> out of the
/// planner implementations registered in DI, matched by <see cref="IGrillPlanner.Name"/>.
/// </summary>
public sealed class GrillPlannerFactory(
    IEnumerable<IGrillPlanner> planners,
    IGrillMasterSettings settings)
{
    private readonly IReadOnlyList<IGrillPlanner> _planners = planners.ToList();

    /// <summary>All planner names, in display order (the registration order).</summary>
    public IReadOnlyList<string> Available => _planners.Select(p => p.Name).ToList();

    /// <summary>The planner selected by <see cref="IGrillMasterSettings.Planner"/>.</summary>
    public IGrillPlanner Current => Create(settings.Planner);

    private IGrillPlanner Create(string name) =>
        _planners.FirstOrDefault(p => string.Equals(p.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException(
                $"Unknown planner '{name}'. Available: {string.Join(", ", Available)}.", nameof(name));
}
