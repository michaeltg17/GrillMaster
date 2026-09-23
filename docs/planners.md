# Grill Master — how the two planners think

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
part is computed exactly by a small cached single-round search (the machinery lives in
`src/GrillMaster.Application/Features/Plans/GrillPlannerHelpers.cs`).

There are two planners. They are like two different cooks with the same ingredients:

| Planner | One-line idea                                                                    | Speed  | Quality                                                                  |
|---------|----------------------------------------------------------------------------------|--------|--------------------------------------------------------------------------|
| `greedy`| Put the biggest pieces down first, tuck each one into the tightest free spot.   | Fast   | Very good, but it can be 1–2 rounds off the best and it can't prove it.  |
| `exact` | Try arrangements one by one, but skip millions of pointless ones with smart rules. | Slower | **Proves** the answer is the best possible (within its time budget).     |

Each planner is explained in its own file, written for a person who has never coded or done
math — with pictures and worked examples:

- [greedy-planner.md](greedy-planner.md) — the quick, intuitive cook.
- [exact-planner.md](exact-planner.md) — the cook who refuses to stop until they can *prove* they did their best.

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

`"Planner"` is either of the two names above. Any setting can be overridden with an
environment variable (e.g. `GRILLMASTER__GRILLMENUAPIURL`, `GRILLMASTER__PLANNER`), so you
can try a planner without touching the file.

- `greedy` is the default (fastest, very good).
- `exact` proves the answer is the best possible within its node budget: on this data it
  proves 14 of 15 menus in well under a second, but Menu 01 spends the whole budget
  (about 30 s) and comes back unproven.

## Results on the live dataset (15 menus)

| Planner  | Total rounds | Notes                                                    |
|----------|--------------|----------------------------------------------------------|
| `greedy` | 39           | fast baseline                                            |
| `exact`  | **38**       | floor on 14 menus; Menu 01 hits the node budget, unproven |

`37` is the sum of the per-menu lower bounds — the maximum of the area bound
(`ceil(totalArea / 600)`) and the per-type bound — so no solution can use fewer rounds; on
this dataset the area bound is the binding one. Both planners reach it on most menus;
Menu 01 (nine square centimetres of slack across three full rounds) defeats the floor,
and the best found is 38 — `exact` finds that 4-round plan, though it can not yet prove
three is impossible.

## Honest corners

- **Axis-aligned placement only.** Pieces may be turned 90°, but not at an arbitrary angle.
- **`exact` has a node budget** (default 20 000 000). On this data it proves the optimum on
  14 of 15 menus in well under a second, but Menu 01 exhausts the whole budget (about
  30 s) and comes back flagged as *not* proven (`GrillPlan.IsProvenOptimal == false`). A
  much larger or adversarial menu would do the same.
- **Assumes every piece fits the grill.** The largest piece in this data is 22 cm, which fits
  the 30 cm side. A piece that cannot fit the grill in either orientation makes the planner
  throw an error instead of guessing.
