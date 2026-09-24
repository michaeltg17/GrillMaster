## Conventions
- Keep the domain value types (`Centimeters`, `SquareCentimeters`, ...) out of the planner hot
  loops: their operators are not inlined, so the packing engines work on raw ints (flattened
  grids, cached widths/areas). That is what the quality snapshot and performance benchmark below
  protect.
- Packing quality over the full 15-menu fixture is pinned by `PlannerFullFixtureQualityTests` in
  the unit tests (deterministic, always run, machine-independent). Update the snapshot when a
  change deliberately alters packing quality.
- The exact planner is verified against an independent OR-Tools CP-SAT oracle in
  `tests/GrillMaster.Verification` (deterministic, always run): small random + edge
  corpora compare planner round counts against CP-SAT optima. The heavy
  `LargeAttackCorpus_MatchCpSatOptimum` test (1000 adversarial cases, a few minutes)
  is explicit: `dotnet run --project tests/GrillMaster.Verification -- --explicit only`.
  Run the full suite after a change that touches the planner's search or proof logic.
  Keep the two engines independent: no shared search code, different encodings.

## Workflow
Commit on `dev` → push `dev` → open (or update) the `dev` → `main` PR.

When creating or updating the `dev` → `main` PR:
1. Run `git fetch origin main` first
2. Compare `origin/main..dev` to identify only the actual new changes.
3. Check if a PR already exists (use `github_list_pull_requests`).
4. If none exists, create one with title and description summarizing the changes.
5. If one exists, update its title and description to reflect the actual current diff.