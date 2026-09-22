# The MaxRects Planner (`maxrects`)

> "Keep a map of the biggest empty spaces. When a new piece comes in, park it at the
> corner of the empty space that wastes the least."

This planner is a different style of [greedy](greedy-planner.md) packing. Instead of
scanning the whole grill for a spot, it keeps a small list of the *biggest* free
rectangles and only ever considers their corners. It is by far the fastest planner we
have (about 0.02 ms per menu) and produces a different packing than the shelf planner —
which is exactly why the [portfolio](portfolio-planner.md) likes having it along.

---

## 1. The map of empty spaces

Imagine a parking lot. The attendant doesn't drive around the whole lot for every new
car; they keep a notepad of the **big open patches** ("the north field, the strip by
the west wall, the gap between rows 3 and 4").

A *maximal free rectangle* is one of those open patches: a free rectangle that cannot
be grown in any direction without hitting meat or the grill's edge. At the start there
is exactly one: the whole grill (30 × 20).

When a piece is placed, the map is updated:

- open patches that don't touch the piece stay as they are;
- a patch the piece *does* touch gets cut into up to four smaller patches
  (left of the piece, right of it, below it, above it);
- any patch that is now fully inside another patch is deleted — only the biggest
  patches are kept.

So the map always lists exactly the biggest free rectangles, and it stays short
(usually a handful of rectangles, no matter how many pieces are down).

## 2. Choosing a spot: the "leftover short side" rule

For a new piece, the candidate spots are the **bottom-left corners of the open
patches**, in both orientations of the piece. Each candidate is scored by asking:
*"after I place the piece here, how much free space is left, and what is the
shorter side of that leftover?"*

The piece goes to the candidate with the **smallest leftover short side**.

Why? A leftover that is 14 wide but only 2 tall is nearly useless (most pieces are
taller than 2 cm); a leftover that is 5 × 5 is still usable for small pieces.
Minimizing the short side keeps the free space *usable* instead of turning it into
long, thin slivers. (Computer scientists call this variant **MaxRects BSSF** —
"best shortest side".)

Just like the shelf planner, when several grills are in use the piece goes to the
grill whose best candidate has the best score; if no grill has any candidate, a new
grill is started.

## 3. A complete example, step by step

Same menu as the [greedy doc](greedy-planner.md): **2 rumpsteaks (15×7) and 4 sausages
(6×3)**.

Step 1 — sort: rumpsteaks first, then the sausages (same as everywhere).

Step 2 — rumpsteak #1: the only open patch is the whole grill. Laid straight (15×7)
the leftover is 15 × 13 → shorter side 13. Turned (7×15) the leftover is 23 × 5 →
shorter side **5**. So it goes down **turned**, at the top-left corner.

Step 3 — rumpsteak #2: same calculation on the remaining patch → also turned, right
next to the first one. The two steaks now stand side by side, 14 wide × 15 tall,
leaving a 16-wide column to their right and a 5 cm strip along the bottom.

Steps 4–7 — the sausages: the first three find the bottom strip (each 6×3, side by
side); the last one scores best in the little 6-wide pocket at the top right.

The result — **one round**:

```
AAAAAAABBBBBBB........FFFFFF......
AAAAAAABBBBBBB........FFFFFF......
AAAAAAABBBBBBB........FFFFFF......
AAAAAAABBBBBBB....................
AAAAAAABBBBBBB....................
AAAAAAABBBBBBB....................
AAAAAAABBBBBBB....................
AAAAAAABBBBBBB....................
AAAAAAABBBBBBB....................
AAAAAAABBBBBBB....................
AAAAAAABBBBBBB....................
AAAAAAABBBBBBB....................
AAAAAAABBBBBBB....................
AAAAAAABBBBBBB....................
AAAAAAABBBBBBB....................
CCCCCCDDDDDDEEEEEE............
CCCCCCDDDDDDEEEEEE............
CCCCCCDDDDDDEEEEEE............
..............................
..............................
```

`A,B` = rumpsteaks (turned, 7×15), `C,D,E` = sausages along the bottom, `F` = the
sausage in the top-right pocket, `.` = empty.

Notice how *different* this looks from the shelf planner's answer (two flat steaks
across the top, sausages in a row beneath). Both are one round; MaxRects simply
makes different "best right now" choices — and sometimes those different choices are
the ones that let everything fit.

### The menu that fools the shelf planner

On the spare-rib menu from the [greedy doc](greedy-planner.md) — where the shelf
planner needs 2 rounds — MaxRects also finds the **1-round** packing (pork chop
standing up along the right edge, steak turned in the middle):

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

(`A,B` = spare ribs, `C` = pork chop, `D` = sirloin, `E` = steak, `F` = sausage,
`G,H` = patties.) Same answer, found by a completely different line of reasoning.

## 4. How the code does this

The planner lives in
`src/GrillMaster.Application/Features/Planning/Planners/MaxRectsPlanner.cs`.
Note it does **not** use the shared `RoundOccupancy` grid — it keeps its own map:

| Code | What it is in the story |
|------|--------------------------|
| `Plan(menu, grill)` | The attendant taking over the lot. |
| `GrillPlannerHelpers.OrderPieces(pieces)` | The usual biggest-first queue. |
| `freeRects` | The notepad: one list of open patches per grill in use. |
| `new List<MaxRect> { new(0, 0, grill.Width.Value, grill.Height.Value) }` | A fresh grill: one open patch, the whole grill. |
| `FindBestTarget(piece, ...)` | "Which grill has the best spot for this piece?" Checks every grill's notepad, keeps the best score. |
| `ChoosePlacement(piece, ...)` | "Where exactly?" Every open patch, both orientations; scores by the leftover short side; returns the best. |
| `Math.Min(rect.W - w, rect.H - h)` | The score: the shorter side of the leftover space. |
| `Place(round, freeRects, placement)` | Park the piece, then update the notepad. |
| The four `updated.Add(new MaxRect(...))` calls | Cutting the touched patch into left / right / below / above pieces. |
| `Prune(updated)` | Deleting any patch that is now inside a bigger one — only the biggest patches stay. |
| `IsProvenOptimal: false` | The honest disclaimer: a greedy planner, no proof. |

## 5. The numbers

- **Speed:** about **0.02 ms per menu** on the 15-menu fixture — roughly 90× faster
  than the shelf planner, because it never scans the grill's 600 squares; it only
  looks at the handful of open patches.
- **Quality:** 39 rounds on the 15-menu fixture — the same total as the shelf
  planner, reached with a different layout per menu.
- **Guarantees:** a valid plan (no overlaps, nothing off the grill), never fewer
  rounds than the lower bound, but no claim of optimality.

Its real job in the team: give the [portfolio](portfolio-planner.md) a fast,
differently-shaped answer to compare against.
