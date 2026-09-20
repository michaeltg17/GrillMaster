# Agent guidance

## Build & test
- Build: `dotnet build GrillMaster.slnx`
- Run (default strategy: greedy): `dotnet run --project src/GrillMaster.Console -- greedy`
- Tests: each test project is an MTP project — run it directly:
  - `dotnet run --project tests/GrillMaster.UnitTests`
  - `dotnet run --project tests/GrillMaster.IntegrationTests`
  - `dotnet run --project tests/GrillMaster.EndToEndTests`
  - `dotnet run --project tests/GrillMaster.PerformanceTests`
- `tests/GrillMaster.Core.Testing` is a shared classlib (WireMock mocks, `LoggerScope`, `TestData`, the `grill-menus.json` fixture) referenced by the test projects; it has no tests of its own.
- Do NOT use `dotnet test` — its MTP mode is broken with xunit v3 on this SDK (exits non-zero, runs 0 tests).

## Analyzer rules
- `Directory.Build.props` sets `AnalysisMode=AllEnabledByDefault` and `TreatWarningsAsErrors=true`.
- Keep the build at **0 warnings**; any new warning fails the build.
- Suppressions live in `.editorconfig` (IDE0130, CA1724, CA1062, CA1814, CA1859, CA1002, CA2227, CA1849, CA13xx, IDE03xx, …). Add new ones there, not in code.

## Layout
- `src/` — three projects.
  - `GrillMaster.Domain/` — pure, dependency-free models (`GrillSize`, `GrillPiece`, `GrillMenuItem`, `GrillMenu`, `GrillPiecePlacement`, `GrillRound`, `GrillPlan`); namespace `GrillMaster.Domain`.
  - `GrillMaster.Application/` — the application layer; namespace `GrillMaster.Application`. `Features/Menus/` = grill menu API client + wire DTOs + typed API exceptions (namespaces `GrillMaster.Application.Features.Menus[.Models|.Exceptions]`); `Features/Plans/` = grilling strategies + engine (namespaces `GrillMaster.Application.Features.Plans[.Strategies]`); `GrillOrchestrator.cs` at the project root.
  - `GrillMaster.Console/` — the executable: `Program.cs` (CLI + DI/host wiring) + `appsettings.json`; namespace `GrillMaster`.
- Dependency chain: `Console` → `Application` → `Domain`.
- Logging is Serilog: the console sink uses template `{Message:lj}{NewLine}` (no timestamp/level) with an all-white `SystemConsoleTheme` (every `ConsoleThemeStyle` → white foreground), so console output is plain white report lines. `GrillOrchestrator` logs one `{MenuName}: {RoundCount} rounds` event per menu (menus processed in name order) plus a `Total: {TotalRounds} rounds` event.
- `tests/` — xunit v3 on MTP. `GrillMaster.Core.Testing/` is the shared classlib: `Infra/` holds WireMock mocks and `LoggerScope` (per-test Serilog scope: in-memory sink + xUnit test output; assert via `sink.Should().HaveMessage(...)` from `Serilog.Sinks.InMemory.Assertions`), `Core/` holds `TestCaseSerializer`, and `grill-menus.json` is the real 15-menu API fixture (exposed via `TestData.GrillMenusJson`). The four test projects (`UnitTests`, `IntegrationTests`, `EndToEndTests`, `PerformanceTests`) each reference it.

## Conventions
- Grilling strategies implement `IGrillPlanStrategy` and are selected by name; the 30×20 cm grill is the fixed frame.
- API failures surface as typed exceptions (`src/GrillMaster.Application/Features/Menus/Exceptions/`); the mock base is `GrillMaster.Core.Testing/Infra/ApiMock` (owns/disposes the `WireMockServer`, exposes `Url` as `Uri`).
- Namespaces follow the project/folder layout (IDE0130 is suppressed, so they are not forced to match the solution-relative path).

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
