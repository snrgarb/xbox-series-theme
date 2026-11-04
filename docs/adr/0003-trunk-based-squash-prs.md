# Trunk-based development: every change is a squash-merged PR to a protected main

Until the first releases (2026-11-04), most work was committed straight to `main`, with a few PRs merged by merge commit. With releases out, `main` has to stay releasable, and the only maintainer wants to be stopped from pushing to it by accident.

- **One trunk.** Short-lived `feat/`, `fix/`, `chore/`, `docs/` and `release/` branches, each in its own worktree, from `origin/main`. No `develop` branch and no long-lived release branches.
- **Every change is a PR**, merged by squash only, with linear history. The PR title and description become the commit on `main`. No approvals are required, as there is one maintainer, but review threads must be resolved and CI must pass.
- **Checks needn't be up to date with `main`.** Requiring every branch to be rebased onto the latest `main` before merging costs a CI run per merge; two PRs that pass alone and clash together are rare with one maintainer, and the push to `main` runs the checks again.
- **No bypass.** The `main` ruleset has no bypass actors, so the repository's admin can't push past it either; only editing the ruleset itself (which is versioned in `.github/rulesets/`) changes that.
- **Milestone-driven releases.** A milestone per upcoming component version; release from `main` when it's done, by one manual workflow, `release.yml`. Patch releases at any time. Each component keeps its own version, bumped only when what it ships from changed (the theme and the extension share Major.Minor, and a new Major.Minor in one takes the other along), and one GitHub Release tagged by date (`v2026.11.0`) carries all three: the changed ones built and tagged `<component>-v<x.y.z>`, the unchanged ones re-attached byte for byte from the release that built them. The release refuses a change without a bump and a bump without a change.
- **One required check.** One CI workflow runs on every PR and push to `main`, builds only the components a PR touches, and reports one `CI` check that passes when its other jobs passed or were skipped; the ruleset requires that and the PR title, so adding or renaming a job never touches the ruleset.
- **Hotfixes go to `main`**, then out as a patch release. This holds only while unfinished work stays off `main`.

## Considered options

- **Merge commits**, as before: keep every branch commit, but `main`'s history is noisy, and a revert takes a merge commit's parent number.
- **GitFlow** (`develop` plus release and hotfix branches): isolates hotfixes, but doubles the merges for one maintainer and needs the deploy jobs to accept other branches.
- **Maintenance branches for hotfixes only** (`maint/<component>-x.y` from a tag): kept in reserve if a hotfix ever has to ship while `main` holds unreleasable work.
- **Path filters on the workflows' PR trigger**, as before: a required check that never starts blocks the PR forever, so CI runs on every PR and skips the jobs it doesn't need instead.
- **A workflow per component, each with its own release**, as at first: three required checks per component, and three releases to run in order (the theme before the extension that packs it), each with its own tag and page.
- **One version for the theme and the extension**: simpler, but a fix to one releases both, unchanged files under a new version.
