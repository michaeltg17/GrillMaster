## Workflow
Commit on `dev` → push `dev` → open (or update) the `dev` → `main` PR.

When creating or updating the `dev` → `main` PR:
1. Run `git fetch origin main` first
2. Compare `origin/main..dev` to identify only the actual new changes.
3. Check if a PR already exists (use `github_list_pull_requests`).
4. If none exists, create one with title and description summarizing the changes.
5. If one exists, update its title and description to reflect the actual current diff.