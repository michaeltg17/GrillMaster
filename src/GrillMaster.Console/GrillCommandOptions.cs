namespace GrillMaster;

/// <summary>
/// Raw command-line values, before any configuration defaults are applied. Null means the
/// option was not specified on the command line; <see cref="HostBuilder"/> applies the non-null
/// values as configuration overrides.
/// </summary>
internal sealed record GrillCommandOptions(string? Planner, string? Url, bool? Verbose);
