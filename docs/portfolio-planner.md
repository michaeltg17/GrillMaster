# The Portfolio Planner (`portfolio`)

> "Hire every cook, let them all plan the menu, and serve the best plate."

This planner doesn't bring any new packing ideas of its own. It runs **all the other
planners** on the menu, keeps the plan with the fewest rounds, and throws the rest
away. It is the answer to the question: *"what if we're not sure which cook is best
for this particular menu — so we just ask all of them?"*

---

## 1. The rule in one sentence

Run `greedy`, `optimized`, `exact` and `ortools` on the menu; the final plan is the
one with the **fewest rounds**.

That's it. And because it only ever *keeps* an existing valid plan, the result is
always a valid plan — and it is **never worse than the best of the four cooks**.

### The moment it becomes a proof

Remember the floor from the other docs: the larger of total meat area ÷ 600 and the
per-type capacity count. No plan —
from any planner, ever — can use fewer rounds than the floor. So if **any** of the
cooks reaches the floor, its plan *is* the best possible, full stop. The portfolio
stops asking cooks as soon as that happens.

So the portfolio gives you one of two things:

- a plan **with a proof** that it is optimal (some cook hit the floor), or
- the **best plan any of the cooks found**, honestly flagged as unproven.

There is no third option. It can never return something worse than what the best
cook produced, and it can never claim a proof it doesn't have.

### The order matters (a little)

The cooks are asked **cheapest first**: `greedy`, then `optimized`, then `exact`,
then `ortools`. Two reasons:

- the fast ones often hit the floor, in which case the expensive ones never run;
- if the menu is hard, you still get `exact`'s answer before the slow specialist
  (`ortools`, up to 30 s per menu) is even asked.

On our 15-menu fixture, 14 of the 15 menus are settled by `greedy` or `optimized` —
but on Menu 01 both heuristics come back one round above the floor, so the portfolio
asks `exact` (which exhausts its 20,000,000-decision budget in about 30 s) and then
`ortools` (up to 30 s) as well. That one menu is where the portfolio spends its time
and its ~21 million search decisions; the other 14 still cost milliseconds.

## 2. Worked examples (all verified against the real code)

### Example A: the menu that fools greedy

The spare-rib menu from the [greedy doc](greedy-planner.md): 2 spare ribs (24×5),
1 pork chop (20×6), 1 sirloin (18×6), 1 steak (10×5), 1 sausage (6×3), 2 patties
(4×4). Area 568 cm² → the floor is **1**.

1. `greedy` runs: **2 rounds**. Not the floor — keep going.
2. `optimized` runs: **1 round** — that *is* the floor. Stop.

`exact` and `maxrects` never run. Final answer: the 1-round plan, **proven optimal**
(some cook hit the floor). Total cost: roughly one fast planner plus one medium one.

### Example B: a menu greedy nails

2 rumpsteaks (15×7) + 4 sausages (6×3). `greedy` runs: **1 round**, and 1 *is* the
floor (282 cm² < 600 cm²). Stop immediately. Nobody else runs.

### Example C: the full 15-menu fixture

Fourteen of the 15 menus are settled by `greedy` or `optimized` before `exact` is
ever consulted. Menu 01 is the exception: both heuristics return 4 rounds against a
floor of 3, so the portfolio spends `exact`'s whole 20,000,000-decision budget (about
30 s) and `ortools`' 30 s cap on it — and all four cooks agree on 4. Total:
**38 rounds** — the floor on 14 menus, best-so-far on Menu 01, honestly flagged
unproven — with about 21 million search decisions, almost all on that one menu.

## 3. When does the portfolio actually pay off?

- **When no single cook is reliably best.** On our fixture `greedy` is off by one
  round on two menus; `optimized` and `exact` are never off. But on *other* menus the
  ranking could change — and the portfolio doesn't care, because it keeps whatever is
  best.
- **When you want the proof without the risk.** `exact` alone can hit its node budget
  on a nasty menu and come back unproven, and `ortools` can hit its time cap the same
  way. The portfolio still gets `optimized`'s (often proven) answer, and takes the
  stronger exact answer only if it's better.
- **When you'd rather wait a millisecond than be suboptimal.** On our fixture the
  first two cooks settle everything, so the portfolio costs a couple of milliseconds
  per menu — but on a hard menu it is happy to wait for `exact` or `ortools` to do
  their thinking, because that is where the proof comes from.

If you *do* care about raw speed on huge menus, run `greedy`, `maxrects` or
`guillotine` alone. If you want the portfolio's guarantees, the extra time is the
price — and on menus like ours it's a couple of milliseconds.

## 4. How the code does this

The planner lives in
`src/GrillMaster.Application/Features/Plans/Planners/PortfolioPlanner.cs`:

| Code | What it is in the story |
|------|--------------------------|
| `Members` | The four cooks, in the order they get asked: greedy, optimized, exact, ortools. |
| `var best = Members[0].Plan(menu, grill);` | Ask the first cook; their plate is the current best. |
| `for (var i = 1; i < Members.Count && best.Rounds.Count > lowerBound; i++)` | Keep asking the next cooks **while** the best plate isn't yet at the floor. |
| `if (plan.Rounds.Count < best.Rounds.Count) best = plan;` | A better plate arrives — swap it in. |
| `searchNodes += plan.SearchNodes;` | Keep the running total of search decisions spent (0 on 14 of the 15 fixture menus; ~21 million on the 15th). |
| `IsProvenOptimal: best.Rounds.Count == lowerBound` | The honesty clause: "proven" exactly when some cook reached the floor. |
| `Name` = `"portfolio"` | The report says "portfolio" produced this plan, whatever cook's plate it kept. |

Because each member is itself a complete, tested `IGrillPlanner`, the portfolio is
about 40 lines of glue: no packing logic of its own, nothing to get out of sync.

## 5. The numbers

- **Speed:** about 2 ms per menu on 14 of the 15 menus (the early stop means it costs
  roughly one or two cooks, not four); Menu 01 runs all four and takes about a minute.
- **Quality:** **38 rounds** — the floor on 14 menus, best-so-far on Menu 01 — versus
  39 for `greedy`/`maxrects`/`optimized` and the same 38 for `exact`.
- **Guarantees:** never worse than the best member; `IsProvenOptimal` is true exactly
  when the floor was reached by any member.
- **Search decisions:** about 21 million on the fixture, almost all on Menu 01 (the
  exact budget plus ortools' time-capped branch count, which varies a little per run).

## 6. Where it fits

- `greedy` / `maxrects` / `guillotine` — single fast cooks, different styles.
- `optimized` / `exact` / `ortools` — single strong cooks (improve a guess / prove
  the best ourselves / prove it with a hired specialist).
- `portfolio` — the head chef: **runs the four cooks above, serves the best plate,
  and tells you honestly whether that plate is provably the best possible.**

For the assessment's menus it is the strongest default: 14 of 15 menus proven optimal
in a few milliseconds each, and the hard one settled by the whole kitchen in about a
minute.
