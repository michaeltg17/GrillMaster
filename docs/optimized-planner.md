# The Optimized Planner (`optimized`)

> "Start with the fast cook's answer. Then keep asking one question:
> *'Can we do it all with one fewer tray?'* — and if the answer is yes, keep going."

This planner is a compromise between the other two. It has the speed of
[greedy](greedy-planner.md) and, on our menus, the quality of
[exact](exact-planner.md) — it reaches the proven-best answer on the full 15-menu
fixture. The trick is simple and a little stubborn: it never accepts a plan it can
improve by one round.

---

## 1. The question it keeps asking

Suppose the greedy planner says: "this menu takes **3 rounds**."
The optimized planner doesn't trust that. It asks:

> **"Can ALL of these pieces fit into 2 rounds?"**

Not "move one piece here and there" — it takes *every* piece off the grill and tries to
pack them from scratch into exactly 2 rounds, biggest piece first, trying every
reasonable (resting) spot. If it succeeds, the 2-round plan replaces the 3-round plan,
and the question is asked again: *"Now, can it all fit into 1 round?"*

- **Yes** → replace again, ask again.
- **No** → stop. The plan you have is the final answer.

Each accepted answer makes the plan strictly smaller (one fewer round), and each plan
is still perfectly valid — every piece placed once, nothing overlapping, nothing off
the grill. So the process can never make things worse; it either improves the plan by
one round or stops. It is also *deterministic*: same menu, same grill, same answer,
every time.

### The cheap first check: the area shortcut

Before doing any work, the question answers itself in cases where the math already
settles it. A round holds 600 cm². If the current plan is 3 rounds and all the meat
together is 1500 cm², then 2 rounds (1200 cm²) can't hold it — the answer to
"2 rounds?" is **no, immediately**, with zero searching. This check is what makes the
planner fast on menus where the greedy answer is already close to the floor.

### A budget per question

Each "can it fit in N−1 rounds?" attempt is a search, and searches can in theory run
long. So each attempt gets its own **budget of 200,000 decisions**; if the search
runs out of decisions, the answer is "no" (we give up on that question) and the
planner stops. The whole loop is also capped at 200 questions. In practice the budgets
are never close to running out on our menus.

## 2. Worked examples (all verified against the real code)

### Example A: a menu greedy already nailed

2 rumpsteaks (15×7) + 4 sausages (6×3). Greedy uses **1 round**, and the area floor
is 1 (282 cm² < 600 cm²). The questioning loop stops as soon as the plan has just one
round left — and 0 rounds is impossible while there is any meat at all. Result:
**1 round**, and since 1 is the floor, the plan is **proven optimal**.
The optimized planner added nothing here — but it would have, had greedy erred.

### Example B: a menu where the math answers the question

Four 15×15 squares (900 cm²). Greedy uses 2 rounds. Question: "1 round?" — area
900 > 600 → **no, instantly**. Stop. Result: **2 rounds, proven optimal** (the floor
is 2). No searching was needed at all — just division.

### Example C: the menu that fools greedy

The spare-rib menu from the [greedy doc](greedy-planner.md): 2 spare ribs (24×5),
1 pork chop (20×6), 1 sirloin (18×6), 1 steak (10×5), 1 sausage (6×3), 2 patties (4×4).
Total 568 cm² → floor is **1**. Greedy says **2 rounds** (the 10×5 steak got left out
of the first round by a bad early choice).

The optimized planner asks: **"Can all 8 pieces fit into 1 round?"**

1. Area check: 568 ≤ 600 — possible, worth searching.
2. Re-pack everything from scratch, biggest first: spare rib top-left, spare rib right
   below it, then — and this is where greedy had differed — the pork chop goes
   **standing up along the right edge** (6 wide × 20 tall) instead of lying flat.
   The sirloin goes on the left below the ribs, the steak is turned and tucked into
   the middle-right, and the sausage and two patties fill the bottom gaps.
3. Every piece placed → **yes, 1 round works!**

The 1-round plan replaces the 2-round one. The plan now has just one round left, so
the questioning ends (0 rounds is impossible while there is meat). Result:
**1 round, proven optimal** (it's the floor).

The final round, exactly as the code produces it (each square is 1 cm):

```
AAAAAAAAAAAAAAAAAAAAAAAACCCCCC
AAAAAAAAAAAAAAAAAAAAAAAACCCCCC
AAAAAAAAAAAAAAAAAAAAAAAACCCCCC
AAAAAAAAAAAAAAAAAAAAAAAACCCCCC
AAAAAAAAAAAAAAAAAAAAAAAACCCCCC
BBBBBBBBBBBBBBBBBBBBBBBBCCCCCC
BBBBBBBBBBBBBBBBBBBBBBBBCCCCCC
BBBBBBBBBBBBBBBBBBBBBBBBCCCCCC
BBBBBBBBBBBBBBBBBBBBBBBBCCCCCC
BBBBBBBBBBBBBBBBBBBBBBBBCCCCCC
DDDDDDDDDDDDDDDDDDEEEEE.CCCCCC
DDDDDDDDDDDDDDDDDDEEEEE.CCCCCC
DDDDDDDDDDDDDDDDDDEEEEE.CCCCCC
DDDDDDDDDDDDDDDDDDEEEEE.CCCCCC
DDDDDDDDDDDDDDDDDDEEEEE.CCCCCC
DDDDDDDDDDDDDDDDDDEEEEE.CCCCCC
FFFFFFGGGGHHHH....EEEEE.CCCCCC
FFFFFFGGGGHHHH....EEEEE.CCCCCC
FFFFFFGGGGHHHH....EEEEE.CCCCCC
......GGGGHHHH....EEEEE.CCCCCC
```

`A,B` = spare ribs, `C` = pork chop (standing up), `D` = sirloin, `E` = steak (turned),
`F` = sausage, `G,H` = patties, `.` = empty.

## 3. Why "one fewer" at a time, and not "as few as possible"?

Asking "what is the *minimum* number of rounds?" is exactly what the
[exact planner](exact-planner.md) does — a big search. Asking "can we do **one fewer**
than now?" is a much smaller question: the number of grills is fixed (it's just one
less), so the search only has to decide *where* each piece goes, not *how many*
grills exist. And because every "yes" shrinks the plan by one, a few small questions
replace one big search — which is why the whole planner still runs in a couple of
milliseconds per menu.

It's like a game of bragging rights: "I bet we can cook this on 3 trays" → prove it →
"on 2?" → prove it → "on 1?" → impossible by area → done. Each round of the game is
small, and you stop the moment a bet can't be kept.

## 4. How the code does this

The planner lives in
`src/GrillMaster.Application/Features/Planning/Planners/OptimizedHeuristicPlanner.cs`.
Code, translated into the story:

| Code | What it is in the story |
|------|--------------------------|
| `Plan(menu, grill)` | Takes the job: gets greedy's plan, then starts asking questions. |
| `new GreedyShelfPlanner().Plan(menu, grill).Rounds` | The fast cook's starting answer (the seed). |
| `Consolidate(seed, grill)` | The whole questioning loop. |
| `while (rounds.Count > 1 && iterations < MaxIterations)` | "Keep asking while we have more than one round (and while we're under the 200-question cap)." |
| `TryReduceByOne(...)` | One question: "can everything fit into one fewer round?" |
| `allPieces.Sum(p => p.Area) > targetRounds * grill.Area → return false` | The area shortcut: the math says no, so don't even search. |
| `OrderByDescending(p => p.Area)...` | The re-packing order: biggest pieces first. |
| `DfsReplan(pieces, 0, targetRounds, ...)` | The search itself: place every piece into the target rounds, biggest first, over the resting spots — with the per-question budget. |
| `SubSearchNodeBudget` (200,000) | The decisions allowed for one question. |
| The commit block (`rounds[i].Add(p)` …) | "Yes!" — throw away the old plan and keep the new, smaller one. |
| `RebuildOccupancies(rounds, grill)` | Redraw each grill's map so the next question starts from the new plan. |
| `IsProvenOptimal: best.Count == lowerBound` | The honesty clause: "proven" only when the plan has reached the area floor. |

The search reuses the same shared machinery as the other planners: `RoundOccupancy`
(the grill map) and `EnumerateSkylinePositions` (the "resting spots only" rule, so a
piece never floats or can be slid down).

## 5. The numbers

- **Speed:** about 2 ms per menu (median, over repeated runs of the 15-menu
  fixture) — usually one or two re-packing questions per menu, and many questions are
  settled by the area shortcut alone.
- **Quality:** 37 rounds on the 15-menu fixture — **exactly the floor, i.e. optimal
  for every menu** — versus 39 for greedy.
- **Guarantees:** a valid plan that is never worse than greedy's; "proven optimal"
  exactly when it reached the floor. It is not a general proof engine (if the search
  budget runs out on a question, it stops and reports what it has).

## 6. How the five planners relate

- `greedy` makes one fast, never-revisited pass → a good plan, fast, unproven.
- `maxrects` makes one fast pass too, but from a different angle (biggest free
  rectangles) → a good plan, fastest of all, a different layout.
- `optimized` takes greedy's plan and *challenges it* one round at a time → usually
  the best plan, still fast.
- `exact` searches for the best plan directly and can *prove* it → the definitive
  answer, with a safety budget.
- `portfolio` runs all four above and keeps the best plan → the best available answer,
  proven whenever any of them reaches the floor.

`greedy`, `optimized` and `exact` share the same grill map (`RoundOccupancy`), the same
biggest-first piece ordering, and the same "pieces must rest, never float" rule — they
differ only in how much they are willing to think. `maxrects` keeps its own map of free
rectangles, and `portfolio` contains no packing logic at all.
