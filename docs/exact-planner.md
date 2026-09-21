# The Exact Planner (`exact`)

> "I will find the best possible number of rounds — and I will *prove* it to you.
> I just need to skip the millions of arrangements that are obviously pointless."

This is the only planner that can hand you a plan and say, with a straight face:
**"no arrangement of this meat uses fewer rounds. I checked. Here is why."**
It gets there by trying arrangements one at a time — but with a handful of rules that
let it skip almost all of them. This file explains how, with pictures.

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
1200 ≥ 900, but 1 × 600 = 600 < 900). This "round up" number is the **floor** — no plan
can ever go below it. (The only *math* in this whole program, and it's just
division-and-round-up.)

**The champion.** Run the [greedy planner](greedy-planner.md) first and keep *its*
answer as the current best — the **champion**. Now the search only has one job:
*beat the champion*. Any arrangement that uses as many rounds as the champion (or more)
is already worthless, so it can be abandoned the moment it becomes clear.

A lovely side effect: if the champion is *already* standing on the floor (greedy found
a plan that hits the area minimum), the search can stop before it even starts — a score
below the floor is impossible, so the champion is proven optimal instantly.

## 3. The search: place one piece, look ahead, undo, try the next spot

The search works like a very patient person arranging a bookshelf:

1. Take the **biggest** piece still waiting (biggest first, same as greedy).
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

Area: 2 × 105 = 210 cm² → floor = 1 round. Greedy's champion: 1 round.
Champion is on the floor → **stop immediately**. Result: 1 round, **proven optimal**,
after exploring a single decision.

### Example B: four big squares (15×15)

Area: 4 × 225 = 900 cm² → floor = **2** rounds (one 30×20 grill can hold at most two
15×15 squares, side by side, filling a 30×15 strip). Greedy's
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

### Example C: the menu that fools greedy

Recall the menu from the [greedy doc](greedy-planner.md): 2 spare ribs (24×5),
1 pork chop (20×6), 1 sirloin (18×6), 1 steak (10×5), 1 sausage (6×3), 2 patties (4×4).
Area 568 cm² → floor = 1. Greedy's champion: 2 rounds.

Now the search has real work: it must find a 1-round arrangement, or prove none
exists. It explores **25 decision points** (that's all) and finds the 1-round packing
(pork chop standing up along the right edge, steak turned in the middle). The new
champion — 1 round — is on the floor, so the search stops. **Proven optimal: 1 round.**

## 4. The five rules that make it fast

Without these rules, even our small menus would take forever. Each one is a way of
saying "this whole family of arrangements is hopeless or pointless — skip it":

1. **The area bound.** To beat the champion you must finish in *fewer* rounds than it
   used, so all the meat — what is placed plus what is still waiting — must fit inside
   that many grills' total area. If it can't, no arrangement under this branch can win
   — abandon it. (Example: champion is 2 rounds, so beating it means fitting everything
   into 1 round = 600 cm²; a menu whose meat totals 700 cm² can't be beaten at all,
   which the search notices immediately.)

2. **Biggest first.** Big pieces have the fewest options; placing them early means
   hopeless branches are discovered early, when there is the least to abandon.

3. **Resting spots only (the "skyline" rule).** A piece must sit on the grill's bottom
   edge or on top of another piece — it may never *float*. If a spot has empty space
   directly under the piece, then the piece could slide down, which means that floating
   spot is never a "real" arrangement; its slid-down version will be considered
   separately. This throws away huge families of equivalent positions. (It's the same
   rule as stacking boxes: every box rests on the floor or on another box.)

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

Real menus can be nasty, and "try everything promising" can still be large. So the
search counts every decision it makes against a **budget** (default: 20,000,000
decisions). On the full 15-menu fixture it uses only about 38,000 in total — a tiny
fraction. But if a menu *did* blow the budget, the planner would stop and return the
best arrangement it had found so far, honestly flagged as **not proven optimal**. It
never lies: `IsProvenOptimal` is true only when it can *prove* the floor was reached.

## 6. How the code does this

The planner lives in
`src/GrillMaster.Application/Features/Planning/Planners/ExactBacktrackingPlanner.cs`.
Code, translated into the story:

| Code | What it is in the story |
|------|--------------------------|
| `Plan(menu, grill)` | Takes the job: builds the floor, the champion, then starts the search. |
| `new GreedyShelfPlanner().Plan(...)` | Hiring the fast cook to set the champion's score. |
| `GrillPlanHelpers.ComputeLowerBound(...)` | The floor: total area ÷ 600, rounded up. Computed once and cached. |
| `Search(index)` | The patient person, mid-arrangement: `index` = "which piece am I placing now?". |
| `for (var round = 0; ...)` | Trying each grill, one after another. |
| `round > _nonEmptyRounds → break` | Rule 4: never open a later grill while an earlier one is empty. |
| `roundsAfter >= _best → break` | Rule 1 (champion edition): opening this grill can't beat the champion. |
| `_remainingArea[index] > TotalFreeCapacity()` | Rule 1 (area edition): the waiting meat can't fit in the champion's leftover room — abandon the branch. |
| `hasPrevSame` / `SlotOrder(...)` | Rule 5: the identical-piece ordering. |
| `EnumerateSkylinePositions(piece)` | Rule 3: the list of "resting spots only" for this piece. |
| `occupancy.MarkOccupied(...)` / `MarkFree(...)` | Putting the piece on the grill / lifting it back off (the undo). |
| `Search(index + 1)` | Recurse: the same game with the next piece. |
| `_best = _nonEmptyRounds; _bestRounds = SnapshotRounds()` | A new champion! Save the arrangement. |
| `_nodes` / `MaxNodes` | The tally of decisions tried / the budget. |
| `IsProvenOptimal = _best == lowerBound && !_budgetExceeded` | The honesty clause: "proven" only when the floor was reached *and* the search was allowed to finish. |

The shared grill map and the "resting spots" rule live in
`RoundOccupancy.cs` (`EnumerateSkylinePositions` walks only the levels where a piece
would actually rest: the bottom edge, then the tops of whatever is already down).

## 7. The numbers

- **Speed:** about 2 ms per menu (median, over repeated runs of the 15-menu fixture),
  38,261 decisions for all 15 menus together. Menus where greedy already hit the floor
  cost essentially nothing.
- **Quality:** 37 rounds on the 15-menu fixture — exactly the floor, i.e. **proven
  optimal for every menu**.
- **Guarantees:** a valid plan, and — within budget — a *proof* that nothing is better.
  If the budget is ever exceeded, the best-so-far plan is returned and honestly flagged
  `IsProvenOptimal: false`.

## 8. When to use it (and when not to)

- Use it when you need the **proof** — or when the menu is small-to-medium (a few
  dozen pieces), which is all the search handles comfortably.
- On huge or adversarial menus it could hit its budget; then you get the best
  arrangement found within budget, not a proof. For everyday use the
  [optimized planner](optimized-planner.md) reaches the same 37-round answer on our
  fixture with a fraction of the machinery — `exact` is the one to reach for when
  "I must know this is the best" matters.
