#!/usr/bin/env bash
# The three components' versions, and what a release does with each. Run from anywhere in the repository; reads the
# working tree and the git tags, changes nothing. Tested by build/components.test.sh.
#
#   build/components.sh versions   prints theme=, extension= and bootshell= <x.y.z>; fails unless the theme and the
#                                  extension share Major.Minor (CI's Versions job)
#   build/components.sh plan       what release.yml releases from HEAD, as lines for $GITHUB_OUTPUT: the dated tag
#                                  (tag=v<year>.<month>.<n>, the month UTC, or $RELEASE_MONTH as 2026.11), the title,
#                                  and for each component <c>_version, <c>_asset and <c>_action: build, with <c>_tag
#                                  the tag to create, or reattach, with <c>_from the release to take its file from
#
# Each component's version is in its own project; its last release is its highest <component>-v<x.y.z> tag. Changed
# since that tag, a component must have a higher version, and is built; unchanged, it must have the same version, and
# its file is re-attached from the release that carried it. A failure prints every problem to stderr and exits 1.
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

components=(theme extension bootshell)
declare -A file=(
    [theme]=theme/XboxSeries.Dashboard/XboxSeries.Dashboard.proj
    [extension]=extension/XboxSeries.Extension/XboxSeries.Extension.csproj
    [bootshell]=bootshell/XboxSeries.BootShell/XboxSeries.BootShell.csproj
)

problems=()
problem() { problems+=("$*"); }
finish() {
    ((${#problems[@]} == 0)) && return 0
    printf '%s\n' "${problems[@]}" >&2
    exit 1
}

# The one <Version>x.y.z</Version> in a component's project.
declare -A version=()
read_versions() {
    local c found
    for c in "${components[@]}"; do
        found="$(sed -n 's:^[[:space:]]*<Version>\([^<]*\)</Version>[[:space:]]*$:\1:p' "${file[$c]}" 2>/dev/null || true)"
        if [[ $found =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
            version[$c]="$found"
        else
            problem "${file[$c]} has no <Version>x.y.z</Version>"
        fi
    done
    finish
}

major_minor() { echo "${1%.*}"; }

check_shared_major_minor() {
    if [[ "$(major_minor "${version[theme]}")" != "$(major_minor "${version[extension]}")" ]]; then
        problem "The theme (${version[theme]}) and the extension (${version[extension]}) must share Major.Minor"
    fi
}

# What a component ships from: a change here since its last tag is a change to the component. The extension packs the
# theme, so a theme change is an extension change too (docs/commits.md). Tests aren't shipped, so they don't count.
declare -A paths=(
    [theme]="theme/XboxSeries.Dashboard shared/Images"
    [extension]="extension/XboxSeries.Extension extension/XboxSeries.Core theme/XboxSeries.Dashboard shared build/Bundle.targets Directory.Build.props"
    [bootshell]="bootshell/XboxSeries.BootShell shared/BootShellEvents.cs Directory.Build.props"
)
declare -A name=([theme]="the theme" [extension]="the extension" [bootshell]="the Boot shell")

# The component's highest <component>-v<x.y.z> tag, or nothing before its first release.
last_tag() { git tag -l "$1-v*" --sort=-v:refname | grep -E "^$1-v[0-9]+\.[0-9]+\.[0-9]+$" | head -n1 || true; }

# Whether the component's paths changed since the tag. An MSBuild file counts only if it differs once its comments,
# its <Version> line and blank lines are left out: a bump alone isn't a change, nor is a reworded comment.
changed_since() { # component tag
    local f
    while read -r f; do
        [[ -n $f ]] || continue
        if [[ $f =~ \.(proj|csproj|props|targets)$ ]]; then
            [[ "$(msbuild_content "$2" "$f")" != "$(msbuild_content HEAD "$f")" ]] && return 0
        else
            return 0
        fi
    done < <(git diff --name-only "$2" HEAD -- ${paths[$1]})
    return 1
}

msbuild_content() { # revision file; nothing for a file the revision hasn't
    git show "$1:$2" 2>/dev/null | perl -0pe 's/<!--.*?-->//gs; s/^[ \t]*<Version>[^<]*<\/Version>[ \t]*$//mg; s/^[ \t]*\n//mg' || true
}

newer() { [[ $1 != "$2" && "$(printf '%s\n%s\n' "$1" "$2" | sort -V | tail -n1)" == "$1" ]]; }

declare -A label=([theme]="Theme" [extension]="Extension" [bootshell]="Boot shell")

asset() { # component version
    case "$1" in
        theme) echo "XboxSeries_Dashboard_$2.pthm" ;;
        extension) echo "XboxSeries_$2.pext" ;;
        bootshell) echo "XboxSeries.BootShell.exe" ;;
    esac
}

# The release that carries the file a component tag was built into: the dated release cut at the tag's commit, which
# built it, or, for a component released on its own before the dated releases, the release of that tag.
release_of() { # component tag
    local dated
    dated="$(git tag -l 'v[0-9]*' --points-at "$2^{commit}" --sort=v:refname | head -n1)"
    echo "${dated:-$2}"
}

# The next dated tag: v<year>.<month>.<n>, n counting this month's releases from 0.
next_dated_tag() {
    local month="${RELEASE_MONTH:-$(date -u +%Y.%m)}" last
    last="$(git tag -l "v$month.*" | sed -n "s/^v${month//./\\.}\\.\\([0-9]\\+\\)$/\\1/p" | sort -n | tail -n1)"
    echo "v$month.$((${last:--1} + 1))"
}

cmd_plan() {
    read_versions
    check_shared_major_minor
    local c tag last
    declare -A action=() from=()
    for c in "${components[@]}"; do
        tag="$(last_tag "$c")"
        last="${tag#"$c"-v}"
        if [[ -z $tag ]] || changed_since "$c" "$tag"; then
            action[$c]=build
            if [[ -n $tag && ${version[$c]} == "$last" ]]; then
                problem "Bump ${name[$c]}: it changed since $tag, and its version is still $last"
            elif [[ -n $tag ]] && ! newer "${version[$c]}" "$last"; then
                problem "Bump ${name[$c]}: it changed since $tag, and its version, ${version[$c]}, isn't higher than $last"
            fi
        elif [[ ${version[$c]} != "$last" ]]; then
            action[$c]=build
            # The theme and the extension share Major.Minor, so a new one in either takes the other to it at .0,
            # changed or not (docs/commits.md): that bump, and only that one, needs no change.
            local partner="" follow=""
            [[ $c == theme ]] && partner=extension
            [[ $c == extension ]] && partner=theme
            if [[ -n $partner && "$(major_minor "${version[$partner]}")" != "$(major_minor "$last")" ]]; then
                follow="$(major_minor "${version[$partner]}").0"
            fi
            if [[ -z $follow || ${version[$c]} != "$follow" ]]; then
                local hint=""
                [[ -n $follow ]] && hint=", or to $follow to follow ${name[$partner]}'s Major.Minor"
                problem "${name[$c]^} was bumped to ${version[$c]} with nothing changed since $tag: set it back to $last$hint"
            fi
        else
            action[$c]=reattach
            from[$c]="$(release_of "$c" "$tag")"
        fi
    done
    if [[ ${action[theme]} == reattach && ${action[extension]} == reattach && ${action[bootshell]} == reattach ]]; then
        problem "Nothing changed since the last release: nothing to release"
    fi
    finish

    local title=()
    for c in "${components[@]}"; do title+=("${label[$c]} ${version[$c]}"); done
    echo "tag=$(next_dated_tag)"
    echo "title=$(printf '%s · ' "${title[@]}" | sed 's/ · $//')"
    for c in "${components[@]}"; do
        echo "${c}_version=${version[$c]}"
        echo "${c}_action=${action[$c]}"
        echo "${c}_asset=$(asset "$c" "${version[$c]}")"
        if [[ ${action[$c]} == build ]]; then
            echo "${c}_tag=$c-v${version[$c]}"
        else
            echo "${c}_from=${from[$c]}"
        fi
    done
}

cmd_versions() {
    read_versions
    check_shared_major_minor
    finish
    local c
    for c in "${components[@]}"; do echo "$c=${version[$c]}"; done
}

case "${1:-}" in
    versions) cmd_versions ;;
    plan) cmd_plan ;;
    *) echo "usage: build/components.sh versions|plan" >&2; exit 2 ;;
esac
