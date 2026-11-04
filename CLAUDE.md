## Agent skills

### Issue tracker

Issues live in GitHub Issues on `snrgarb/xbox-series-theme`, managed via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default canonical labels: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.

### Development lifecycle

Never commit to `main`; it only takes squash-merged PRs (enforced by a ruleset with no bypass). Start every change in a worktree on a `feat/`, `fix/`, `chore/`, `docs/` or `release/` branch from `origin/main` under `.claude/worktrees/`, and land it with a PR whose title and description read as the commit message, in Conventional Commits form (`docs/commits.md`). Releases, milestones and component labels: `docs/development.md`.
