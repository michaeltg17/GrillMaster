# The Greedy Planner (`greedy`)

> "Lay the big things down first, tuck each new piece into the snuggest free spot,
> and if it doesn't fit anywhere, get out another tray."

That is the whole strategy. No looking back, no second-guessing. It is fast, it is
intuitive, and it is *almost* always good — but it can be fooled, and it will never
*prove* it did the best job. This file explains exactly how it works, with pictures.

---

## 1. The setup

The grill is 30 cm wide and 20 cm tall. Imagine it as a sheet of graph paper where every
square is 1 cm × 1 cm. That's **600 squares**.

A *round* is one batch of meat on the grill at the same time. If our meat needs more
room than one grill, we cook it in several rounds (we take the first batch off, put the
next batch on). Fewer rounds is better.

A piece is a rectangle. A steak that is 15 cm by 7 cm covers 15 × 7 = **105 squares**
(we call that its *area*). It may be laid down straight (15 wide, 7 tall) or turned a
quarter-turn (7 wide, 15 tall) — whichever fits.

## 2. The plan, in plain words

1. **Sort the meat from biggest to smallest.** (Area, then length, then name — the
   details only matter for tie-breaking.)
2. Take the biggest piece. If no grill is in use, put it in the top-left corner of a
   fresh grill. If there are already pieces on the grill, find the spot that leaves the
   **least wasted space around it**.
3. Take the next piece, do the same.
4. If a piece fits in no free spot on any grill, **start a new grill** (a new round) and
   put it in the top-left corner.
5. Until every piece is placed.

The name *greedy* comes from the fact that each decision is made to be the best possible
**right now**, without thinking about the pieces still waiting in the queue.

### Why biggest first?

Think of loading a van: you put the sofa in before the pillows, because a big thing left
for last often has nowhere to go. If the big pieces are placed first, the small pieces
can fill the awkward gaps the big ones leave behind.

## 3. A complete example, step by step

Our menu: **2 rumpsteaks (15×7) and 4 sausages (6×3)**.

Step 1 — sort: rumpsteaks first (105 cm² each), then the sausages (18 cm² each).
Order: `Rumpsteak, Rumpsteak, Sausage, Sausage, Sausage, Sausage`.

Step 2 — rumpsteak #1: no grill exists yet, so it takes the top-left corner (0,0).

Step 3 — rumpsteak #2: only one grill exists. Where is the snuggest spot? Right next to
the first steak, still hugging the top edge — at (15,0). The two steaks now form a
30×7 strip across the top.

Steps 4–7 — the sausages, one at a time, each in the snuggest remaining spot: directly
below the steaks, in a row: (0,7), then (6,7), then (12,7), then (18,7).

The result — **one round** (each square is 1 cm):

```
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
AAAAAAAAAAAAAAAABBBBBBBBBBBBBBBB
CCCCCCDDDDDDEEEEEEFFFFFF......
CCCCCCDDDDDDEEEEEEFFFFFF......
CCCCCCDDDDDDEEEEEEFFFFFF......
..............................
..............................
..............................
..............................
..............................
..............................
..............................
..............................
..............................
..............................
```

`A` = rumpsteak 1, `B` = rumpsteak 2, `C–F` = sausages, `.` = empty grill.
Everything fit in one round — and since the meat (282 cm²) is smaller than the grill
(600 cm²), one round is the best possible.

### What is a "snuggest spot"?

When several free spots work, the planner scores each one and picks the lowest score.
The score is: **count the empty squares directly *above* the piece, plus the empty
squares to its *left*** — with the space above counting much more heavily.

That means a piece is happiest when:

- it touches the top edge (nothing free above it), and
- it touches something on its left (nothing free to its left).

In other words: *drop the piece until it rests on the floor or on another piece, and
slide it left until it bumps into something*. Pieces stack into neat horizontal rows —
"shelves" — which is why this planner is called the **shelf** planner.

### What does "which grill" mean?

When several grills are in use, the piece goes to the grill that would be **fullest
afterwards** (least free squares left). We want to finish grills off before starting
new ones.

## 4. Where greedy goes wrong

Greedy never thinks about the future. Sometimes a spot that looks perfect *now* leaves
a gap that is the wrong shape for a piece coming later. Here is a real, verified
example from this codebase.

**The menu:** 2 spare ribs (24×5), 1 pork chop (20×6), 1 sirloin (18×6), 1 steak
(10×5), 1 sausage (6×3), 2 patties (4×4).

Total meat: **568 cm²**, which is less than the grill's 600 cm² — so *by area*, all of
it should fit in **one round**.

Greedy places the pieces biggest-first:

1. spare rib → top-left, (0,0).
2. spare rib → right below it, (0,5).
3. pork chop (20×6) → the snuggest spot is flat along the bottom-left, (0,10).
4. sirloin (18×6) → turned a quarter-turn, it stands up along the right edge, (24,0).
5. **steak (10×5) → does not fit anywhere on grill #1!**

Why not? Look at the free space left on grill #1 after step 4:

- a **4 cm wide** column down the right-middle,
- a **4 cm tall** strip along the bottom,
- a 6×2 corner.

The steak needs a gap 10 wide and 5 tall (or 5 wide and 10 tall). A 4 cm wide column is
too narrow; a 4 cm tall strip is too short. So greedy — which never takes pieces back
out — opens **grill #2** for a single steak. The sausage and the two patties (small
enough) still sneak into grill #1's gaps afterwards.

Greedy's two-round answer (each square is 1 cm; every letter is one piece):

```
grill 1 (7 pieces)                grill 2 (just the steak)
AAAAAAAAAAAAAAAAAAAAAAAADDDDDD    AAAAAAAAAA....................
AAAAAAAAAAAAAAAAAAAAAAAADDDDDD    AAAAAAAAAA....................
AAAAAAAAAAAAAAAAAAAAAAAADDDDDD    AAAAAAAAAA....................
AAAAAAAAAAAAAAAAAAAAAAAADDDDDD    AAAAAAAAAA....................
AAAAAAAAAAAAAAAAAAAAAAAADDDDDD    AAAAAAAAAA....................
BBBBBBBBBBBBBBBBBBBBBBBBDDDDDD    ..............................
BBBBBBBBBBBBBBBBBBBBBBBBDDDDDD    ..............................
BBBBBBBBBBBBBBBBBBBBBBBBDDDDDD    ..............................
BBBBBBBBBBBBBBBBBBBBBBBBDDDDDD    ..............................
BBBBBBBBBBBBBBBBBBBBBBBBDDDDDD    ..............................
CCCCCCCCCCCCCCCCCCCCFFFFDDDDDD    ..............................
CCCCCCCCCCCCCCCCCCCCFFFFDDDDDD    ..............................
CCCCCCCCCCCCCCCCCCCCFFFFDDDDDD    ..............................
CCCCCCCCCCCCCCCCCCCCFFFFDDDDDD    ..............................
CCCCCCCCCCCCCCCCCCCC....DDDDDD    ..............................
CCCCCCCCCCCCCCCCCCCC....DDDDDD    ..............................
EEEEEGGGG..............DDDDDD     ..............................
EEEEEGGGG..............DDDDDD     ..............................
EEEEEGGGG....................     ..............................
......GGGG....................    ..............................
```

`A,B` = spare ribs, `C` = pork chop, `D` = sirloin, `E` = sausage, `F,G` = patties,
`A` on grill 2 = the steak (letters restart at 1 for each grill), `.` = empty grill.

The fix is simple to *see* but impossible for greedy to *find*: stand the pork chop up
vertically along the right edge (6 wide × 20 tall) instead of laying it flat. That
opens the middle of the grill, where the steak fits comfortably. Then **everything
fits in one round** — which the `exact` planner actually finds:

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

`A,B` = spare ribs, `C` = pork chop (standing up along the right edge), `D` = sirloin,
`E` = steak (turned, middle-right), `F` = sausage, `G,H` = patties, `.` = empty.
One round. Done.

This is the fundamental trade-off: greedy makes each choice in an instant and never
revisits it. When the choices line up (which is most of the time) it's excellent; when
they don't, it pays for it with an extra round. On the full 15-menu fixture, greedy
needs **39 rounds** where the best found is **38** — usually it ties the best answer,
occasionally it is a round or two off.

## 5. How the code does this

The whole planner lives in
`src/GrillMaster.Application/Features/Plans/Planners/GreedyShelfPlanner.cs`.
Here is each piece of code translated back into the story:

| Code | What it is in the story |
|------|--------------------------|
| `Plan(menu, grill)` | The cook taking on the whole job. |
| `menu.ExpandPieces()` | Unpacking the menu: "2 rumpsteaks" becomes two separate steak pieces. |
| `GrillPlannerHelpers.OrderPieces(pieces)` | Step 1: sorting the meat biggest-first. |
| `GrillPlannerHelpers.ComputeLowerBound(...)` | The floor: the larger of total meat area ÷ 600 and the per-type capacity count. Reported with the plan (but not used to place anything). |
| `rounds` / `occupancies` | The list of grills in use, and each grill's map of which squares are taken. |
| `FindBestRound(piece, ...)` | "Which of my grills should this piece go on?" Tries the piece on every grill, keeps the one that would be fullest afterwards (`freeAfter = 600 − used − piece`). |
| `occupancy.FindBestPosition(piece)` | "And exactly *where* on that grill?" Looks at every possible position in both orientations, scores them, returns the snuggest. |
| `Score(x, y, w, h)` | The snugness score: empty squares above × grill-width, plus empty squares to the left. Lower is snuggier. |
| `occupancy.MarkOccupied(...)` | Draws the piece onto the grill's map so the next piece knows the squares are taken. |
| `new GrillRound()` | "It fits nowhere — get me another grill." |
| `IsProvenOptimal: false` | The honest disclaimer: greedy can't prove its answer is the best possible. |

The grill's map itself is `RoundOccupancy`
(`src/GrillMaster.Application/Features/Plans/RoundOccupancy.cs`): a 30×20 grid of
"taken / free" squares shared by `greedy` and `exact`.

## 6. The numbers

- **Speed:** about 2 ms per menu (median, over repeated runs of the 15-menu fixture).
  It never searches or backtracks — every piece gets exactly one decision.
- **Quality:** 39 rounds on the 15-menu fixture (best found: 38). It ties the best
  answer on most menus and is off by a round on two of them.
- **Guarantees:** a valid plan (no overlaps, nothing off the grill), never fewer rounds
  than the lower bound, but no claim that it is optimal.

If you want a better answer on a small-to-medium menu, look at
[the exact planner](exact-planner.md) — it improves on this plan and can prove the
answer is the best possible.
