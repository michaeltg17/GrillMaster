# Agent guidance

## Build & test
- Build: `dotnet build GrillMaster.slnx`
- Tests: `dotnet run --project tests/GrillMaster.Tests`
- Do NOT use `dotnet test` — its MTP mode is broken with xunit v3 on this SDK (exits non-zero, runs 0 tests).

## Analyzer rules
- `Directory.Build.props` sets `AnalysisMode=AllEnabledByDefault` and `TreatWarningsAsErrors=true`.
- Keep the build at **0 warnings**; any new warning fails the build.
- Suppressions live in `.editorconfig` (IDE0130, CA1062, CA1814, CA1859, CA1002, CA2227, CA1849, CA13xx, IDE03xx, …). Add new ones there, not in code.

## Layout
- `src/` — app. `Api/` (client + typed exceptions), `Domain/`, `Packing/` (strategies + engine), `GrillOrchestrator.cs`, `Program.cs`.
- Logging is Serilog: the console sink uses template `{Message:lj}{NewLine}` (no timestamp/level), so console output is plain report lines. `GrillOrchestrator` logs one `{MenuName}: {RoundCount} rounds` event per menu (menus processed in name order) plus a `Total: {TotalRounds} rounds` event.
- `tests/GrillMaster.Tests/` — xunit v3. `Infra/` holds WireMock mocks and `LoggerScope` (per-test Serilog scope: in-memory sink + xUnit test output; assert via `sink.Should().HaveMessage(...)` from `Serilog.Sinks.InMemory.Assertions`). `grill-menus.json` is the real 15-menu API fixture (exposed via `TestData.GrillMenusJson`).

## Conventions
- Packing strategies implement `IPackStrategy` and are selected by name; the 30×20 cm grill is the fixed frame.
- API failures surface as typed exceptions (`src/Api/ApiExceptions.cs`); the mock base is `Infra/ApiMock` (owns/disposes the `WireMockServer`, exposes `Url` as `Uri`).
