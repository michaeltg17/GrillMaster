# The Batch Planner (`batch`)

> "Never mix the meats. Grill all the sausages first — full grills of nothing but
> sausages — and only then worry about the leftovers."

This planner works **type by type**. For each kind of meat it asks a question the
other planners never ask: *"how many of this exact piece fit on one empty grill —
and where?"* It then pre-fills as many **full grills of that type** as possible with
the proven answer, and only at the end does it mix the leftovers (less than one full
grill of each type) into whatever space is left, the way the shelf planner would.

---

## 1. The question: "how many fit?"

Take one piece type, say the 6×3 sausage, and the empty 30×20 grill. The question
*"how many of these fit on one grill?"* has a definite answer, and it can be found
exactly: it is a one-round packing problem with **identical** pieces, and identical
pieces are easy for a backtracking search (if sausage #2 can go where sausage #1
went, the order of the sausages doesn't matter — the search just tries positions in
one fixed order and never repeats a multiset of positions).

The search is budgeted (a node limit) and **cached**: the first time it answers for
a type it pays for the work, and every later call — other menus, other planners, the
lower bound — is free.

The answer also comes with a **witness**: the actual placement of the pieces. For the
sausage the witness is a clean 5 × 6 grid of 30 sausages (the area alone would allow
33, but the search settles on 30 — the most it can show fits):

```
A = 6x3 sausage
AAAAAABBBBBBCCCCCCDDDDDDEEEEEE
AAAAAABBBBBBCCCCCCDDDDDDEEEEEE
AAAAAABBBBBBCCCCCCDDDDDDEEEEEE
FFFFFFGGGGGGHHHHHHIIIIIIJJJJJJ
FFFFFFGGGGGGHHHHHHIIIIIIJJJJJJ
FFFFFFGGGGGGHHHHHHIIIIIIJJJJJJ
KKKKKKLLLLLLMMMMMMNNNNNNOOOOOO
KKKKKKLLLLLLMMMMMMNNNNNNOOOOOO
KKKKKKLLLLLLMMMMMMNNNNNNOOOOOO
PPPPPPQQQQQQRRRRRRSSSSSSTTTTTT
PPPPPPQQQQQQRRRRRRSSSSSSTTTTTT
PPPPPPQQQQQQRRRRRRSSSSSSTTTTTT
UUUUUUVVVVVVWWWWWWXXXXXXYYYYYY
UUUUUUVVVVVVWWWWWWXXXXXXYYYYYY
UUUUUUVVVVVVWWWWWWXXXXXXYYYYYY
ZZZZZZ[[[[[[\\\\\\]]]]]]^^^^^^
ZZZZZZ[[[[[[\\\\\\]]]]]]^^^^^^
ZZZZZZ[[[[[[\\\\\\]]]]]]^^^^^^
..............................
```

Thirty sausages, no gaps to speak of, and the search is the one that says
*"thirty, not thirty-one, not thirty-three"*.

## 2. The plan: full grills first, leftovers last

For each piece type (biggest first, as everywhere):

1. ask the question from §1: how many fit on one grill? (the **capacity**) and how?
   (the **pattern**);
2. pre-fill `count ÷ capacity` rounds by stamping that pattern;
3. the remainder (`count mod capacity` pieces of this type) goes to a leftovers pile.

Then the leftovers pile is packed the ordinary greedy way: biggest first, each piece
into the existing round that ends up fullest, a new round when nothing fits.

### Why does it do this?

A full grill of one type is *provably as full as that type allows* — no mixing can
squeeze one more sausage into round 1 of the example. So the batch planner buys
certainty for the big, repetitive part of a menu and pays for it only where the
pieces actually differ: the leftovers.

## 3. A complete example: 40 sausages

Menu: **40 sausages (6×3)**.

1. The capacity question: **30** fit on one grill (the 5 × 6 grid above).
2. `40 ÷ 30 = 1` full round, stamped from the grid. The remainder is 10.
3. The 10 leftovers start a new round: two neat rows of five.

The result — **2 rounds** (the lower bound is 2, so this is optimal for this menu):

```
round 1 (30 sausages, the proven pattern)     round 2 (the 10 leftovers)
AAAAAABBBBBBCCCCCCDDDDDDEEEEEE               AAAAAABBBBBBCCCCCCDDDDDDEEEEEE
AAAAAABBBBBBCCCCCCDDDDDDEEEEEE               AAAAAABBBBBBCCCCCCDDDDDDEEEEEE
AAAAAABBBBBBCCCCCCDDDDDDEEEEEE               AAAAAABBBBBBCCCCCCDDDDDDEEEEEE
FFFFFFGGGGGGHHHHHHIIIIIIJJJJJJ               FFFFFFGGGGGGHHHHHHIIIIIIJJJJJJ
FFFFFFGGGGGGHHHHHHIIIIIIJJJJJJ               FFFFFFGGGGGGHHHHHHIIIIIIJJJJJJ
FFFFFFGGGGGGHHHHHHIIIIIIJJJJJJ               FFFFFFGGGGGGHHHHHHIIIIIIJJJJJJ
KKKKKKLLLLLLMMMMMMNNNNNNOOOOOO
KKKKKKLLLLLLMMMMMMNNNNNNOOOOOO
KKKKKKLLLLLLMMMMMMNNNNNNOOOOOO
PPPPPPQQQQQQRRRRRRSSSSSSTTTTTT
PPPPPPQQQQQQRRRRRRSSSSSSTTTTTT
PPPPPPQQQQQQRRRRRRSSSSSSTTTTTT
UUUUUUVVVVVVWWWWWWXXXXXXYYYYYY
UUUUUUVVVVVVWWWWWWXXXXXXYYYYYY
UUUUUUVVVVVVWWWWWWXXXXXXYYYYYY
ZZZZZZ[[[[[[\\\\\\]]]]]]^^^^^^
ZZZZZZ[[[[[[\\\\\\]]]]]]^^^^^^
ZZZZZZ[[[[[[\\\\\\]]]]]]^^^^^^
```

(One letter per sausage in round 1 — 30 of them; round 2 shows 10.)

## 4. How the code does this

The planner (including `MaxPattern`) lives in
`src/GrillMaster.Application/Features/Plans/Planners/BatchPlanner.cs`; the
one-round capacity search it builds on lives in
`src/GrillMaster.Application/Features/Plans/GrillPlannerHelpers.cs`:

| Code | What it is in the story |
|------|--------------------------|
| `Plan(menu, grill)` | The cook taking the order, one meat at a time. |
| `MaxPattern(type, grill, count)` | The question of §1: returns the proven count **and** the witness pattern. Exact search; if the top of the range is proven not to fit it binary-searches down; if the budget runs out it falls back to a greedy shelf answer. |
| `var full = (j - i) / fits;` | How many full grills this type needs. |
| the `for (var r = 0; r < full; r++)` loop | Stamping the pattern into one round at a time. |
| `remainder.Add(...)` | The leftovers pile (`count mod capacity` per type). |
| `FindBestRound(piece, ...)` | The leftovers' placement: the existing round that ends up fullest after the piece is added. |
| `IsProvenOptimal: false` | The honest disclaimer: the *patterns* are proven, the whole plan is not. |

The same one-round capacity search is what computes the **per-type part of the lower
bound** for every planner: *"this menu has 40 sausages and 30 is the most a grill
holds, so you need at least ⌈40/30⌉ = 2 rounds — sausages alone."*

## 5. The numbers

- **Speed:** about **0.2 ms per menu** on the 15-menu fixture. The capacity searches
  are cached, so after the first menu they are essentially free; the rest is stamping
  patterns and one small greedy pass.
- **Quality:** **62 rounds** on the 15-menu fixture — the weakest total in the
  family. That is the honest price of the no-mixing rule on *mixed* menus: a full
  grill of one type leaves gaps (a full sausage grill uses 540 of its 600 cm²) that a
  mixed plan could fill with the corners of other pieces. On menus dominated by one
  type — many sausages, many patties — the rule pays off, because the full grills are
  provably maximal for that type.
- **Guarantees:** a valid plan, never fewer rounds than the lower bound; each stamped
  round is a proven-maximal tiling of its type, but the plan as a whole is not
  claimed optimal.

Its real job in the team: be the planner that *answers the per-type question exactly*
— and to let that answer (and its cache) serve the lower bound and the
[portfolio](portfolio-planner.md) indirectly.
