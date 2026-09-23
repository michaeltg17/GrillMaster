# Grill Master — how the eight planners think

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
rounded up. No planner can ever do better than that — it's just the floor.

There is a second floor, for menus full of one meat: if a menu has 40 sausages and a grill
can hold at most 30 of them, the sausages alone need 2 rounds — even if the *area* says one.
Every planner reports the maximum of the two floors ("the lower bound"), and the per-type
part is computed exactly by a small cached search (see [the batch planner](batch-planner.md)
for the walkthrough).

There are eight planners. They are like eight different cooks with the same ingredients:

| Planner     | One-line idea                                                                    | Speed    | Quality                                                                  |
|-------------|----------------------------------------------------------------------------------|----------|--------------------------------------------------------------------------|
| `greedy`    | Put the biggest pieces down first, tuck each one into the tightest free spot.   | Fast     | Very good, but it can be 1–2 rounds off the best and it can't prove it.  |
| `exact`     | Try arrangements one by one, but skip millions of pointless ones with smart rules. | Slower   | **Proves** the answer is the best possible (within its time budget).     |
| `optimized` | Take `greedy`'s answer, then keep asking "can we do it with one fewer round?".  | Fast     | Within a round of the best found on all our menus.                       |
| `maxrects`  | Track the largest free rectangles and place each piece at the corner that wastes least. | Fastest  | Good (ties `greedy` on our fixture), different packing style.            |
| `guillotine`| Only cut pieces out of the corners of free rectangles — straight cuts only.     | Fastest  | Ties `greedy` on our fixture, with tiny bookkeeping.                     |
| `batch`     | Grill each meat type separately: proven full grills per type, then mix leftovers. | Fast     | Weakest on our mixed menus; shines on single-type menus.                 |
| `ortools`   | Write the problem as maths and let a professional IP solver (CP-SAT) cook it.   | Slow (30 s cap per menu) | A second, independent exact engine; proves some menus.         |
| `portfolio` | Run all the other planners and keep the best plan.                              | Fast*    | The best of everything; 14 of 15 menus proven optimal (*one menu takes a minute). |

Each planner is explained in its own file, written for a person who has never coded or done
math — with pictures and worked examples:

- [greedy-planner.md](greedy-planner.md) — the quick, intuitive cook.
- [exact-planner.md](exact-planner.md) — the cook who refuses to stop until they can *prove* they did their best.
- [optimized-planner.md](optimized-planner.md) — the cook who starts with a good guess and keeps improving it.
- [maxrects-planner.md](maxrects-planner.md) — the cook who keeps a map of the biggest free spaces.
- [guillotine-planner.md](guillotine-planner.md) — the cook who only makes straight corner cuts.
- [batch-planner.md](batch-planner.md) — the cook who never mixes the meats.
- [ortools-planner.md](ortools-planner.md) — the cook who hires a professional integer-programming specialist.
- [portfolio-planner.md](portfolio-planner.md) — hire all the cooks, serve the best plate.

## Picking one (the configuration)

The program runs one planner at a time; you choose it in
`src/GrillMaster.Console/appsettings.json`:

```json
{
  "GrillMaster": {
    "GrillMenuApiUrl": "http://isol-grillassessment.azurewebsites.net",
    "Planner": "greedy"
  }
}
```

`"Planner"` is any of the eight names above. Any setting can be overridden with an
environment variable (e.g. `GRILLMASTER__GRILLMENUAPIURL`, `GRILLMASTER__PLANNER`), so you
can try a planner without touching the file.

- `greedy` is the default (fastest, very good).
- `optimized` is the best everyday choice: 39 rounds on the full 15-menu fixture in
  about 2 ms per menu — one round off the best found on two menus.
- `portfolio` runs `greedy`, `optimized`, `exact` and `ortools` (cheapest first) and keeps
  the best result — 38 rounds, a couple of milliseconds per menu on 14 menus; the 15th
  (Menu 01) spends the exact budget and ortools' cap and still comes back unproven.
- `ortools` alone is the "let the specialist think" option — up to 30 seconds per menu.

## Results on the live dataset (15 menus)

| Planner      | Total rounds | Notes                                                    |
|--------------|--------------|----------------------------------------------------------|
| `greedy`     | 39           | fast baseline                                            |
| `exact`      | **38**       | floor on 14 menus; Menu 01 hits the node budget, unproven |
| `optimized`  | 39           | floor on 13 menus; one off on Menu 01 and Menu 07       |
| `maxrects`   | 39           | ~0.02 ms/menu                                            |
| `guillotine` | 39           | ~0.02 ms/menu, lightest bookkeeping                      |
| `batch`      | 62           | per-type full grills; weakest on these mixed menus       |
| `ortools`    | 38           | 30 s cap per menu; proves 4 of 15 menus; excluded from the perf suite |
| `portfolio`  | **38**       | best of all members; 14 of 15 proven, ~21M search nodes on Menu 01 |

`37` is the sum of the per-menu lower bounds — the maximum of the area bound
(`ceil(totalArea / 600)`) and the per-type bound — so no solution can use fewer rounds; on
this dataset the area bound is the binding one. Every planner reaches it on most menus;
Menu 01 (nine square centimetres of slack across three full rounds) defeats the floor,
and the best anyone found is 38 — `exact` and `ortools` agree on that 4-round plan,
though neither can yet prove three is impossible.

## Honest corners

- **Axis-aligned placement only.** Pieces may be turned 90°, but not at an arbitrary angle.
- **`exact` has a node budget** (default 20 000 000). On this data it proves the optimum on
  14 of 15 menus in well under a second, but Menu 01 exhausts the whole budget (about
  30 s) and comes back flagged as *not* proven (`GrillPlan.IsProvenOptimal == false`). A
  much larger or adversarial menu would do the same. `ortools` behaves the same way with
  its 30-second cap.
- **Assumes every piece fits the grill.** The largest piece in this data is 22 cm, which fits
  the 30 cm side. A piece that cannot fit the grill in either orientation makes the planner
  throw an error instead of guessing.
