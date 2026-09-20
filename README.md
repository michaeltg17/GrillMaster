# Grill Master

A .NET / C# console application that helps the grill master **minimise the number of cooking
rounds** needed to grill every piece of meat on a set of menus, given a fixed-size grill.

This is the solution for the *isolutions "Assessment Grill Master"* task.

## Problem

The grill is a fixed **20 cm × 30 cm** surface. A REST API returns a number of **menus**; each
menu is a list of meat items, and each item has a size (`Length` × `Width` in cm) and a
`Quantity` (how many identical pieces must be grilled).

All pieces take the **same** cooking time, so the only thing that matters is **how many rounds**
are needed. In one round, any set of pieces that fit on the grill **without overlapping** can be
cooked at once. The goal is to place the pieces so that the number of rounds is as small as
possible.

This is a 2‑D rectangle **placement** problem (formally a bin‑packing problem: fit each menu's
rectangles onto fixed 20×30 grill surfaces and minimise their count), which is NP‑hard — so the app
ships three strategies with different speed/quality trade‑offs, all behind one interface.

## How it works

```
Program.cs (CLI + DI)
        │
        ▼
GrillOrchestrator ──► IGrillMenuClient ──► REST API  (GET /api/GrillMenu)
        │
        │  for each menu: expand items × quantity into pieces
        ▼
    IGrillPlanStrategy.Plan(pieces, grill)  ──►  GrillPlan (rounds of placements)
        │
        ▼
   ReportPrinter ──► console (one "<menu>: N rounds" line + "Total: N rounds")
```

- **Domain** (`src/GrillMaster.Domain`) — pure, dependency‑free models: `GrillSize`,
  `GrillPiece`, `GrillMenuItem`, `GrillMenu`, `GrillPiecePlacement`, `GrillRound`, `GrillPlan`.
- **Application** (`src/GrillMaster.Application`) — the application layer, organised by feature:
  `Features/Menus` (the grill‑menu API client `IGrillMenuApiClient` / `GrillMenuApiClient`, wire
  DTOs, and typed API exceptions) and `Features/Plans` (`IGrillPlanStrategy` and the three
  strategies, sharing a `RoundOccupancy` grid and skyline position search). `GrillOrchestrator`
  ties the two features together.
- **Console** (`src/GrillMaster.Console`) — the executable: `Program.cs` (CLI + DI/host wiring)
  and `appsettings.json`. The API client's `HttpClient` base address comes from configuration.

Pieces may be **rotated 90°** (both `L×W` and `W×L` are tried). Placement is **axis‑aligned and
non‑overlapping** (see [Known limitations](#known-limitations)).

## Getting started

Requires the **.NET 10 SDK**.

```bash
# restore + build everything
dotnet build GrillMaster.slnx

# run (default strategy: greedy)
dotnet run --project src/GrillMaster.Console -- greedy
```

### Command line

```
Usage:
  GrillMaster [<strategy>] [options]

Arguments:
   <strategy>  Grilling strategy (greedy | exact | optimized).

Options:
  -s, --strategy <strategy>  Grilling strategy (greedy | exact | optimized).
  -u, --url <url>            API base URL (overrides appsettings.json).
  -v, --verbose              Print the full per-round placement breakdown.
  -h, --help                 Show help and usage information.
  --version                  Show version information.
```

The strategy can be given as a positional argument (`GrillMaster exact`) or with
`--strategy`; when both are supplied, `--strategy` wins. Strategies:
`greedy` (best-fit shelf heuristic, default), `exact` (branch-and-bound, proves the
optimum), `optimized` (greedy seed + local-search consolidation).

The API base URL is read from `src/appsettings.json` (`Grill:GrillMenuApiUrl`) and can be overridden
with `--url` or the `GRILL__GRILLMENUAPIURL` environment variable.

## The three strategies

| Strategy    | Approach                                                                    | Speed   | Quality                                   |
|-------------|-----------------------------------------------------------------------------|---------|-------------------------------------------|
| `greedy`    | Sort pieces largest‑first; place each into the fullest round that fits.      | Fastest | Good, usually within 1–2 rounds of optimal |
| `exact`     | Branch‑and‑bound seeded with the greedy bound; skyline positions + symmetry breaking + area bound. | Slower  | **Proven optimum** (within node budget)    |
| `optimized` | Greedy seed + deterministic local search (bounded replan to drop a round).   | Fast    | Reaches the optimum on the assessment data |

All three reuse the same `RoundOccupancy` grid. `exact` and `optimized` rely on a **skyline
candidate‑position** search (only positions that cannot be shifted up/left are considered), which
is what keeps them tractable.

### Results on the live dataset (15 menus)

| Strategy    | Total rounds | Notes                                  |
|-------------|--------------|----------------------------------------|
| `greedy`    | 39           | fast baseline                          |
| `exact`     | **37**       | equals the area lower bound → optimal  |
| `optimized` | **37**       | matches the optimum via local search   |

`37` is the sum of the per‑menu area lower bounds (`ceil(totalArea / 600)`), so no solution can
use fewer rounds — `exact` proves it and `optimized` reaches it.

## Output

Default (summary) output matches the format requested in the brief:

```
Menu 04: 2 rounds
Menu 11: 1 rounds
Menu 03: 3 rounds
...
Total: 37 rounds
```

With `--verbose`, each round is expanded to show the exact placement of every piece:

```
Menu 04: 2 rounds
  Round 1 (22 pieces, 536 cm^2):
    - Veal 8x4 at (0,0)
    - Veal 8x4 at (8,0)
    - Paprika Sausage 6x3 at (24,0)
    ...
```

## Testing

Tests live in `tests/` and use **WireMock.Net** to stand up a local HTTP server that mimics the
grill API — no real network calls and no in‑memory HTTP fakes. Shared test infrastructure
(WireMock mocks, `LoggerScope`, `TestData`, the `grill-menus.json` fixture) lives in
`tests/GrillMaster.Core.Testing`, a classlib referenced by every test project.

The suite uses **xUnit v3**, which runs on the Microsoft Testing Platform (MTP) instead of VSTest.
Each test project is run directly with `dotnet run`:

```bash
dotnet run --project tests/GrillMaster.UnitTests
dotnet run --project tests/GrillMaster.IntegrationTests
dotnet run --project tests/GrillMaster.EndToEndTests
dotnet run --project tests/GrillMaster.PerformanceTests
```

> `dotnet test` is also wired up for MTP (`global.json` + `UseMicrosoftTestingPlatformRunner`),
> but on some .NET 10 SDK + xUnit v3 combinations it reports “zero tests” — the `dotnet run`
> commands above are the reliable way to run the suite.

Coverage includes:

- **API client** — parses menus/items/quantities, hits the right endpoint, handles empty menus and
  error responses.
- **Grilling invariants** (all three strategies) — every piece placed exactly once, all pieces
  within the grill, no overlaps, footprints match the piece (rotated or not).
- **Optimality** — `exact` is never worse than the heuristics; heuristics never beat the area
  lower bound; known‑optimum instances are solved correctly.
- **End‑to‑end** — the full pipeline (WireMock → client → grilling → report) produces the required
  per‑menu lines and a `Total:` equal to their sum.

## Project structure

```
src/
  GrillMaster.Domain/             pure models: GrillSize, GrillPiece, GrillMenuItem, GrillMenu,
                                  GrillPiecePlacement, GrillRound, GrillPlan
  GrillMaster.Application/        the application layer (namespace GrillMaster.Application)
    GrillOrchestrator.cs          fetch → plan each menu → print
    Features/
      Menus/                      IGrillMenuApiClient, GrillMenuApiClient
        Models/                   GrillMenuDto, GrillMenuItemDto
        Exceptions/               typed API exceptions
      Plans/                      IGrillPlanStrategy, RoundOccupancy, GrillPlanHelpers,
                                  GrillPlanStrategyFactory
        Strategies/               GreedyShelfStrategy, ExactBacktrackingStrategy,
                                  OptimizedHeuristicStrategy
  GrillMaster.Console/            the executable
    Program.cs                    CLI (System.CommandLine) + host/DI wiring
    appsettings.json              default API base URL, strategy, verbose flag
tests/
  GrillMaster.Core.Testing/       shared test classlib (no tests of its own)
    Infra/                        WireMock mocks (ApiMock, GrillMenuApiMock) + LoggerScope
    Core/                         TestCaseSerializer
    TestData.cs                   loads the grill-menus.json fixture
    grill-menus.json              fixture payload (the live API's 15-menu response)
  GrillMaster.UnitTests/
    GrillingInvariantsTests.cs    validity invariants for all strategies
    GrillingOptimalityTests.cs    relative quality / known optima
  GrillMaster.IntegrationTests/
    GrillMenuApiClientTests.cs    WireMock-based client tests
  GrillMaster.EndToEndTests/
    EndToEndTests.cs              full pipeline via WireMock
  GrillMaster.PerformanceTests/
    Performance/                  GrillingBenchmarkTests, PerfBaseline, baseline.json
```

## Known limitations

- **Axis‑aligned placement only.** Pieces may be rotated 90°, but arbitrary (non‑right‑angle)
  rotation is not supported. Allowing free angles would be a continuous 2‑D placement problem and is
  well beyond what this assessment needs; the current model is the standard, tractable one.
- **`exact` has a node budget** (default 20 000 000). On the assessment data it finishes in well
  under a second and proves the optimum. On a much larger or adversarial menu it may hit the
   budget and then returns the best incumbent found so far, flagged as **not** proven optimal
   (`GrillPlan.IsProvenOptimal == false`).
- **Assumes every piece fits the grill.** The current data's largest piece is 22 cm, which fits on
  the 30 cm side. A piece that cannot fit the grill in either orientation throws
  `InvalidOperationException`.
