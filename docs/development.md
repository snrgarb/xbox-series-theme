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
- **Required to merge**: every review thread resolved, a `PR title` in [the commit format](commits.md), and `CI`. The CI workflow (`.github/workflows/ci.yml`) runs on every PR and push to `main`: it builds and tests only the components the PR touches, checks that the theme and the extension share Major.Minor, and its `CI` job passes when every other job passed or was skipped. Its extension job uploads the `bundle` artifact that `e2e/run.sh` installs.
- No approval is required (there's one maintainer); review your own diff in the PR before merging.

## Releases

Each component has a version of its own, and one GitHub Release, tagged by date, carries all three:

| Component | Version in | File | Tag when built |
|---|---|---|---|
| Dashboard theme | `theme/XboxSeries.Dashboard/XboxSeries.Dashboard.proj` | `XboxSeries_Dashboard_<x.y.z>.pthm` | `theme-v<x.y.z>` |
| Extension | `extension/XboxSeries.Extension/XboxSeries.Extension.csproj` | `XboxSeries_<x.y.z>.pext`, with the theme inside | `extension-v<x.y.z>` |
| Boot shell | `bootshell/XboxSeries.BootShell/XboxSeries.BootShell.csproj` | `XboxSeries.BootShell.exe` | `bootshell-v<x.y.z>` |

The release is `v<year>.<month>.<n>`, `n` counting that month's releases from 0 (`v2026.11.0`, then `v2026.11.1`), titled with its tag, "v2026.11.0 (early access)"; its notes list each component's version and file. Each is published as the Latest release, which the README's [latest release](https://github.com/snrgarb/xbox-series-theme/releases/latest) link and the repository's sidebar follow; until the Bundle's first stable release, its title ends in "(early access)". It isn't marked a pre-release, as GitHub never makes a pre-release Latest.

**Bump a component only when it changed** since its last `<component>-v<x.y.z>` tag, by the biggest [version effect](commits.md#version-effect) among its commits. What counts as a change is what the component ships from, not its tests (`build/components.sh` lists the paths): the extension packs the theme, so a theme change is an extension change too, and `shared/` and `Directory.Build.props` count for each component that builds from them. In an MSBuild file, the `<Version>` line and comments don't count. **The theme and the extension share Major.Minor**: CI fails when they don't, and when one changes to a new Major.Minor, it takes the other to it at `.0`, changed or not; two unchanged ones can't take each other.

To release, once the milestone's issues are closed:

1. A `release/…` PR, `chore(release): …`, bumps the versions of what changed and points every row of the README's download table at the release to come, `https://github.com/snrgarb/xbox-series-theme/releases/download/v<year>.<month>.<n>/<file>`, unchanged components included; merge it.
2. `gh workflow run release.yml --ref main`. Its Plan job ([`build/components.sh plan`](../build/components.sh)) refuses, listing every problem, when:
   - a component changed without a higher version ("Bump the theme: …"), or was bumped with nothing changed ("The Boot shell was bumped to 1.0.1 with nothing changed …");
   - nothing changed at all, or the theme and the extension don't share Major.Minor;
   - the README doesn't link this release's files.

   Otherwise its Release job builds what changed (tests first), checks the `.pext` holds the release's own `.pthm`, scans each built file on VirusTotal (`build/VirusTotal.ps1`), and re-attaches each unchanged component's file byte for byte from the release that built it. It creates the release as a draft, pushes the dated tag and each built component's tag at once, then publishes it, with a table of each file's version, origin, and VirusTotal report.
3. Close the milestone.

`.github/workflows/downloads.yml` builds all three files from any branch, as an Actions artifact, for a try between releases; it releases and tags nothing.

**A botched release**: a released tag can't be moved or deleted, by anyone. To take one back, set the `release tags` ruleset to Disabled (Settings > Rules > Rulesets), `gh release delete <tag> --cleanup-tag`, and set it back to Active straight after. Prefer releasing the next patch.

## The rules, as code

`.github/rulesets/*.json` hold the rulesets on GitHub: `main` (PR required, squash only, linear history, no force push or deletion, the checks above) and `release tags` (a released tag, component or dated, can't be moved or deleted; it can still be created, as `release.yml` does). `build/github-settings.sh` applies them and the merge settings, and points this clone at `.githooks/`, whose pre-commit refuses a commit on `main`. Change a ruleset by PR, then run the script again. Run it once in each new clone for the hook; the ruleset refuses the push anyway, the hook only stops the commit sooner.
