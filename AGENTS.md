## Conventions
- Keep the domain value types (`Centimeters`, `SquareCentimeters`, ...) out of the planner hot
  loops: their operators are not inlined, so the packing engines work on raw ints (flattened
  grids, cached widths/areas). That is what the quality snapshot and performance benchmark below
  protect.
- Packing quality over the full 15-menu fixture is pinned by `PlannerFullFixtureQualityTests` in
  the unit tests (deterministic, always run, machine-independent). Update the snapshot when a
  change deliberately alters packing quality.
- Wall-clock speed is measured, not gated:
  `dotnet run --project tests/GrillMaster.PerformanceTests -- --explicit only` benchmarks the
  planners,
  prints the delta against the previous results, and rewrites the committed
  `tests/GrillMaster.PerformanceTests/results.json`. Run it before and after a
  performance-relevant change and commit the updated file so the PR shows the perf diff.

## Workflow
Commit on `dev` → push `dev` → open (or update) the `dev` → `main` PR.

When creating or updating the `dev` → `main` PR:
1. Run `git fetch origin main` first
2. Compare `origin/main..dev` to identify only the actual new changes.
3. Check if a PR already exists (use `github_list_pull_requests`).
4. If none exists, create one with title and description summarizing the changes.
5. If one exists, update its title and description to reflect the actual current diff.