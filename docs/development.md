# Development lifecycle

How work gets from an issue to a release. The rules on GitHub enforce it; nobody, the owner included, bypasses them. Why: [ADR 0003](adr/0003-trunk-based-squash-prs.md).

## The shape

- **`main` is the trunk and is always releasable.** Nothing reaches it except a squash-merged PR whose checks pass.
- **Every change is a short-lived branch in its own worktree**, from `origin/main`, merged by PR, deleted after.
- **Releases are cut from `main`** by a manual workflow run, when a milestone is done.
- **Hotfixes are ordinary `fix/` PRs to `main`**, followed by a patch release. There are no long-lived release branches; so don't merge half-done work: finish it on its branch, or leave it out of the build until it's done.

## Issues

- Every piece of work starts as an issue (`docs/agents/issue-tracker.md`). Triage labels: `docs/agents/triage-labels.md`.
- **Type**: `bug`, `enhancement` or `documentation`. **Component**: `component:theme`, `component:extension`, `component:bootshell` or `component:build`, one or more.
- **Milestones** are component versions at Major.Minor: `Bundle 0.2` (the theme and the extension, which share Major.Minor) and `Boot shell 1.1`. An issue goes into the milestone it should ship in; one with no milestone isn't planned yet. A release ships whichever milestones are done; patch releases need no milestone. Specs and maps (`Spec: …`, `wayfinder:map`) stay open until their children are closed.

## Branches and worktrees

| Prefix | For | Example |
|---|---|---|
| `feat/` | an enhancement | `feat/34-achievements-page` |
| `fix/` | a bug, hotfixes included | `fix/97-startup-mediaopened` |
| `chore/` | build, CI, tooling, cleanup | `chore/dev-lifecycle` |
| `docs/` | docs only | `docs/readme-limits` |
| `release/` | the PR that bumps versions for a release, deleted at merge like any other | `release/bundle-0.2.0` |

Put the issue number in the branch name when there is one. Worktrees live under `.claude/worktrees/` (ignored by git), so the main checkout stays on `main` and only ever fast-forwards:

```sh
git fetch origin
git worktree add -b feat/34-achievements-page .claude/worktrees/34-achievements-page origin/main
cd .claude/worktrees/34-achievements-page
# ...work, commit as often as you like...
git push -u origin HEAD
gh pr create                   # its title and description become the commit on main; "Closes #34"
gh pr merge --squash --auto    # merges itself once the checks pass; the branch is deleted on merge
cd ../../.. && git worktree remove .claude/worktrees/34-achievements-page && git pull --ff-only
```

## Pull requests

- **Squash only.** The PR's title becomes the commit's header on `main` and its description the body, so write them as the commit message, following [the commit conventions](commits.md): `feat(extension): …`, `fix(bootshell): …`. Commits on the branch can be as rough as you like.
- `Closes #n` in the description closes the issue at merge.
- **Required to merge**: every review thread resolved, a `PR title` in [the commit format](commits.md), and the checks of each workflow, `<Component>: changes` and `<Component>: build and test` / `pack and verify`. Each workflow runs on every PR; a check whose component the PR doesn't touch is skipped, which counts as passed.
- No approval is required (there's one maintainer); review your own diff in the PR before merging.

## Releases

Today the theme and the extension share one version, the Bundle version in `Directory.Build.props`, and each component is released on its own:

| Component | Version in | Tag |
|---|---|---|
| Dashboard theme | `Directory.Build.props` | `theme-v<x.y.z>` |
| Extension | `Directory.Build.props` | `extension-v<x.y.z>` |
| Boot shell | `bootshell/XboxSeries.BootShell/XboxSeries.BootShell.csproj` | `bootshell-v<x.y.z>` |

Bump a component's version only when its folder has changed since its last release, except that the theme and the extension always share Major.Minor ([commits.md](commits.md#version-effect)). To release, once the milestone's issues are closed:

1. A `release/…` PR, `chore(release): …`, bumps the versions of what changed, each by the biggest [version effect](commits.md#version-effect) among its commits since its last release, and updates the README's download table; merge it.
2. Run the deploy workflows on `main`, the theme before the extension (the extension refuses a theme it doesn't match): `gh workflow run theme.yml --ref main`, then `extension.yml`, then `bootshell.yml` if it changed.
3. Close the milestone.

Coming next: one `release.yml` builds everything into a single GitHub Release tagged by date (`v2026.11.0`), re-attaching the unchanged components' files, and refuses a component that changed without a version bump, or was bumped without a change. The theme and the extension get versions of their own that share Major.Minor.

**A botched release**: a released tag can't be moved or deleted, by anyone. To take one back, set the `release tags` ruleset to Disabled (Settings > Rules > Rulesets), `gh release delete <tag> --cleanup-tag`, and set it back to Active straight after. Prefer releasing the next patch.

## The rules, as code

`.github/rulesets/*.json` hold the rulesets on GitHub: `main` (PR required, squash only, linear history, no force push or deletion, the checks above) and `release tags` (a released tag, component or dated, can't be moved or deleted; it can still be created, as the deploy jobs do). `build/github-settings.sh` applies them and the merge settings, and points this clone at `.githooks/`, whose pre-commit refuses a commit on `main`. Change a ruleset by PR, then run the script again. Run it once in each new clone for the hook; the ruleset refuses the push anyway, the hook only stops the commit sooner.
