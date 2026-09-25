# Menu 01: the optimum is 4 rounds

**Status: proven.** A 3-round packing of Menu 01 on the standard 30×20 grill does not
exist, by two independent routes: the hand proof in §3 and the exhaustive machine
proof in §4 (a different algorithm from both the planner's joint search and the
CP-SAT oracle). Since the planner's greedy pass finds a 4-round plan, the optimum is
exactly **4**.

The machine proof is implemented as the planner's third phase (§6), so the product
itself can certify tight instances like this one instead of returning an honest
`IsProvenOptimal = false`.

## 1. The instance

Standard grill: 30 × 20 cm, area 600 cm². Menu 01 (34 pieces, total area 1791 cm²):

| Piece | Size (cm) | Count | Area each |
|-------|-----------|-------|-----------|
| Sausage | 22 × 5 | 10 | 110 |
| Rumpsteak | 15 × 7 | 1 | 105 |
| Chicken | 12 × 5 | 2 | 60 |
| Steak | 10 × 5 | 5 | 50 |
| Veal | 8 × 4 | 1 | 32 |
| Paprika sausage | 6 × 3 | 3 | 18 |
| Shrimp | 5 × 3 | 2 | 15 |
| Chipolata | 5 × 2 | 10 | 10 |

The lower bound is 3: area floor ⌈1791/600⌉ = 3, and the sausage capacity floor
⌈10/4⌉ = 3 (at most 4 sausages fit on one grill — see Lemma 1). The planner's greedy
pass finds a 4-round plan, and its verification pass must then prove that 3 rounds
are impossible. It cannot, within budget: the 3-round verification space is over
10,000,000,000 decisions (a 10-billion-node run across all 16 cores of a Ryzen
9800X3D, ~23 minutes, did not exhaust it), and an external CP-SAT solver given
1500 seconds returns *unknown* on the 3-round decision problem (it does find the
4-round plan). So the planner honestly returns **not proven** — and the question
this document answers is whether 3 rounds really are infeasible.

## 2. Notation

- The grill is the integer rectangle [0,30] × [0,20]; pieces are axis-aligned
  rectangles at integer coordinates, in either orientation when both fit.
- A **round** is a set of non-overlapping pieces inside the grill.
- In any 3-round packing, each round's area aᵢ satisfies **591 ≤ aᵢ ≤ 600**:
  the total is 1791 and no round exceeds 600, so no round can be below
  1791 − 2·600 = 591. This ten-square window is the engine of the proof.
- A piece **S** (sausage) is 22 wide and 5 tall; call its y-extent a **band**.

## 3. Hand proof that 3 rounds are infeasible

**Lemma 1 (sausage structure).** A sausage cannot rotate (22 > 20), so every
sausage is a 22 × 5 strip. Two sausages in one round cannot overlap in y
(their 22-wide x-intervals always intersect, since 22 + 22 > 30), so the bands
are disjoint and each round holds at most ⌊20/5⌋ = **4 sausages**. Ten sausages
in three rounds therefore split as **(4,3,3) or (4,4,2)** (sorted counts
a ≤ b ≤ c: c = 4 is forced by c ≤ 4 and c ≥ ⌈10/3⌉, and then b ≥ 3).

**Lemma 2 (no round holds 3 sausages and the rumpsteak).** Let a round contain
three sausages (bands: 15 y-rows in total, 5 y-rows of **gap** left) and the
rumpsteak R.

- *R portrait (15 × 7).* The three disjoint bands cover 15 rows; R's y-interval
  has length 7. Since 15 + 7 = 22 > 20, R's y-interval intersects some band B.
  In the intersecting rows, R's x-interval (15) and B's x-interval (22) must
  intersect, since 15 + 22 = 37 > 30. Collision.
- *R landscape (7 × 15).* Let J be R's y-interval (15 rows). Every row
  r ∈ J ∩ (band rows) contains a sausage (22 cells) and R (7 cells — R's x-interval
  is forced disjoint from that sausage's, because r is a row of J and of that
  band). Such a row has 30 − 22 − 7 = **1 free cell**, a width-1 cell that no
  piece can use (every piece is at least 2 × 2, and any piece covering that cell
  would need a neighbouring free cell in the same row). Now
  |J ∩ (band rows)| = 15 − |J ∩ (gap rows)| ≥ 15 − 5 = **10**, so at least 10
  cells are unusable. Free area in the round is 600 − 3·110 − 105 = 165, so the
  usable free area is at most 165 − 10 = **155**. But the round's remaining
  pieces must have area a − 435 ≥ 591 − 435 = **156**. 156 > 155. Impossible.

**Lemma 3 (no round holds 4 sausages and the rumpsteak).** Four bands fill all
20 rows.

- *R portrait:* every row of R contains a sausage; in those rows
  15 + 22 > 30 forces a collision.
- *R landscape:* let J be R's y-interval; every row of J contains a sausage, so
  R's 7-wide x-interval must avoid **all four** sausage x-intervals
  [oᵢ, oᵢ+22]. Being a connected interval of length 7, it lies entirely in one
  side of every band: in [0, min oᵢ] (width min oᵢ) or in
  [max oᵢ + 22, 30] (width 8 − max oᵢ). Hence **min oᵢ ≥ 7 or max oᵢ ≤ 1**; by
  mirror symmetry take max oᵢ ≤ 1. The 15 rows of J each have 30 − 22 − 7 = 1
  free cell (unusable, as in Lemma 2). The 5 rows outside J have 8 free cells
  ([0, oᵢ] ∪ [oᵢ+22, 30], the right part at least 7 wide — usable). So the
  round's packable area is at most 5·8 = **40**, while its remaining pieces need
  a − 545 ≥ 591 − 545 = **46**. 46 > 40. Impossible.

**Lemma 4 (in a 4-sausage round, chicken and steak cannot coexist).** In a
4-sausage round every row's free set is [0, oᵢ] ∪ [oᵢ+22, 30] (widths oᵢ and
8 − oᵢ, a gap of 22 − oᵢ ≥ 14 between them). Every piece is at most 12 wide <
14, so **every piece lies entirely in one side**. A chicken (12 × 5) cannot use
a 12-wide side (max width 8), so it must sit 5 × 12: 12 consecutive rows on one
side of width ≥ 5 (i.e. o ≥ 5 on those rows, or o ≤ 3). A steak (10 × 5) cannot
use a 10-wide side, so it must sit 5 × 10: 10 consecutive rows on one side of
width ≥ 5.

- Both on the same side: stacked, 12 + 10 = 22 > 20 rows; side by side,
  5 + 5 = 10 > 8 width. Impossible.
- On opposite sides: the chicken's 12 rows need o ≥ 5 (or o ≤ 3) and the
  steak's 10 rows need the *opposite* inequality on o — disjoint row sets of
  sizes 12 and 10 in 20 rows. 12 + 10 = 22 > 20. Impossible.

**Lemma 5 (in the (4,4,2) split, the 2-sausage round is one of five
compositions).** By Lemma 3 the rumpsteak must sit in the 2-sausage round. By
Lemma 4 each 4-sausage round holds at most one of {chicken, steak} (the pool has
2 chickens + 5 steaks = 7), so the 2-sausage round holds **at least 5** of them,
with c ≤ 2 chickens, t ≤ 5 steaks: (c, t) ∈ {(2,3), (1,4), (0,5)}. The round's
area a ∈ [591, 600] and 2·110 + 105 = 325 already, so chicken+steak+small pieces
must fill 266–275 cm², where "small" = veal (32), paprika (18), shrimp (15),
chipolata (10):

| (c, t) | C+T area | small must be | round |
|--------|----------|---------------|-------|
| (2,3) | 270 | 0 (smallest piece is 10) | 2S + R + 2C + 3T = 595 |
| (1,4) | 260 | 6–15 → one K (10) or one X (15) | 2S + R + C + 4T + K = 595, or + X = 600 |
| (0,5) | 250 | 16–25 → one P (18) or X+K (25) | 2S + R + 5T + P = 593, or + X + K = 600 |

**Lemma 6 (the 2-sausage round is infeasible in all five cases).** Two bands
leave **10 band rows** (each row's free set is [0, o] ∪ [o+22, 30], widths o and
8 − o) and **10 gap rows**. In a band row at most one 5-wide piece fits
(5 + 5 > 8); a 2-wide chipolata (2 × 5) may accompany it (5 + 2 = 7 ≤ 8,
o ∈ {5,6}), and a 3-wide piece (shrimp 3 × 6, paprika 3 × 6) may (5 + 3 = 8,
o = 5). Pieces 10 or 12 wide never fit a band row (max side 8).

*R portrait (15 × 7).* R's 7 rows must be gap rows (a band row would force
15 + 22 > 30). Seven gap rows then have 30 − 15 = 15 free cells each, the other
three gap rows 30: **gap capacity 7·15 + 3·30 = 195**. The five cases need:

| Case | Band-row capacity (5-wide pieces + small) | Needed in gap | Gap capacity |
|------|--------------------------------------------|----------------|--------------|
| 2S+R+2C+3T | 10·5 = 50 | 270 − 50 = 220 | 195 |
| … + C + 4T + K | 10·7 = 70 | 270 − 70 = 200 | 195 |
| … + C + 4T + X | 6·8 + 4·5 = 68 (X 3 × 6 spans 6 rows) | 275 − 68 = 207 | 195 |
| … + 5T + P | 6·8 + 4·5 = 68 (P 3 × 6; P 6 × 3 gives 3·6 + 7·5 = 53) | 268 − 68 = 200 | 195 |
| … + 5T + X + K | 6·8 + 4·7 = 76 (X rows then K rows) | 275 − 76 = 199 | 195 |

In every case the needed gap area exceeds the gap capacity. Impossible.

*R landscape (7 × 15).* Let J be R's y-interval (15 rows). At least
10 − 5 = 5 band rows lie in J (the complement of J is only 5 rows); each such
row has 30 − 22 − 7 = 1 free cell (unusable). The remaining ≤ 5 band rows have
8 free cells each: **band capacity ≤ 5·8 = 40** (the 3-wide/2-wide companions
above only tighten this). The 10 gap rows each have 30 − 7 = 23 free cells
(split by R into two sides of widths x and 23 − x): **gap capacity 10·23 =
230**.

- 2S+R+2C+3T: needed gap ≥ 270 − 25 = 245 (band holds at most 5·5 = 25) > 230.
- … + C + 4T + K: needed gap ≥ 270 − 35 = 235 > 230.
- … + C + 4T + X: needed gap ≥ 275 − 40 = 235 > 230.
- … + 5T + X + K: needed gap ≥ 275 − 40 = 235 > 230.
- … + 5T + P: needed gap ≥ 268 − 40 = 228 — not yet ruled out, so look at the
  rows. Write w for the number of wide steaks (10 × 5; a wide steak needs a gap
  side of width ≥ 10, which all gap rows share, since R's position is fixed).
  The gap pieces' widths per row are drawn from {10 (steak), 5 (narrow steak),
  6 or 3 (paprika)} and each gap row sums to at most 23. A row summing to 23
  **must contain the 3-wide paprika** (10a + 5b = 23 has no non-negative
  solution); the single paprika, as 3 × 6, contributes to at most 6 rows.
  (As 6 × 3 no row reaches 23 at all.) So the total gap area is at most
  6·23 + 4·20 = **218** (the remaining rows sum to at most 20: two wide steaks,
  or four 5-wide pieces). But the case needs ≥ 228. Impossible. (For w = 5 the
  wide steaks alone demand 5·50 = 250 > 230.)

**Conclusion.** Lemma 1 reduces 3-round packings to the splits (4,3,3) and
(4,4,2). In (4,3,3) every round holds 3 sausages, so the round containing the
rumpsteak contradicts Lemma 2. In (4,4,2) Lemmas 3–6 contradict every possible
composition. **No 3-round packing exists; with the greedy 4-round plan, the
optimum is 4.** ∎

## 4. Machine proof (independent algorithm)

The hand proof is a human artifact; the following is a *different algorithm*
that reaches the same conclusion by exhaustive computation, independently of
both the planner's joint search and the CP-SAT oracle. It is implemented as the
Explicit test `Menu01_ThreeRounds` in
`tests/GrillMaster.Verification/Tests/Menu01CompositionProbe.cs`:

```
dotnet run --project tests/GrillMaster.Verification -c Release -- \
    --explicit only --filter-class "GrillMaster.Verification.Tests.Menu01CompositionProbe"
```

**Algorithm.** A 3-round packing is the same as a partition of the 34 pieces
into three groups, each packable on one empty grill. So:

1. **Enumerate compositions.** Split every type's count across the three rounds,
   keeping only canonical splits (g₀ ≤ g₁ ≤ g₂ lexicographically over the
   per-type count vectors — round symmetry breaking), pruned by:
   - the per-round area window 591–600 (Lemma's window, applied exactly), and
   - the exact per-type one-round capacity
     (`GrillPlannerHelpers.SingleRoundCapacity`, the same budgeted exact search
     the lower bound is built from).

    This collapses the composition space from ~300 million raw splits to **2936
    canonical partitions**.

2. **Check groups.** For each distinct piece group (memoized by its per-type
   count vector) run a *complete* one-round packing search: every free
   position, both orientations, identical-piece slot-order symmetry breaking
   (the same completeness guarantee the planner's verification pass relies on),
   plus bounds: remaining-area ≤ free area, per-type remaining ≤ empty-grill
   capacity, and an exact **residual-capacity** check at the start of each
   type's section (a budgeted exact sub-search proving, for that type, that its
   remaining pieces cannot fit the current free space — this is what
   automatically discovers lemmas like "two 12 × 5 chickens cannot fit the
   8-wide strip left by four 22 × 5 sausages"). A group is *feasible* when a
   packing is found, *infeasible* when the search exhausts within its node
   budget, *unknown* otherwise.

3. **Conclude.** A partition is feasible iff all three groups are feasible.
   Exhausting the enumeration without an all-feasible partition proves 3 rounds
   infeasible.

**Soundness.** Every prune is a sound bound (area, exact capacities, proven
residual non-fits); the one-round search is complete over positions and
orientations; the enumeration covers every partition exactly once (the
lex-canonical representative of each round multiset); and a group's status
depends only on its multiset of pieces (same-type pieces are interchangeable).
An "infeasible" group is a genuine certificate.

**Result** (measured on 8 cores, ~24 min; the node count is exact and
machine-independent, since each group's search is serialized by a per-group gate
and the total budget does not bind):

```
partitions=2936
groups checked=627 (feasible 316, infeasible 199, unknown 112)
one-round nodes=7,441,921,295
=> 3 rounds INFEASIBLE; with the greedy 4-round plan, optimum = 4
```

Every one of the 2936 partitions contains at least one group **proven**
infeasible; the 112 budget-unknown groups are irrelevant (each hit the prover's
50M per-group node budget, and every partition one appears in has a
proven-infeasible sibling group). No 3-round packing exists. ∎

## 5. Why the planner's own search cannot see this

In the planner's joint search the pieces are placed one by one across rounds.
The lower-bound look-ahead (`RoundsLowerBound`) only improves once enough area
is committed; through the long prefix of sausage placements it stays exactly at
3 (the area floor), which equals the bound and never reaches the champion's 4,
so *nothing is pruned*. The first sausage alone has 144 free positions
(9 × 16), and the sausage subtree is on the order of 10²⁰ nodes — ten orders of
magnitude beyond a 10-billion-node budget. The composition approach sidesteps
that subtree entirely by decoupling the rounds from each other: the area window
makes each round *nearly full*, which is exactly the regime where per-group
one-round proofs are cheap and the enumeration is tiny.

## 6. The third planner phase (implemented)

The planner now certifies instances like Menu 01 itself (where the champion
stands exactly one round above the lower bound and the area slack for the
lower-bound rounds is small — here 3·600 − 1791 = 9) instead of returning
`IsProvenOptimal = false`.

**The engine** is `RoundCompositionProver` in
`src/GrillMaster.Application/Features/Plans/RoundCompositionProver.cs`: the §4
algorithm, generalized to any grill and round count.
`Prove(pieces, grill, rounds, nodeBudget, parallelism)` returns a
`CompositionProofResult` (the verdict, the witness, and the work counters).
It first enumerates the canonical round compositions *serially up front* —
the partition list is complete before any worker starts, which is what keeps
the verdict sound under parallelism — and then checks the groups across
`parallelism` workers. Each group is searched exactly once (a per-group gate
serializes concurrent checks of the same group), so the total node count is
the deterministic sum of the per-group searches and does not depend on the
core count. A group is *infeasible* only when its complete search exhausts; a
group that exceeds the 50M per-group node budget stays *unknown* and can never
turn the verdict into a false proof. The verdicts are `LbInfeasible` (no
all-feasible partition), `LbFeasible` (a full R-round witness), and `Unknown`
(the total budget ran out first).

**The wiring** is `GrillPlanner.TryCompositionProof`, called at every point a
search phase ends (serial and parallel, both phase 1 and the verification
pass). It runs only when the search ran out of budget, the champion stands
exactly one above the lower bound, the lower bound is at most 5, and the area
slack for the lower-bound rounds is at most 30 cm². The phase has its own
budget, the `CompositionProofNodes` setting (0 disables it — the default in
the test suites; the console is configured with 8,000,000,000, headroom over
Menu 01's 7,441,921,295). An `LbInfeasible` verdict marks the champion plan
proven optimal; an `LbFeasible` witness replaces the champion with a
lower-bound packing and marks it proven; `Unknown` leaves the honest unproven
flag in place. The existing guarantee is preserved: `IsProvenOptimal` is true
only on an exhaustive proof.

**Consequences, accepted deliberately.**

- In the console, Menu 01 flips to `IsProvenOptimal = true` (the phase adds
  ~7.4 billion one-round nodes, ~24 min on 8 cores, to the run).
- The pinned quality snapshot (`PlannerFullFixtureQualityTests`) and the
  performance baseline are unaffected: the test settings leave
  `CompositionProofNodes` at 0, and the other 14 fixture menus sit on the
  lower bound, where the phase never runs.
- The planner/CP-SAT independence rule in AGENTS.md is preserved: the
  composition prover is its own search engine (no shared search code with the
  oracle), and the differential verification suite keeps comparing planner
  results against CP-SAT optima.

**Verification.**

- `Menu01CompositionProbe` (and the CP-SAT `Menu01OracleProbe`) remain the
  standing Explicit witnesses for this instance; the probe now runs the
  planner's own prover and pins the §4 numbers.
- The unit tests cover the engine directly (both verdicts, the witness, the
  budget cutoff) and the wiring end to end on a tight 12×8 menu where the
  joint search cannot prove optimality but the composition phase flips it.

Out of scope (for now): integrating a generalized "long strip" lower bound
into `ComputeLowerBound` — the composition prover achieves the same effect on
tight instances without touching the lower bound that every other menu relies
on.
