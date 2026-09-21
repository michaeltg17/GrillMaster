# The Guillotine Planner (`guillotine`)

> "Only cut a rectangle in a straight line, corner to corner — and always take a piece
> from a corner of what's left."

This planner packs with a restriction borrowed from paper cutting: every piece must sit
in a **corner of a free rectangle**, so that one straight cut can always slice it off
from the remaining space. The restriction sounds limiting, but it is what keeps the
planner's bookkeeping tiny: the free space of a round is always a set of *disjoint*
rectangles that only ever split, never merge, never overlap.

---

## 1. The straight-cut rule

Imagine a sheet of paper and a guillotine (the kind of paper cutter that slices a full
straight line in one stroke). You can only ever cut a rectangle into two smaller
rectangles — no L-shaped scraps, no jigsaw.

Now imagine the grill as the paper and the meat pieces as the shapes you want to cut
out. The rule is: **a piece may only be placed in a corner of one of the free
rectangles**. Why? A piece in a corner of a rectangle is exactly the shape that one
straight cut separates from the rest:

```
before cutting                after one straight cut
┌──────────────────┐          ┌────────────┬──────────┐
│            ┌───┐ │          │            │ leftover │
│            │ P │ │          │            │ strip    │
│            └───┘ │          │            │          │
│                  │   ──►    ├────────────┼──────────┤
│                  │          │ leftover   │          │
│                  │          │ strip      │ (under   │
│                  │          │            │  the     │
└──────────────────┘          │            │  piece's │
P = piece in the corner       │            │  columns)│
                              └────────────┴──────────┘
```

Because every placement is a corner placement, the leftover space is *always* a set of
rectangles — and a piece placed in a corner splits its rectangle into **at most two**
new rectangles (a full-height strip and a strip under the piece's own columns). The
free space of a round is kept as exactly that: a **disjoint guillotine partition**.

## 2. The bookkeeping: rectangles that only split

At the start of a round there is one free rectangle: the whole grill (30 × 20).

When a piece is placed in a corner of one free rectangle:

- that rectangle is removed from the list;
- its leftover (up to two rectangles, sketched above) is added.

Nothing else changes. There is no overlap checking against a grid, no "is this
rectangle inside another?" cleanup pass — the rectangles are *disjoint by
construction*, so the list can only grow by one entry per piece. A round with 40
pieces has at most 40 rectangles in its list, no matter what.

## 3. Choosing a spot: keep the narrowest strip wide

For a new piece, the candidates are the **four corners of every free rectangle**, in
both orientations of the piece. Each candidate is scored by asking:

*"after I place the piece here, what is the narrowest leftover strip I leave behind —
and how wide is its short side?"*

The piece goes to the candidate with the **widest narrowest strip**.

Why? A corner placement leaves two leftover strips: a full-height one (left or right
of the piece) and a full-width one (above or below it). The *narrowest* of those is
the first one future pieces will struggle with — so we keep it as wide as possible.
If the piece fills its rectangle exactly, the score is "perfect" and beats
everything else.

(That is the opposite of the [maxrects](maxrects-planner.md) rule, which *minimizes*
the leftover short side. Same intuition — "don't create useless slivers" — applied to
a different family of placements.)

As always, when several rounds are open the piece goes to the round with the best
candidate; if no round has any candidate, a new round starts.

## 4. A complete example, step by step

Menu: **2 rumpsteaks (15×7) and 4 sausages (6×3)** — the same menu as the
[maxrects doc](maxrects-planner.md), packed by corner cuts this time.

Step 1 — sort: rumpsteaks first, then the sausages.

Step 2 — rumpsteak A: one free rectangle, the whole grill. Laid straight in the
top-left corner, it leaves a 15-wide full-height strip and a 13-tall strip under the
piece → the narrowest strip's short side is 13. Laid turned (7×15) it would leave a
23-wide strip and a 5-tall strip → 5. 13 wins: A goes down **straight**, top-left.

Step 3 — rumpsteak B: the free rectangles are the 15×20 right strip and the 15×13
bottom strip. B straight in the right strip fills its full width and leaves a 15×13
strip → score 13. The bottom strip would leave a 15×6 strip → score 6. B goes to the
right strip.

Step 4 — sausage C: every corner of the two 15-wide rectangles scores the same (9:
a 9-wide strip plus a 10-tall strip); the first candidate wins, so C takes the
top-left of the left strip.

Step 5 — sausage D: the same 9, at the top-left of the right strip. The narrow 9-wide
middle strip only offers 3–6, so D goes right.

Step 6 — sausage E: now the best score is 6 — and it is won by **standing the sausage
up** (3×6) in the middle strip: a vertical sausage there leaves a 6-wide strip
beside it, while lying flat would leave only a 3-wide one.

Step 7 — sausage F: several corners still score 6; the first wins — the spot directly
below C.

The result — **one round** (the lower bound is 1):

```
AAAAAAAAAAAAAAABBBBBBBBBBBBBBB
AAAAAAAAAAAAAAABBBBBBBBBBBBBBB
AAAAAAAAAAAAAAABBBBBBBBBBBBBBB
AAAAAAAAAAAAAAABBBBBBBBBBBBBBB
AAAAAAAAAAAAAAABBBBBBBBBBBBBBB
AAAAAAAAAAAAAAABBBBBBBBBBBBBBB
AAAAAAAAAAAAAAABBBBBBBBBBBBBBB
CCCCCCEEE......DDDDDD.........
CCCCCCEEE......DDDDDD.........
CCCCCCEEE......DDDDDD.........
FFFFFFEEE.....................
FFFFFFEEE.....................
FFFFFFEEE.....................
..............................
..............................
..............................
..............................
..............................
..............................
```

`A,B` = rumpsteaks straight across the top, `C,D,F` = sausages lying flat,
`E` = the sausage standing up in the middle, `.` = empty. Notice again how a
different planner gets a different shape for the same one-round answer — and how,
after every single placement, the empty space stayed a set of disjoint rectangles
(the list never had more than a handful of entries).

### Why the disjoint partition matters

An earlier version of this planner kept *overlapping* candidate rectangles and pruned
the contained ones after every placement — a cleanup pass that costs quadratically in
the number of placed pieces. On a 1000-piece test menu that version took minutes to
hours; the disjoint partition takes the same menu in well under a second (there is a
test that pins this: `GuillotinePlannerTests.PlansLargeMenu_Quickly` requires 1000
pieces in under 5 seconds).

## 5. How the code does this

The planner lives in
`src/GrillMaster.Application/Features/Planning/Planners/GuillotinePlanner.cs`. It does
**not** use the shared `RoundOccupancy` grid — it keeps its own rectangle lists:

| Code | What it is in the story |
|------|--------------------------|
| `Plan(menu, grill)` | The cutter taking over the paper stack. |
| `GrillPlannerHelpers.OrderPieces(pieces)` | The usual biggest-first queue. |
| `freeRects` | The per-round list of free rectangles — always a disjoint partition. |
| `FindBestTarget(piece, rounds, freeRects)` | "Which round has the best corner for this piece?" Best score across every round. |
| `ChoosePlacement(piece, freeRects)` | "Where exactly?" Every rectangle, both orientations, four corners; keeps the widest narrowest strip. |
| `Score(rect, w, h, dx, dy)` | The score: the narrowest leftover strip's short side; `int.MaxValue` for an exact fill. |
| `Place(round, freeRects, rectIndex, placement)` | Cut the piece out: remove the rectangle, add its leftover (at most two new rectangles). |
| `GRect` | One free rectangle (x, y, width, height). |
| `IsProvenOptimal: false` | The honest disclaimer: a greedy planner, no proof. |

## 6. The numbers

- **Speed:** about **0.05 ms per menu** on the 15-menu fixture — in the same league
  as `maxrects`, because it never scans the grill's 600 squares; it only looks at the
  handful of free rectangles. Linear in the number of pieces, no matter how big the
  menu gets.
- **Quality:** 39 rounds on the 15-menu fixture — the same total as `greedy` and
  `maxrects`, reached with corner-only placements.
- **Guarantees:** a valid plan (no overlaps, nothing off the grill), never fewer
  rounds than the lower bound, but no claim of optimality.

Its real job in the team: another fast, differently-shaped answer for the
[portfolio](portfolio-planner.md) to compare against — and proof that a hard
constraint (straight cuts only) does not cost much here, because the grill is small
and the pieces are few.
