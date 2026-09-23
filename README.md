# Grill Master

A small console app that tells you **how many grill rounds** you need to cook every piece
of meat on a list of menus — without burning anything.

This is the solution for the *isolutions "Assessment Grill Master"* task.

## The problem, in plain words

You have a grill — a flat **30 cm × 20 cm** patch — and a list of menus that a cooking API
sends you. Each menu is a shopping list of meat: "2 rumpsteaks (15 cm × 7 cm)",
"4 sausages (6 cm × 3 cm)", and so on.

Everything takes the **same time** to cook. So the only question is: **how many rounds
(batches) do you need?** In one round, anything that fits on the grill at the same time — with
no pieces overlapping — cooks together. Pieces may be turned sideways (a 15×7 steak may lie
7×15), but they must stay on the grill, on whole centimetres.

That is the whole problem. It is the same kind of puzzle as fitting boxes into a lorry: obvious
for one or two boxes, genuinely hard in general — which is why the app ships several different
strategies (we call them *planners*) and lets you compare them.

## How to run it

Requires the **.NET 10 SDK**.

```bash
# build everything
dotnet build GrillMaster.slnx

# run it (downloads the menus, then prints one line per menu plus a total)
dotnet run --project src/GrillMaster.Console
```

The output looks like:

```
Menu 04: 2 rounds
Menu 11: 1 rounds
Menu 03: 3 rounds
...
Total: 37 rounds
```

## How it thinks (one paragraph)

The app arranges the meat with a *planner* — a strategy for deciding what goes on the grill in
each round. There are **eight** of them: a fast "biggest first, tuck it in" cook; a patient one
that tries arrangements until it can *prove* no better answer exists; a specialist
integer-programming engine; and a team captain that runs the good ones and keeps the best
plate. You pick one in a single line of a configuration file. The plain-language explanations,
the results table, and the configuration details all live in
[`docs/planners.md`](docs/planners.md).

## Testing

The app comes with four test suites — unit, integration, end-to-end, and performance:

```bash
dotnet run --project tests/GrillMaster.UnitTests
dotnet run --project tests/GrillMaster.IntegrationTests
dotnet run --project tests/GrillMaster.EndToEndTests
dotnet run --project tests/GrillMaster.PerformanceTests
```

The unit, integration, and performance suites run against a local stand-in for the API (no
network needed); the end-to-end suite launches the built console app against the live API.

Packing quality is pinned by a snapshot test in the unit suite (the planners are deterministic, so
it is machine-independent). Speed is measured, not gated, because wall-clock numbers are only
comparable on the same machine:

```bash
dotnet run --project tests/GrillMaster.PerformanceTests -- --explicit only
```

That run benchmarks the planners, prints the delta against the previous results in
[`tests/GrillMaster.PerformanceTests/results.json`](tests/GrillMaster.PerformanceTests/results.json),
and rewrites the file — run it before and after a performance-relevant change and commit the
updated file so the diff shows up in the PR.

## What's in the repo

- `src/` — the app itself, in three small parts: the plain data (menus, pieces, rounds), the
  planning logic (the eight planners) with its configuration, and the console program that ties
  them together.
- `tests/` — the four test suites above, each carrying the API stand-in / menu fixtures it needs.
- `docs/` — the plain-language planner explanations, starting from
  [`docs/planners.md`](docs/planners.md).

## Things it does not do

- Pieces may be turned 90°, but not at an arbitrary angle.
- The "prove it's the best" planners have a time budget; on this data they finish almost
  instantly, but on a huge or hostile menu they may stop and return the best plan found so
  far, clearly marked as *not proven*.
- Every piece is assumed to fit the grill (the largest piece in this data is 22 cm; the grill
  is 30 cm wide). A piece that cannot fit in either orientation is an error, not a guess.
