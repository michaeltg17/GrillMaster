# The OR-Tools Planner (`ortools`)

> "Stop cooking. Write the whole problem down as maths and hand it to a professional
> integer-programming solver."

Every other planner in this repo *steers its own search*: we decided the order of the
pieces, the candidate positions, the pruning rules, the symmetry breaks. This planner
does none of that. It translates the menu into a **constraint model** — variables,
rules, and an objective — and hands the model to **CP-SAT**, Google OR-Tools'
constraint-programming solver, which brings its own search (linear-relaxation
propagation, clause learning, heuristics we didn't write). Our job is just to make
sure the maths says exactly what the grill allows.

---

## 1. The model, in plain words

For every piece the model declares:

- an **x** and a **y** — the top-left corner, in whole centimetres;
- a **rotation** — true or false;
- a **width** and a **height** — the piece's sides, possibly swapped by the rotation;
- an **end-x** and an **end-y** — where the piece's far edges land.

For every round it declares a **used** flag, and for every (piece, round) pair a
**present** flag. The rules:

1. the width is the length *or* the side (depending on the rotation) — and the height
   is the other one;
2. the piece stays on the grill (corner and far edges inside 30 × 20);
3. **every piece is present in exactly one round**;
4. **pieces present in the same round do not overlap** — this is CP-SAT's 2-D
   no-overlap constraint, over *optional* rectangles: a piece's rectangle only counts
   in a round when its present flag says it is there;
5. a round is *used* if any piece is present in it.

The objective: **minimise the number of used rounds.**

That is the whole planner. No ordering, no heuristics, no symmetry tricks — the
solver finds all of that on its own, the way a professional kitchen hires a specialist
instead of training a new cook.

## 2. What the solver owes us

- **A valid plan, always.** The rules above are exactly the grill's rules, so anything
  the solver returns is a legal plan. (If the solver's time runs out *before it has
  found any plan at all*, the planner hands back the greedy plan instead — the same
  "fall back to the incumbent" convention as the [exact planner](exact-planner.md)
  when its node budget runs out.)
- **An honesty flag.** `IsProvenOptimal` is true only when the solver *proves* no
  plan can use fewer rounds. Hitting the time limit with a good-but-unproven plan
  keeps the flag false.
- **Determinism.** The solver is told to use a single search worker, so the same
  model always gives the same plan — the tests rely on that.

## 3. A complete example: the test fixture, one round

The unit-test fixture — 2 rumpsteaks (15×7), 2 steaks (10×5), 3 chickens (12×5),
4 sausages (6×3) — covers 562 of the 600 cm², so the lower bound is **1**. CP-SAT
finds this one-round packing and proves it optimal in a few dozen milliseconds:

```
BBBBBBBBBBBBBBBFFFFFFFFFFDDDDD
BBBBBBBBBBBBBBBFFFFFFFFFFDDDDD
BBBBBBBBBBBBBBBFFFFFFFFFFDDDDD
BBBBBBBBBBBBBBBFFFFFFFFFFDDDDD
BBBBBBBBBBBBBBBFFFFFFFFFFDDDDD
BBBBBBBBBBBBBBB.......JJJDDDDD
BBBBBBBBBBBBBBB.......JJJDDDDD
CCCCCCCCCCCCGGGGGGGGGGJJJDDDDD
CCCCCCCCCCCCGGGGGGGGGGJJJDDDDD
CCCCCCCCCCCCGGGGGGGGGGJJJDDDDD
CCCCCCCCCCCCGGGGGGGGGG...DDDDD
EEEEEEEEEEEEAAAAAAAAAAAAAAAKKK
EEEEEEEEEEEEAAAAAAAAAAAAAAAKKK
EEEEEEEEEEEEAAAAAAAAAAAAAAAKKK
EEEEEEEEEEEEAAAAAAAAAAAAAAAKKK
EEEEEEEEEEEEAAAAAAAAAAAAAAAKKK
IIIIIIHHHHHHAAAAAAAAAAAAAAAKKK
IIIIIIHHHHHHAAAAAAAAAAAAAAA...
IIIIIIHHHHHH..................
```

`A,B` = rumpsteaks, `C,E` = chickens lying flat, `D` = a chicken standing up along
the right edge, `F,G` = steaks, `H,I,J,K` = sausages (two of them standing up),
`.` = empty. Notice the solver's instinct is the same as a human's: big pieces first,
awkward corners filled by *rotated* small pieces. It just gets there without us
telling it to.

## 4. How the code does this

The planner lives in
`src/GrillMaster.Application/Features/Planning/Planners/OrToolsPlanner.cs`; the
package is `Google.OrTools` (the CP-SAT part of the OR-Tools suite):

| Code | What it is in the story |
|------|--------------------------|
| `MaxTimeSeconds` (default **30**) | How long the specialist may think per menu. |
| `var greedy = new GreedyShelfPlanner().Plan(menu, grill);` | Two jobs: it fixes the *number of rounds to model* (the greedy answer is a valid upper bound), and it is the fallback if the specialist finds nothing in time. |
| `model.NewIntVar(...)`, `model.NewBoolVar(...)` | Declaring the variables of §1. |
| `model.Add(width[i] == piece.Length + ((piece.Width - piece.Length) * orientation[i]))` | Rule 1, in one line of arithmetic (the rotation swaps the sides). |
| `model.Add(endX[i] == x[i] + width[i])` | Rule 2's far edge — see the quirk below. |
| `model.NewOptionalIntervalVar(x[i], width[i], endX[i], presence[i][r], ...)` | The piece's rectangle in round r, active only when `presence[i][r]`. |
| `noOverlap.AddRectangle(xInterval, yInterval)` | Rule 4: the 2-D no-overlap, per round. |
| `model.AddExactlyOne(presence[i])` | Rule 3: every piece in exactly one round. |
| `model.Add(used[r] >= presence[i][r])` | Rule 5. |
| `model.Minimize(objective)` | The objective: as few used rounds as possible. |
| `StringParameters = "max_time_in_seconds:30 num_search_workers:1"` | The time cap and the determinism requirement. |
| `IsProvenOptimal: status == CpSolverStatus.Optimal` | The honesty flag. |

**The quirk:** CP-SAT's interval expressions may contain **at most one variable**
each, so the model cannot say "the interval ends at `x + width`" directly (that is
two variables). Instead it declares a real `endX` variable and adds the equation
`endX == x + width` as an ordinary linear constraint. One extra variable per axis —
the price of speaking CP-SAT.

## 5. The numbers

Measured on the 15-menu fixture (single search worker, 30 s cap):

- **Quality:** **38 rounds** — one round above the 37 lower bound: on one menu the
  solver's best plan in time was one round off, and on **4 of the 15 menus** it
  reached *and proved* the lower bound.
- **Speed:** wildly menu-dependent. Some menus settle in **milliseconds** (a menu
  whose lower bound is 1 is proven the moment the first one-round packing is
  found); many others spend the whole 30 s trying to prove the last step. The whole
  fixture takes several minutes.
- **Why slower than `exact` here?** On these small menus, a purpose-built
  backtracking search (skyline positions, per-type symmetry, area bounds) is a much
  sharper tool than a general engine. CP-SAT is not out to beat our `exact` planner
  on *this* fixture — it is an **independent second opinion**: a completely different
  engine, written by a different team, that solves the same maths. If it and `exact`
  agree, the answer is very likely right.

Because of the 30 s cap, `ortools` is deliberately **not** in the end-to-end suite or
the performance benchmark (both would take ~8 and ~35 minutes respectively); the
planner's behaviour is covered by the unit tests, which run it with a short cap.

## 6. Where it fits

- `greedy` / `maxrects` / `guillotine` / `batch` — fast cooks, different styles.
- `optimized` / `exact` — our own strong cooks (improve a guess / prove the best).
- `ortools` — the **hired specialist**: a general-purpose exact engine, slower on our
  small data, but one whose model you could hand a bigger menu (or a different
  problem) without rewriting the search.
