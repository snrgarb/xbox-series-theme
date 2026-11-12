#!/usr/bin/env bash
# Tests for build/changelog.sh, each in a throwaway git repository with real commits and tags. Run by CI's Versions job;
# run it by hand with `build/changelog.test.sh`. Exits 1 on a failure.
set -uo pipefail

script="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/changelog.sh"
failures=0 passes=0
out="" status=0
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT

repo() {
    dir="$(mktemp -d "$scratch/repo.XXXXXX")"
    cd "$dir" || exit 2
    git init -q -b main
    git config user.email test@example.com
    git config user.name test
    git config tag.gpgSign false
    git config commit.gpgSign false
    commit "docs: the first"
}

commit() { echo "$RANDOM$RANDOM" >> file; git add -A && git commit -q -m "$1"; }
run() { out="$("$script" "$@" 2>&1)"; status=$?; }

pass() { passes=$((passes + 1)); }
fail() { failures=$((failures + 1)); echo "FAIL: $current: $*"; echo "$out" | sed 's/^/    | /'; }
expect_status() { [[ $status == "$1" ]] && pass || fail "exited $status, expected $1"; }
expect_text() { grep -qF -- "$1" <<< "$out" && pass || fail "no '$1'"; }
expect_no_text() { grep -qF -- "$1" <<< "$out" && fail "unexpected '$1'" || pass; }
expect_output() { [[ $out == "$1" ]] && pass || fail "expected exactly:"$'\n'"$1"; }

test() { current="$1"; }

test "the commits since the base, oldest first, under their sections"
repo
git tag v2026.11.0
commit "fix(extension): keep the page where it opened (#3)"
commit "feat(extension): add a Background delay setting (#4)"
commit "test(e2e): start on a seeded library (#5)"
commit "perf(theme): decode covers once (#6)"
commit "feat!: drop the old settings file (#7)"
run v2026.11.0
expect_status 0
expect_output "## Changes since v2026.11.0

### Features

- feat(extension): add a Background delay setting (#4)
- feat!: drop the old settings file (#7)

### Fixes

- fix(extension): keep the page where it opened (#3)
- perf(theme): decode covers once (#6)

### Other

- test(e2e): start on a seeded library (#5)"

test "what the base already had is left out, and so are release PRs"
repo
commit "feat(theme): an old feature (#1)"
git tag v2026.11.0
commit "chore(release): theme 0.2.0, extension 0.2.0 (#2)"
commit "fix(bootshell): start once (#3)"
run v2026.11.0
expect_status 0
expect_no_text "an old feature"
expect_no_text "chore(release)"
expect_no_text "### Features"
expect_text "- fix(bootshell): start once (#3)"

test "a pre-release's tag between the base and HEAD doesn't cut the list short"
repo
git tag v2026.11.0
commit "fix(extension): one (#2)"
git tag v2026.11.1
commit "fix(extension): two (#3)"
run v2026.11.0
expect_text "- fix(extension): one (#2)"
expect_text "- fix(extension): two (#3)"

test "a type is the whole word: a header that only starts like one is Other"
repo
git tag v2026.11.0
commit "fixtures for the planner"
run v2026.11.0
expect_no_text "### Fixes"
expect_text "### Other"

test "nothing since the base says so"
repo
git tag v2026.11.0
run v2026.11.0
expect_status 0
expect_output "## Changes since v2026.11.0

None."

test "without a base, every commit"
repo
commit "feat(theme): the Dashboard (#1)"
run
expect_status 0
expect_text "## Changes"
expect_no_text "since"
expect_text "- docs: the first"
expect_text "- feat(theme): the Dashboard (#1)"

test "a base that isn't a commit is refused"
repo
run v9999.1.0
expect_status 1
expect_text "No commit v9999.1.0"

test "--fixes lists only the fixes, as plain descriptions"
repo
git tag v2026.11.0
commit "fix(theme,extension): set Segoe UI 26 body text (#3)"
commit "feat(extension): add a Background delay setting (#4)"
commit "chore(release): theme 0.4.2, extension 0.4.2 (#5)"
commit "perf!: decode covers once (#6)"
commit "docs: add the Discord invite (#7)"
run --fixes v2026.11.0
expect_status 0
expect_output "- Set Segoe UI 26 body text (#3)
- Decode covers once (#6)"

test "--fixes with no fixes since the base prints nothing"
repo
git tag v2026.11.0
commit "feat(theme): a feature (#2)"
run --fixes v2026.11.0
expect_status 0
expect_output ""

test "--fixes without a base is refused"
repo
run --fixes
expect_status 1
expect_text "--fixes needs a base"

echo "$passes passed, $failures failed"
((failures == 0))
