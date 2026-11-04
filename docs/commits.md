# Commit conventions

Commits on `main` follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/), with the types, scopes and version rules below; what this repo adds to the spec is listed under [Beyond the spec](#beyond-the-spec). Every PR is squash-merged ([development.md](development.md)), so **the PR's title is the commit's header and its description the body**: these rules apply to PR titles and descriptions. The `PR title` check (`.github/workflows/pr-title.yml`) refuses to merge a title that doesn't fit the header format; editing the title runs it again. Capitals, mood and the body are left to you. Commits on a branch can be as rough as you like; they don't reach `main`.

## Format

```
<type>(<scope>)!: <description>

<body>

<footers>
```

- **type**: from the table below. Required.
- **scope**: the component, from the list below, in parentheses. Two at most, comma-separated: `feat(theme,extension): …`. Leave it out when the change has no component, as for repo-wide docs.
- **!**: before the colon when the change breaks something (see [Breaking changes](#breaking-changes)).
- **description**: what the change does, in the imperative ("add", "fix", "stop"), as if finishing "This commit will…". Lower-case first word unless it's a name or a glossary term (`Guide`, `Dashboard`, `Boot shell`: [CONTEXT.md](../CONTEXT.md)'s vocabulary, capitalised as there). No full stop. The whole header fits in 72 characters; GitHub adds ` (#n)` after it.
- **body**: optional, after a blank line. Why the change was needed and what it does that the header can't say: the behaviour before and after, the cases it covers, what it deliberately leaves alone. Plain paragraphs or bullets, wrapped at 120 columns as the code is.
- **footers**: after a blank line, `Token: value` or `Token #value` (`Closes #12`); a value can run over several lines, up to the next footer.

## Types

| Type | For | Version effect |
|---|---|---|
| `feat` | something new a user can see or use | minor |
| `fix` | a bug fixed, as the user sees it | patch |
| `perf` | the same behaviour, faster or lighter | patch |
| `refactor` | code restructured, no behaviour change | patch if it changes a shipped file, else none |
| `test` | tests only, unit or e2e | none |
| `build` | MSBuild, packing, `build/`, dependencies | patch if it changes a shipped file, else none |
| `ci` | `.github/workflows/`, rulesets, the e2e runner | none |
| `docs` | docs only: README, `docs/`, CONTEXT.md, comments | none |
| `chore` | anything else that ships nothing, and release PRs | none |
| `revert` | undoing an earlier commit | none if that commit isn't released yet; else as a change of its own (reverting a released `feat` removes a feature: breaking) |
| any, with `!` | a [breaking change](#breaking-changes) | major (minor before 1.0.0) |

When a change is two types, it's the one with the bigger version effect: a `feat` with its tests and docs is a `feat`. When it's really two changes, it's two PRs.

## Scopes

| Scope | Covers |
|---|---|
| `theme` | `theme/`, the Dashboard theme |
| `extension` | `extension/`, the extension and Core |
| `bootshell` | `bootshell/`, the Boot shell |
| `shared` | `shared/` itself: the source more than one component builds from |
| `e2e` | `e2e/`, the end-to-end tests and their VM |
| `release` | release PRs and the release pipeline's own logic |

`ci` and `build` changes take a component's scope when they're about one component (`build(bootshell): …`), and none otherwise. Scopes match the issues' `component:` labels; `e2e`, `release` and unscoped `ci` or `build` changes are `component:build`.

## Version effect

Each component's version, in its own project, is bumped in the `release/` PR from the commits to what it ships from since its last release (its folder, the `shared/` files it builds from, and for the extension the theme it packs): the biggest effect wins. `release.yml` refuses a component that changed without a bump, or was bumped without a change ([development.md](development.md#releases)). Before 1.0.0 (the Bundle today) a breaking change bumps the minor rather than the major, as [SemVer](https://semver.org/#spec-item-4) allows.

- **The theme and the extension share Major.Minor**, and that wins over "only what changed": a minor or major bump in either, for a change of its own, bumps both, so a `feat(extension)` also takes the theme to the new Major.Minor at patch 0. Patches stay independent: a `fix` in one bumps only that one.
- **The extension packs the theme**: any theme change that ships is a change to the extension too, so both get at least a patch.
- `shared/` changes count for every component that builds from them.

The release PR itself is `chore(release): theme 0.2.0, extension 0.2.0` (the versions it sets, Boot shell too when it changes).

## Breaking changes

Mark one with `!` in the header **and** a `BREAKING CHANGE:` footer saying what breaks and what the user has to do (the spec takes either; here both, so the footer always explains it). The token is upper-case, as the spec requires; `BREAKING-CHANGE:` means the same. It's breaking when someone who updates has to do something, or loses something, they didn't before:

- the extension can't read settings, profiles or tokens an earlier version saved;
- a theme and an extension at the same Major.Minor no longer work together (a resource, setting or control the theme binds to renamed or removed);
- the Boot shell's command line, install record or registry values change so that an earlier install can't be undone or upgraded in place;
- a feature or setting users had is removed.

## Footers

- `Closes #n`: closes the issue when the PR merges. `Refs #n` for one it only touches.
- `BREAKING CHANGE: <what breaks, and what to do>`.
- No `Co-Authored-By` or "Generated with" lines.

## Examples

```
feat(extension): open the Guide over games that start with it open

A game that started while the Guide was up, as over its Launch screen, took the pad behind the Guide and showed as a
recent game whose A did nothing. It's now frozen as one the Guide opened over, listed as running with Quit, and
Profile & system greys Switch profile until it stops.

Closes #57
```

```
fix(bootshell): write Shell quoted so profile paths with spaces work
```

```
feat(theme,extension)!: rename the accent colour setting to Highlight

BREAKING CHANGE: the extension no longer reads AccentColour; pick the colour again on Settings > Personalisation.
```

```
ci: run every workflow on each PR so their checks can be required
```

```
revert(extension): poll Video.HasVideo for the first frame again

Refs: 1921dde
```

## Beyond the spec

Conventional Commits leaves these open; this repo fixes them:

- the type list, and version effects for `perf`, `refactor`, `build` and `revert` (the spec gives effects to `feat`, `fix` and breaking changes only);
- scopes are the components, and a header names two at most, comma-separated;
- a breaking change takes both `!` and the footer;
- the header's 72 characters, imperative mood and glossary capitals;
- the theme and the extension's shared Major.Minor, and the extension packing the theme.

## Before this convention

Earlier commits on `main`, up to and including #138, are long sentences with no type ("The extension runs only on the Dashboard, …"). They stay as they are; versions are worked out only from commits since each component's last release, all of which follow this convention.
