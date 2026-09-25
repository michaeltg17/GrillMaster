# The GrillPlanner

> "I will find the best possible number of rounds — and I will *prove* it to you.
> I just need to skip the millions of arrangements that are obviously pointless."

`GrillPlanner` is the one planner the app ships, and it can hand you a plan and say,
with a straight face: **"no arrangement of this meat uses fewer rounds. I checked.
Here is why."** It gets there by trying arrangements one at a time — but with a
handful of rules that let it skip almost all of them. This file explains how, with
pictures.

---

## 1. The hard part: "try everything" grows insanely fast

Suppose you have 8 pieces. The first piece can go in many spots on the grill; the
second piece can then go in many *remaining* spots; and so on. If every piece had just
10 possible spots, the total number of possible full arrangements would be
10 × 10 × 10 × … (eight times) = **100,000,000**. With 15 pieces it's a quadrillion.
Trying every arrangement the slow way is impossible — that is the whole problem this
planner solves.

The trick is: **you don't have to try every arrangement. You only have to try every
*promising* one**, and you can prove the rest are hopeless without visiting them.
Computer scientists call this *branch and bound*:

- **branch** = pick a choice and explore it ("put the steak at the top-left, now what?"),
- **bound** = a rule that says "everything under this branch is hopeless, skip it all".

## 2. The champion and the floor

Before searching, the planner does two quick things:

**The floor.** Add up all the meat's area. A grill holds 600 cm². If the meat needs
900 cm², then *no matter how clever you are*, at least 2 rounds are needed (2 × 600 =
1200 ≥ 900, but 1 × 600 = 600 < 900). This "round up" number is **one** floor — no plan
can ever go below it. (The only *math* in this whole program, and it's just
division-and-round-up.) There is a second floor for menus full of one meat: 40 sausages
need 2 rounds when a grill holds at most 30, even if the area says 1 — that part is
computed exactly by a small cached search over one empty grill. The floor is the larger
of the two.

**The champion.** Run the [greedy pre-pass](greedy-prepass.md) first and keep *its*
answer as the current best — the **champion**. Now the search only has one job:
*beat the champion*. Any arrangement that uses as many rounds as the champion (or more)
is already worthless, so it can be abandoned the moment it becomes clear.

A lovely side effect: if the champion is *already* standing on the floor (the pre-pass
found a plan that hits the lower bound), the search can stop before it even starts — a
score below the floor is impossible, so the champion is proven optimal instantly.

## 3. The search: place one piece, look ahead, undo, try the next spot

The search works like a very patient person arranging a bookshelf:

1. Take the **biggest** piece still waiting (biggest area first; ties: fatter first,
    then longer — big pieces have the fewest options).
2. Try every reasonable spot for it, one at a time.
3. For each spot, repeat the whole game with the *next* biggest piece.
4. If a branch can't possibly beat the champion, **abandon it** — no point going deeper.
5. If you reach a moment where *every* piece is placed, compare the round count with
   the champion; if it's better, the new arrangement becomes champion.
6. **Undo** the last placement (lift the piece off the grill) and try the next spot.

Step 6 — undoing — is called *backtracking*, and it is why the search can explore
millions of arrangements without ever touching a single piece of meat twice in the
same place. The grill's map is always perfectly restored after each try.

### Example A: two rumpsteaks (15×7)

Area: 2 × 105 = 210 cm² → floor = 1 round. The pre-pass's champion: 1 round.
Champion is on the floor → **stop immediately**. Result: 1 round, **proven optimal**,
after exploring a single decision.

### Example B: four big squares (15×15)

Area: 4 × 225 = 900 cm² → floor = **2** rounds (one 30×20 grill can hold at most two
15×15 squares, side by side, filling a 30×15 strip). The pre-pass's
champion: 2 rounds — again on the floor → stop immediately.

Each round looks like this (two 15×15 squares side by side, 30 wide × 15 tall,
leaving a 5 cm strip empty):

```
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
..............................
..............................
..............................
..............................
..............................
```

Proven: you cannot do this in one round (the floor says 2), and two rounds work.

### Example C: the menu that fools the pre-pass

Recall the menu from the [pre-pass doc](greedy-prepass.md): 2 spare ribs (24×5),
1 pork chop (20×6), 1 sirloin (18×6), 1 steak (10×5), 1 sausage (6×3), 2 patties (4×4).
Area 568 cm² → floor = 1. The pre-pass's champion: 2 rounds.

Now the search has real work: it must find a 1-round arrangement, or prove none
exists. It explores **25 decision points** (that's all) and finds the 1-round packing
(pork chop standing up along the right edge, steak turned in the middle). The new
champion — 1 round — is on the floor, so the search stops. **Proven optimal: 1 round.**

## 4. The five rules that make it fast

Without these rules, even our small menus would take forever. Each one is a way of
saying "this whole family of arrangements is hopeless or pointless — skip it":

1. **The look-ahead bound.** At every decision point the planner asks: "in the best
   case, how many rounds do I still need?" The meat that is still waiting must fit into
   the leftover space of the grills already open plus fresh grills (600 cm² each); and
   for each kind of meat, the waiting pieces of that kind beyond what the open grills
   can still hold must go into fresh grills, each of which holds at most a fixed number
   of that kind. If that total is at least the champion's round count, no arrangement
   under this branch can win — abandon it. (The bound gets sharper the deeper the search
   goes: the more meat already down, the less room is left. Example: the champion is on
   2 rounds, and the waiting meat plus the open grills' leftover room would need 3
   grills — abandon the branch.)

2. **Biggest first.** Big pieces have the fewest options; placing them early means
   hopeless branches are discovered early, when there is the least to abandon.

3. **Two spot sets: one to find fast, one to prove complete.** The search runs in
   two passes with different spot sets. The **fast pass** only tries "resting,
   pushed-in" ("skyline") spots: a piece must sit on the grill's bottom edge or on top
   of another piece — it may never *float* — and it must be pushed as far **left** as
   it will go. (It's the same rule as stacking boxes: every box rests on the floor or
   on another box, and is slid left until it bumps into something.) This throws away
   huge families of equivalent positions and is what makes the fast pass fast — but
   **alone it is not complete**: the pieces are placed in a fixed order (biggest
   first), and in the optimal packing a piece's left wall or support can be provided
   by a piece that is placed *later* in that order, so the skyline-only search can
   miss the optimum — and worse, "prove" a suboptimal one. (The differential harness
   against an external CP-SAT solver found exactly this: a 9×7 grill with six pieces
   whose optimum is one round, where the skyline search found none.) The
   **verification pass** therefore re-runs the search over **every** free position:
   only that complete set makes "no better arrangement exists" a true proof, and only
   then is `IsProvenOptimal` allowed to be true.

4. **Don't open a later grill while an earlier one is empty.** Grills are
   indistinguishable boxes: "put the steak on grill 3 while grill 2 is empty" is the *same plan* as
   "put the steak on grill 2 while grill 3 is empty" — just the names swapped. The
   search only ever opens the next grill in line.

5. **Identical pieces aren't permuted.** With 10 identical sausages, "sausage 3 in
   spot A, sausage 7 in spot B" and "sausage 7 in spot A, sausage 3 in spot B" are the
   *same* arrangement. The search enforces an order: the second identical sausage may
   only go into a later round, or the same round in a slot that comes after the first
   sausage's slot. This cuts the search for repeat-heavy menus by an enormous factor.

And the **early stop**: the moment the champion touches the floor, everything remaining
is hopeless by definition — the search ends, and the answer is *proven*.

## 5. The budget: an honest time limit

Real menus can be nasty, and "try everything" can still be large. So the search
counts every decision it makes against a **budget** (default: 10,000,000 decisions),
shared across both passes and, in the default parallel mode, across all cores. On
the full 15-menu fixture, thirteen menus need no decisions at all (the pre-pass is
already on the floor and is accepted without searching), Menu 07 settles in 619,
and Menu 01 (1791 cm² of meat against three rounds of 600 cm²: nine squares of
slack) spends the whole budget: the fast pass uses about 6.1M decisions and comes up
short of the floor, then the verification pass spends the remaining ~3.9M trying to
prove that no 3-round arrangement exists — and does not finish in time, so Menu 01
is returned as **not proven**. Menu 01 is a genuinely hard instance: the 3-round
verification space is over 10,000,000,000 decisions (a 10-billion-node run across
all 16 cores of a Ryzen 9800X3D — about 23 minutes — still had not exhausted it),
and an external CP-SAT solver given 10 minutes cannot prove that 3 rounds are
impossible either. The honest flag was therefore the correct behaviour, not a
planner failure — and the instance has since been settled by an independent
route: 3 rounds are provably infeasible (a hand proof plus an exhaustive
machine proof, neither of which is the planner's search), so Menu 01's optimum
is the greedy's 4. See `docs/menu-01-optimality.md`, which also records the
plan to give the planner a third phase that can certify such tight instances.
If a menu blows the budget before a
proof, the planner stops and returns the best arrangement it had found so far,
honestly flagged as **not proven optimal**. It never lies: `IsProvenOptimal` is true
exactly when the champion reached the floor or the *complete* position set was
exhausted within the budget.

## 6. How the code does this

The planner lives in
`src/GrillMaster.Application/Features/Plans/GrillPlanner.cs`.
Code, translated into the story:

| Code | What it is in the story |
|------|--------------------------|
| `Plan(menu, grill)` | Takes the job: builds the floor, runs the pre-pass, starts the search. |
| `GreedyShelf.Place(...)` | Hiring the fast cook to set the champion's score. |
| `greedyRounds.Count == lowerBound → return` | The champion is already on the floor: proven with zero search. |
| `GrillPlannerHelpers.ComputeLowerBound(...)` | The floor: the larger of total area ÷ 600 and the per-type capacity count. Computed once, per-type capacities cached. |
| `SearchState` | The patient person's notebook: everything mutable about the search, one per `Plan` call. |
| `Search(index)` | The patient person, mid-arrangement: `index` = "which piece am I placing now?". |
| `for (var round = 0; round <= _nonEmptyRounds; ...)` | Trying each grill, one after another; only the last one may be new. |
| `opensNewRound && _nonEmptyRounds + 1 >= _best → break` | Rule 1 (champion edition): opening this grill can't beat the champion. |
| `RoundsLowerBound(index) >= _best → return` | Rule 1 (look-ahead edition): the waiting meat can't fit in the open grills' leftover room plus fresh full grills — abandon the branch. |
| `hasPrevSame` / `SlotOrder(y, x, rotated)` | Rule 5: the identical-piece ordering. |
| `new SearchState(..., allPositions: false)` then `new SearchState(..., allPositions: true)` | The two passes: first the fast skyline search, then — only if the champion is still above the floor — the complete re-proof seeded with the fast pass's champion. |
| `occupancy.CreateSkylineScan(w, h)` | Rule 3 (fast pass): walks this piece's "resting, pushed-in" spots one at a time, without allocating. |
| `occupancy.CreateAllFreePositionsScan(w, h)` | Rule 3 (verification pass): walks *every* free spot one at a time, without allocating — the complete set the proof rests on. |
| `var orientations = w0 == h0 ? 1 : 2;` | A square piece's 90° turn is the same geometry, so it is tried once, not twice. |
| `MarkOccupiedCells(...)` / `MarkFreeCells(...)` | Putting the piece on the grill / lifting it back off (the undo). |
| `RawPlacement` stacks | The arrangements under construction, kept as raw numbers — no bookkeeping object per candidate. |
| `Search(index + 1)` | Recurse: the same game with the next piece. |
| `_best = _nonEmptyRounds; _bestRounds = SnapshotRounds()` | A new champion! Save the arrangement. |
| `_nodes` / `MaxNodes` | The tally of decisions tried / the budget. |
| `SearchOutcome` / `IsProvenOptimal` | The honesty clause, made explicit: the search either ends `ProvenOptimal` (the floor was reached, or the *complete* position set was exhausted) or `BudgetExceeded` — the one finish that is not a proof. |

The shared grill map and both spot-set walkers live in
`RoundOccupancy.cs`. The map holds the grill as one 32-bit word per row and one per
column, so "is this rectangle free?" is a handful of bit operations.
`CreateSkylineScan` walks only the spots where a piece would actually rest (the bottom
edge, then the tops of whatever is already down) and could not be slid left, and
`CreateAllFreePositionsScan` walks every free spot in row-major order — both as
allocation-free `ref struct` iterators.

By default the search does not run with one patient person but with one per core:
each worker keeps its own notebook and pulls subtrees off a shared queue. A subtree
is handed back to the queue whenever its holder has spent 32,000 decisions on it, so
the cores stay balanced at every depth of the search. The champion, the budget tally,
and the queue are the only things the workers share, so the same rules — and the
same proof — apply as in the one-person story above; the quality snapshot pins the
total rounds the search returns (and the serial run's exact node count), while the
node count of a parallel run is formally scheduling-dependent and left unpinned. `EnableParallelism: false` keeps the search strictly serial
and fully deterministic, including that node count; `Parallelism` caps the thread
count when it is enabled.

## 7. The numbers

- **Speed:** thirteen of the 15 menus cost essentially nothing (the pre-pass is
  already on the floor and is accepted without searching); Menu 07 settles in 619
  decisions; Menu 01 spends the full 10,000,000 budget (fast pass: ~6.1M skyline
  decisions, verification pass: the rest over the complete set) — about 2 s in
  Release at the default budget on all 16 cores (about 9 s serially), and ends
  **not proven** because the verification pass cannot finish in time.
- **Quality:** 38 rounds on the 15-menu fixture, unchanged by the two-pass search:
  14 menus reach the floor (37 in total), and Menu 01's best-found plan is 4 rounds
  (its floor is 3; no solver — this planner or the external CP-SAT oracle — has
  found a 3-round arrangement).
- **Guarantees:** a valid plan, and — within budget — a *proof* that nothing is
  better, by reaching the floor or exhausting the *complete* position set. If the
  budget is ever exceeded before that, the best-so-far plan is returned and honestly
  flagged `IsProvenOptimal: false`.

## 8. What you get (and when the proof stops)

- Menus that settle at the lower bound cost essentially nothing: the pre-pass's plan
  is accepted as proven optimal with zero search.
- Small-to-medium menus (a few dozen pieces) get a full proof: the fast pass finds
  the answer, and the verification pass confirms it by exhausting the complete set —
  which is all the search handles comfortably at that size.
- Tight menus that miss the floor by one round (like Menu 01) can spend the whole
  budget on the verification pass and still come back flagged as **not proven
  optimal** (`GrillPlan.IsProvenOptimal == false`): the best-found plan is usually
  the true optimum, but the planner no longer claims a proof it cannot complete.
- On huge or adversarial menus the search hits its budget sooner; then you get the
  best arrangement found within budget, honestly flagged the same way. The pre-pass
  alone comes within a round of the final answer on our fixture, so the fallback is
  still a good plan.
