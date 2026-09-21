# Grill Master — how the three planners think

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

We have three planners. They are like three different cooks with the same ingredients:

| Planner     | One-line idea                                                                    | Speed | Quality                                                                  |
|-------------|----------------------------------------------------------------------------------|-------|--------------------------------------------------------------------------|
| `greedy`    | Put the biggest pieces down first, tuck each one into the tightest free spot.   | Fast  | Very good, but it can be 1–2 rounds off the best and it can't prove it.  |
| `exact`     | Try arrangements one by one, but skip millions of pointless ones with smart rules. | Slow  | **Proves** the answer is the best possible (within its time budget).     |
| `optimized` | Take `greedy`'s answer, then keep asking "can we do it with one fewer round?".  | Fast  | Reaches the best possible on all our menus.                              |

Each planner is explained in its own file, written for a person who has never coded or done
math — with pictures and worked examples:

- [greedy-planner.md](greedy-planner.md) — the quick, intuitive cook.
- [exact-planner.md](exact-planner.md) — the cook who refuses to stop until they can *prove* they did their best.
- [optimized-planner.md](optimized-planner.md) — the cook who starts with a good guess and keeps improving it.

Which one do you pick? The program runs one planner at a time; you choose it in
`src/GrillMaster.Console/appsettings.json` (`"Planner": "greedy" | "exact" | "optimized"`).
`greedy` is the default (fastest). `optimized` is the best everyday choice: it matches the
proven optimum on the full 15-menu fixture (185 rounds) in about 2 ms per menu.
