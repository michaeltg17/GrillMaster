# Agent guidance

## Build & test
- Build: `dotnet build GrillMaster.slnx`
- Run (planner from `appsettings.json`): `dotnet run --project src/GrillMaster.Console`
- Tests: each test project is an MTP project — run it directly:
  - `dotnet run --project tests/GrillMaster.UnitTests`
  - `dotnet run --project tests/GrillMaster.IntegrationTests`
  - `dotnet run --project tests/GrillMaster.EndToEndTests`
  - `dotnet run --project tests/GrillMaster.PerformanceTests`
- `tests/GrillMaster.Core.Testing` is a shared classlib (WireMock mocks, `TestData`, the `grill-menus.json` fixture) referenced by the test projects; it has no tests of its own.
- Do NOT use `dotnet test` — its MTP mode is broken with xunit v3 on this SDK (exits non-zero, runs 0 tests).

## Analyzer rules
- `Directory.Build.props` sets `AnalysisMode=AllEnabledByDefault` and `TreatWarningsAsErrors=true`.
- Keep the build at **0 warnings**; any new warning fails the build.
- Suppressions live in `.editorconfig` (IDE0130, CA1724, CA1062, CA1814, CA1859, CA1002, CA2227, CA1849, CA13xx, IDE03xx, …). Add new ones there, not in code.

## Layout
- `src/` — four projects.
   - `GrillMaster.CrossCutting/` — crosscutting concerns shared by all hosts; namespace `GrillMaster.CrossCutting`. `Settings/` = `IGrillMasterSettings`/`GrillMasterSettings` (bound to the `GrillMaster` configuration section: `GrillMenuApiUrl`, `Planner`) + `IValidateOptions` validator; `DependencyConfigurator.AddCrossCuttingDependencies()` registers the options (validate-on-start) and exposes `IGrillMasterSettings` as a singleton.
  - `GrillMaster.Domain/` — pure, dependency-free models (`GrillSize`, `GrillPiece`, `GrillMenuItem`, `GrillMenu`, `GrillPiecePlacement`, `GrillRound`, `GrillPlan`); namespace `GrillMaster.Domain`.
   - `GrillMaster.Application/` — the application layer; namespace `GrillMaster.Application`. `Features/Menus/` = grill menu API client + wire DTOs (namespace `GrillMaster.Application.Features.Menus[.Models]`); `Features/Planning/` = grilling planners + engine (namespaces `GrillMaster.Application.Features.Planning[.Planners]`); `GrillMasterApp.cs` + `DependencyConfigurator.cs` at the project root — `AddApplicationDependencies()` registers the menu `HttpClient` + `GrillMenuService`, all three planners as `IGrillPlanner` singletons, `GrillPlannerFactory` and `GrillMasterApp`.
    - `GrillMaster.Console/` — the executable, namespace `GrillMaster`: `Program.cs` (`Run()` builds the host via `HostBuilder.CreateHost` and runs it with `host.RunAsync()`), `HostBuilder.cs` (the composition root — `CreateHost` builds the Generic Host, pins the content root to `AppContext.BaseDirectory` so `appsettings.json` loads from the app's own directory, then wires everything in DI via `AddLogging` (Serilog) and `AddAppServices` (crosscutting + application dependencies + the hosted service); `ConfigureConsoleLogging` sets the all-white console sink; there is no CLI, all settings come from configuration), `GrillMasterAppHostedService.cs` (runs the `GrillMasterApp` when the host starts, then stops the application so `host.RunAsync()` returns), `appsettings.json`.
- Dependency chain: `Console` → `Application` → `Domain`; `Application` → `GrillMaster.CrossCutting` (settings), `Console` → `GrillMaster.CrossCutting`.
- The host is built in `HostBuilder.CreateHost` — no runtime objects are passed into DI registrations; the whole application graph (`GrillMenuService`, the planners, `GrillMasterApp`) is constructor-injected. `GrillPlannerFactory` is itself DI-resolved (it receives `IEnumerable<IGrillPlanner>` + `IGrillMasterSettings`) and picks the planner by matching `IGrillPlanner.Name` against the configured `Planner` (its `Current` property; `Available` lists the registered planner names in registration order). The pipeline runs as the `GrillMasterAppHostedService` hosted service, so `host.RunAsync()` drives the whole app. Misconfiguration (bad URL, unknown planner) fails fast when the host starts (options validation + planner resolution).
- Logging is Serilog via `Serilog.Extensions.Hosting`: `HostBuilder.CreateHost` builds a per-host logger from the `Serilog` configuration section (`ReadFrom.Configuration`) plus the `configureLogging` hook, and registers it with `builder.Services.AddSerilog(logger, dispose: false)` (the `Log.Logger` static is never touched; `dispose: false` keeps the host shutdown from disposing the logger and its sinks). Serilog backs `Microsoft.Extensions.Logging`, so app code injects `ILogger<T>` (category = the type's full name). `builder.Logging.ClearProviders()` keeps the default providers from double-writing. The console app configures a console sink with template `{Message:lj}{NewLine}` (no timestamp/level) and an all-white `SystemConsoleTheme` (every `ConsoleThemeStyle` → white foreground), so console output is plain white report lines; the `Microsoft`/`System` level overrides (framework noise off) live in the `Serilog` section of `appsettings.json`. `GrillMasterApp` logs one `{MenuName}: {RoundCount} rounds` event per menu (menus processed in name order) plus a `Total: {TotalRounds} rounds` event.
- `tests/` — xunit v3 on MTP. `GrillMaster.Core.Testing/` is the shared classlib: `Infra/` holds the WireMock mocks, `Core/` holds `TestCaseSerializer`, and `grill-menus.json` is the real 15-menu API fixture (exposed via `TestData.GrillMenusJson`). The four test projects (`UnitTests`, `IntegrationTests`, `EndToEndTests`, `PerformanceTests`) each reference it. `EndToEndTests` references the `Console` project and runs the real pipeline through `Fixtures/GrillMasterFactory`, which creates a `GrillMasterApp` (the hosted app: exposes the per-test in-memory `Sink` and `RunAsync`) — the same `HostBuilder.CreateHost` as `Program`, with the planner and API base URL supplied as in-memory configuration, the API pointed at WireMock and logging routed to the in-memory sink + the xUnit test output (assert via `app.Sink.Should().HaveMessage(...)` from `Serilog.Sinks.InMemory.Assertions`).

## Conventions
- Grilling planners implement `IGrillPlanner` and are selected by name; the 30×20 cm grill is the fixed frame.
- No try/catch in the request path: API failures surface as the raw .NET exceptions (`HttpRequestException` for transport/non-2xx, `JsonException` for malformed bodies) and are let to reach the top (unhandled → non-zero exit). The mock base is `GrillMaster.Core.Testing/Infra/ApiMock` (owns/disposes the `WireMockServer`, exposes `Url` as `Uri`).
- Assertions use AwesomeAssertions (`Should()`), never xunit's `Assert` — in every test project.
- Namespaces follow the project/folder layout (IDE0130 is suppressed, so they are not forced to match the solution-relative path).
- Logging goes through `[LoggerMessage]` partial methods (e.g. `private static partial void LogX(ILogger<T> logger, ...)` with the message template in the attribute), not inline `logger.LogXxx(...)` calls.

## Branching & PR workflow (dev → main)

- **All work happens on the `dev` branch.** Commit directly to `dev`; do **not** create feature/topic branches that open a PR straight to `main`.
- `main` only ever changes via a merged **`dev` → `main`** PR. There is exactly one PR in flight at a time, from `dev` to `main`.
- The loop is: commit on `dev` → push `dev` → open (or update) the `dev` → `main` PR.

When creating or updating the `dev` → `main` PR:

1. **Always run `git fetch origin main` first** — this is critical. The local `main` branch is often outdated and will show stale committed changes as part of the diff if not refreshed.
2. Compare `origin/main..dev` to identify only the actual new changes.
3. Check if a PR already exists (use `github_list_pull_requests`).
4. If none exists, create one with an accurate title and description summarizing the changes.
5. If one exists, update its title and description to reflect the actual current diff.
6. **Stop there. Never merge the PR (no `gh pr merge`, no merge via the API).** The user reviews the diff and merges it themselves; merging on the user's behalf defeats the review.
