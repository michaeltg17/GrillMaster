# Grill Master — how the planner thinks

The problem in one sentence: **we have a pile of meat pieces of different sizes, one grill that
holds 30 cm × 20 cm, and we want to cook everything in as few "rounds" (batches) as possible.**

A *round* is everything that sits on the grill at the same time. The rules:

- A piece must lie flat on the grill, never sticking out.
- Pieces must not overlap.
- A piece may be turned 90° (a 15×7 steak may be placed as 7×15).
- Every piece gets grilled exactly once.

The grill is 30 cm wide and 20 cm tall, so it is a grid of **600 one-centimetre squares**.
The total size of a piece is its *area*: length × width (a 15×7 steak covers 105 squares).
The absolute minimum number of rounds is therefore the meat's total area divided by 600,
rounded up. No plan can ever do better than that — it's just the floor.

There is a second floor, for menus full of one meat: if a menu has 40 sausages and a grill
can hold at most 30 of them, the sausages alone need 2 rounds — even if the *area* says one.
The planner reports the maximum of the two floors ("the lower bound"), and the per-type
part is computed exactly by a small cached single-round search (the machinery lives in
`src/GrillMaster.Application/Features/Plans/GrillPlannerHelpers.cs`).

There is one planner, `GrillPlanner`, and it works in two phases — like a cook who first lays
the meat down quickly, and then checks the arrangement until they can *prove* it is the best:

| Phase           | One-line idea                                                                              | Speed       | Role                                                                                     |
|-----------------|--------------------------------------------------------------------------------------------|-------------|------------------------------------------------------------------------------------------|
| greedy pre-pass | Put the biggest pieces down first, tuck each one into the tightest free spot.              | Fast (~2 ms) | Gives the search a starting "champion". When it already lands on the lower bound, the answer is proven with zero search. |
| exact search    | Try arrangements one by one, but skip millions of pointless ones with smart rules.         | Slower      | **Proves** the answer is the best possible (within its time budget).                     |

Each phase is explained in its own file, written for a person who has never coded or done
math — with pictures and worked examples:

- [greedy-prepass.md](greedy-prepass.md) — the quick, intuitive first pass (an internal
  optimization of the planner, not a planner of its own).
- [grill-planner.md](grill-planner.md) — the exact search that refuses to stop until it can
  *prove* it did its best.

## Configuration

The settings live in the `GrillMaster` section of
`src/GrillMaster.Console/appsettings.json`:

```json
{
  "GrillMaster": {
    "GrillMenuApiUrl": "http://isol-grillassessment.azurewebsites.net",
    "MaxNodes": 10000000,
    "VerboseLogging": true
  }
}
```

- `GrillMenuApiUrl` — the grill-menu API to fetch the menus from.
- `MaxNodes` — the exact search's hard node budget, shared between its fast pass and its
  verification pass and, in the default parallel mode, across all cores. When the budget runs
  out, the best plan found so far is returned, flagged as *not* proven.
- `EnableParallelism` — when `true` (the default) the exact search runs on every logical
  core; when `false` it stays strictly serial and fully deterministic, including the exact
  search-node count.
- `Parallelism` — the number of search threads when parallelism is enabled; `0` (the default)
  means every logical core.
- `VerboseLogging` — when `true`, each menu is logged with its proven-optimal status
  (`Menu 01: 4 rounds (Proven: False)`); when `false`, the plain line
  (`Menu 01: 4 rounds`) is logged.

Any setting can be overridden with an environment variable
(e.g. `GRILLMASTER__GRILLMENUAPIURL`, `GRILLMASTER__MAXNODES`), so you can point at another API
or change the budget without touching the file.

## Results on the live dataset (15 menus)

| Total rounds | Notes                                                              |
|--------------|--------------------------------------------------------------------|
| **38**       | proven on 14 menus (all at their floor); Menu 01 best-found, unproven |

The greedy pre-pass alone would use 39 rounds on this dataset; the search improves one menu
and proves the rest.

`37` is the sum of the per-menu lower bounds — the maximum of the area bound
(`ceil(totalArea / 600)`) and the per-type bound — so no solution can use fewer rounds; on
this dataset the area bound is the binding one. Most menus settle at the floor during the
pre-pass itself; Menu 01 (nine square centimetres of slack across three full rounds) defeats
the floor — the search finds the 4-round plan, and its verification pass (which re-checks
every possible position, because only that complete search is a real proof) cannot finish
within the default budget, so Menu 01 comes back flagged *not* proven. This is a genuinely
hard instance: the 3-round space is over 10 billion search nodes (a 10-billion-node run
across all 16 cores took about 23 minutes and still had not exhausted it), and an external
CP-SAT solver given ten minutes cannot prove 3 rounds impossible either. Whether 3 rounds
are actually possible is unknown.

## Honest corners

- **Axis-aligned placement only.** Pieces may be turned 90°, but not at an arbitrary angle.
- **The exact search has a node budget** (default 10 000 000), shared between its fast
  pass and its verification pass and, in the default parallel mode, across all cores. On
  this data it proves the optimum on 14 menus in well under a second; Menu 01 spends the
  whole budget (~10M nodes, ~2 s on 16 cores) and comes back flagged as *not* proven
  (`GrillPlan.IsProvenOptimal == false`). A much larger or adversarial menu would do the
  same.
- **Assumes every piece fits the grill.** The largest piece in this data is 22 cm, which fits
  the 30 cm side. A piece that cannot fit the grill in either orientation makes the planner
  throw an error instead of guessing.
