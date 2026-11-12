#!/usr/bin/env bash
# A release's changelog, as Markdown: the header of every commit on HEAD since a base, the last full release, oldest
# first under Features (feat), Fixes (fix, perf) and Other. main takes squash-merged PRs only, so each header is a PR's
# title with its number, which GitHub links. Release PRs (chore(release)) are left out: they change versions, nothing
# else. Run from anywhere in the repository; reads the history, changes nothing. Tested by build/changelog.test.sh.
#
#   build/changelog.sh [<base>]           <base>: the tag of the last full release; without one, every commit on HEAD
#   build/changelog.sh --fixes <base>     the fixes (fix, perf) since <base> only, the previous pre-release for #builds
#                                         on Discord: one "- <description> (#n)" line each, type and scope left out and
#                                         the first letter capitalised, oldest first; nothing when there are none
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

fixes_only=false
if [[ ${1:-} == --fixes ]]; then
    fixes_only=true
    shift
    if [[ -z ${1:-} ]]; then
        echo "--fixes needs a base to list the fixes since" >&2
        exit 1
    fi
fi
base="${1:-}"
if [[ -n $base ]] && ! git rev-parse -q --verify "$base^{commit}" > /dev/null; then
    echo "No commit $base to list the changes since" >&2
    exit 1
fi

features=() fixes=() others=()
while IFS= read -r header; do
    [[ -n $header ]] || continue
    case "$header" in
        'chore(release)'*) ;;
        feat[\(:!]*) features+=("$header") ;;
        fix[\(:!]* | perf[\(:!]*) fixes+=("$header") ;;
        *) others+=("$header") ;;
    esac
done < <(git log --reverse --format=%s ${base:+"$base..HEAD"})

if $fixes_only; then
    for header in ${fixes[@]+"${fixes[@]}"}; do
        description="$(sed -E 's/^[a-z]+(\([^)]*\))?!?: *//' <<< "$header")"
        echo "- ${description^}"
    done
    exit 0
fi

if [[ -n $base ]]; then echo "## Changes since $base"; else echo "## Changes"; fi
if ((${#features[@]} + ${#fixes[@]} + ${#others[@]} == 0)); then
    echo
    echo "None."
    exit 0
fi
section() { # title, headers...
    local title="$1"; shift
    (($#)) || return 0
    echo
    echo "### $title"
    echo
    printf -- '- %s\n' "$@"
}
section Features "${features[@]}"
section Fixes "${fixes[@]}"
section Other "${others[@]}"
