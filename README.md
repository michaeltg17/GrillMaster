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

This is a 2‑D rectangle **bin‑packing** problem (pack each menu's rectangles into 20×30 bins and
minimise the bin count), which is NP‑hard — so the app ships three strategies with different
speed/quality trade‑offs, all behind one interface.

## How it works

```
Program.cs (CLI + DI)
        │
        ▼
GrillOrchestrator ──► IGrillMenuClient ──► REST API  (GET /api/GrillMenu)
        │
        │  for each menu: expand items × quantity into pieces
        ▼
   IPackStrategy.Pack(pieces, grill)  ──►  PackResult (rounds of placements)
        │
        ▼
   ReportPrinter ──► console (one "<menu>: N rounds" line + "Total: N rounds")
```

- **Domain** (`src/GrillMaster/Domain`) — pure, dependency‑free models: `GrillSize`,
  `GrillPiece`, `GrillMenuItem`, `GrillMenu`, `Placement`, `Round`, `PackResult`.
- **Api** (`src/GrillMaster/Api`) — `IGrillMenuClient` / `GrillMenuClient` (an `HttpClient`
  whose base address comes from configuration) plus wire DTOs.
- **Packing** (`src/GrillMaster/Packing`) — `IPackStrategy` and the three strategies, sharing a
  `RoundOccupancy` grid and skyline position search.
- **Output** (`src/GrillMaster/Output`) — `ReportPrinter` (writes to any `TextWriter`).

Pieces may be **rotated 90°** (both `L×W` and `W×L` are tried). Placement is **axis‑aligned and
non‑overlapping** (see [Known limitations](#known-limitations)).

## Getting started

Requires the **.NET 10 SDK**.

```bash
# restore + build everything
dotnet build GrillMaster.slnx

# run (default strategy: greedy)
dotnet run --project src/GrillMaster -- greedy
```

### Command line

```
GrillMaster [strategy] [options]

Strategies:
  greedy     Best-fit shelf heuristic (fast, near-optimal). Default.
  exact      Branch-and-bound search (proves the optimum).
  optimized  Greedy seed + local-search consolidation.

Options:
  -s, --strategy <name>   Packing strategy (greedy | exact | optimized).
  -u, --url <baseUrl>     API base URL (overrides appsettings.json).
  -v, --verbose           Print the full per-round placement breakdown.
  -h, --help              Show this help.
```

The API base URL is read from `src/appsettings.json` (`Grill:ApiBaseUrl`) and can be overridden
with `--url` or the `GRILL__APIBASEURL` environment variable.

## The three strategies

| Strategy    | Approach                                                                    | Speed   | Quality                                   |
|-------------|-----------------------------------------------------------------------------|---------|-------------------------------------------|
| `greedy`    | Sort pieces largest‑first; place each into the fullest round that fits.      | Fastest | Good, usually within 1–2 rounds of optimal |
| `exact`     | Branch‑and‑bound seeded with the greedy bound; skyline positions + symmetry breaking + area bound. | Slower  | **Proven optimum** (within node budget)    |
| `optimized` | Greedy seed + deterministic local search (bounded repack to drop a round).   | Fast    | Reaches the optimum on the assessment data |

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

Tests live in `tests/GrillMaster.Tests` and use **WireMock.Net** to stand up a local HTTP server
that mimics the grill API — no real network calls and no in‑memory HTTP fakes.

The suite uses **xUnit v3**, which runs on the Microsoft Testing Platform (MTP) instead of VSTest.
Run it directly with:

```bash
dotnet run --project tests/GrillMaster.Tests
```

> `dotnet test` is also wired up for MTP (`global.json` + `UseMicrosoftTestingPlatformRunner`),
> but on some .NET 10 SDK + xUnit v3 combinations it reports “zero tests” — the `dotnet run`
> command above is the reliable way to run the suite.

Coverage includes:

- **API client** — parses menus/items/quantities, hits the right endpoint, handles empty menus and
  error responses.
- **Packing invariants** (all three strategies) — every piece placed exactly once, all pieces
  within the grill, no overlaps, footprints match the piece (rotated or not).
- **Optimality** — `exact` is never worse than the heuristics; heuristics never beat the area
  lower bound; known‑optimum instances are solved correctly.
- **End‑to‑end** — the full pipeline (WireMock → client → packing → report) produces the required
  per‑menu lines and a `Total:` equal to their sum.

## Project structure

```
src/GrillMaster/
  Program.cs                      CLI parsing + host/DI wiring
  appsettings.json                default API base URL, strategy, verbose flag
  DependencyInjection.cs          AddGrillMaster(...)
  GrillOrchestrator.cs            fetch → pack each menu → print
  Domain/                         GrillSize, GrillPiece, GrillMenuItem, GrillMenu,
                                  Placement, Round, PackResult
  Api/                            IGrillMenuClient, GrillMenuClient, GrillMenuDtos
  Packing/                        IPackStrategy, RoundOccupancy, PackingHelpers,
                                  GreedyShelfStrategy, ExactBacktrackingStrategy,
                                  OptimizedHeuristicStrategy, PackStrategyFactory
  Output/                         ReportPrinter
tests/GrillMaster.Tests/
  GrillMenuClientTests.cs         WireMock-based client tests
  PackingInvariantsTests.cs       validity invariants for all strategies
  PackingOptimalityTests.cs       relative quality / known optima
   EndToEndTests.cs                full pipeline via WireMock
   grill-menus.json                fixture payload (the live API's 15-menu response)
```

## Known limitations

- **Axis‑aligned placement only.** Pieces may be rotated 90°, but arbitrary (non‑right‑angle)
  rotation is not supported. Allowing free angles would be a continuous 2‑D packing problem and is
  well beyond what this assessment needs; the current model is the standard, tractable one.
- **`exact` has a node budget** (default 20 000 000). On the assessment data it finishes in well
  under a second and proves the optimum. On a much larger or adversarial menu it may hit the
  budget and then returns the best incumbent found so far, flagged as **not** proven optimal
  (`PackResult.IsProvenOptimal == false`).
- **Assumes every piece fits the grill.** The current data's largest piece is 22 cm, which fits on
  the 30 cm side. A piece that cannot fit the grill in either orientation throws
  `InvalidOperationException`.
