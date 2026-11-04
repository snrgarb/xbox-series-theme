#!/usr/bin/env bash
# Tests for build/components.sh, each in a throwaway git repository with the three components' version files, real
# commits and real tags. Run by CI's Versions job; run it by hand with `build/components.test.sh`. Exits 1 on a failure.
set -uo pipefail

script="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/components.sh"
failures=0 passes=0
out="" status=0
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT

# A repository holding the theme, the extension and the Boot shell at the given versions, committed.
repo() { # theme extension bootshell
    dir="$(mktemp -d "$scratch/repo.XXXXXX")"
    cd "$dir" || exit 2
    git init -q -b main
    git config user.email test@example.com
    git config user.name test
    git config tag.gpgSign false
    git config commit.gpgSign false
    mkdir -p theme/XboxSeries.Dashboard extension/XboxSeries.Extension extension/XboxSeries.Core \
        extension/XboxSeries.Core.Tests bootshell/XboxSeries.BootShell bootshell/XboxSeries.BootShell.Tests \
        shared/Images build docs
    project theme/XboxSeries.Dashboard/XboxSeries.Dashboard.proj "$1"
    project extension/XboxSeries.Extension/XboxSeries.Extension.csproj "$2"
    project bootshell/XboxSeries.BootShell/XboxSeries.BootShell.csproj "$3"
    for f in theme/XboxSeries.Dashboard/Home.xaml extension/XboxSeries.Extension/Guide.cs extension/XboxSeries.Core/Core.cs \
        extension/XboxSeries.Core.Tests/CoreTests.cs bootshell/XboxSeries.BootShell/Program.cs \
        bootshell/XboxSeries.BootShell.Tests/ProgramTests.cs shared/Images/source-steam-shadow.png \
        shared/BootShellEvents.cs build/Bundle.targets docs/notes.md README.md Directory.Build.props; do
        echo one > "$f"
    done
    commit "the first"
}

project() { # file version
    printf '<Project>\n  <!-- A comment. -->\n  <PropertyGroup>\n    <Version>%s</Version>\n  </PropertyGroup>\n</Project>\n' "$2" > "$1"
}

commit() { git add -A && git commit -q -m "$1"; }
change() { echo "$RANDOM$RANDOM" >> "$1"; commit "change $1"; }
bump() { # component version
    case "$1" in
        theme) project theme/XboxSeries.Dashboard/XboxSeries.Dashboard.proj "$2" ;;
        extension) project extension/XboxSeries.Extension/XboxSeries.Extension.csproj "$2" ;;
        bootshell) project bootshell/XboxSeries.BootShell/XboxSeries.BootShell.csproj "$2" ;;
    esac
    commit "bump $1 to $2"
}

# Tags HEAD as a release would: the components' tags given, and a dated tag when one is given.
released() { for tag in "$@"; do git tag "$tag"; done; }

run() { out="$(RELEASE_MONTH="${RELEASE_MONTH:-2026.11}" "$script" "$@" 2>&1)"; status=$?; }

pass() { passes=$((passes + 1)); }
fail() { failures=$((failures + 1)); echo "FAIL: $current: $*"; echo "$out" | sed 's/^/    | /'; }
expect_status() { [[ $status == "$1" ]] && pass || fail "exited $status, expected $1"; }
expect_line() { grep -qxF -- "$1" <<< "$out" && pass || fail "no line '$1'"; }
expect_text() { grep -qF -- "$1" <<< "$out" && pass || fail "no '$1'"; }
expect_no_text() { grep -qF -- "$1" <<< "$out" && fail "unexpected '$1'" || pass; }

test() { current="$1"; }

test "versions: the theme and the extension at one Major.Minor pass"
repo 0.2.0 0.2.3 1.0.0
run versions
expect_status 0
expect_line "theme=0.2.0"
expect_line "extension=0.2.3"
expect_line "bootshell=1.0.0"

test "versions: the theme and the extension at different Major.Minor fail"
repo 0.2.0 0.3.0 1.0.0
run versions
expect_status 1
expect_text "The theme (0.2.0) and the extension (0.3.0) must share Major.Minor"

test "versions: a version that isn't x.y.z fails"
repo 0.2 0.2.0 1.0.0
run versions
expect_status 1
expect_text "theme/XboxSeries.Dashboard/XboxSeries.Dashboard.proj has no <Version>x.y.z</Version>"

test "plan: a changed component without a bump is refused"
repo 0.1.0 0.1.0 1.0.0
released theme-v0.1.0 extension-v0.1.0 bootshell-v1.0.0
change bootshell/XboxSeries.BootShell/Program.cs
run plan
expect_status 1
expect_text "Bump the Boot shell: it changed since bootshell-v1.0.0, and its version is still 1.0.0"
expect_no_text "theme"
expect_no_text "extension"

test "plan: a bumped change is built, the rest re-attached from their own releases before the first dated one"
repo 0.1.0 0.1.0 1.0.0
released theme-v0.1.0 extension-v0.1.0 bootshell-v1.0.0
change bootshell/XboxSeries.BootShell/Program.cs
bump bootshell 1.0.1
run plan
expect_status 0
expect_line "tag=v2026.11.0"
expect_line "title=Theme 0.1.0 · Extension 0.1.0 · Boot shell 1.0.1"
expect_line "theme_version=0.1.0"
expect_line "theme_action=reattach"
expect_line "theme_from=theme-v0.1.0"
expect_line "theme_asset=XboxSeries_Dashboard_0.1.0.pthm"
expect_line "extension_action=reattach"
expect_line "extension_from=extension-v0.1.0"
expect_line "extension_asset=XboxSeries_0.1.0.pext"
expect_line "bootshell_version=1.0.1"
expect_line "bootshell_action=build"
expect_line "bootshell_tag=bootshell-v1.0.1"
expect_line "bootshell_asset=XboxSeries.BootShell.exe"

test "plan: a bump with nothing changed is refused"
repo 0.1.0 0.1.0 1.0.0
released theme-v0.1.0 extension-v0.1.0 bootshell-v1.0.0
change extension/XboxSeries.Extension/Guide.cs
bump extension 0.1.1
bump bootshell 1.0.1
change docs/notes.md
change bootshell/XboxSeries.BootShell.Tests/ProgramTests.cs
run plan
expect_status 1
expect_text "The Boot shell was bumped to 1.0.1 with nothing changed since bootshell-v1.0.0: set it back to 1.0.0"
expect_no_text "extension"

test "plan: an unchanged theme taken to the extension's new Major.Minor at .0 is built"
repo 0.1.0 0.1.0 1.0.0
released theme-v0.1.0 extension-v0.1.0 bootshell-v1.0.0
change extension/XboxSeries.Extension/Guide.cs
bump extension 0.2.0
bump theme 0.2.0
run plan
expect_status 0
expect_line "theme_action=build"
expect_line "theme_tag=theme-v0.2.0"
expect_line "extension_action=build"
expect_line "bootshell_action=reattach"

test "plan: an unchanged theme bumped past its partner's Major.Minor .0 is refused"
repo 0.1.0 0.1.0 1.0.0
released theme-v0.1.0 extension-v0.1.0 bootshell-v1.0.0
change extension/XboxSeries.Extension/Guide.cs
bump extension 0.2.1
bump theme 0.2.1
run plan
expect_status 1
expect_text "The theme was bumped to 0.2.1 with nothing changed since theme-v0.1.0: set it back to 0.1.0, or to 0.2.0 to follow the extension's Major.Minor"

test "plan: an unchanged theme and extension can't follow each other to a new Major.Minor"
repo 0.1.0 0.1.0 1.0.0
released theme-v0.1.0 extension-v0.1.0 bootshell-v1.0.0
bump theme 0.2.0
bump extension 0.2.0
run plan
expect_status 1
expect_text "The theme was bumped to 0.2.0 with nothing changed since theme-v0.1.0: set it back to 0.1.0"
expect_text "The extension was bumped to 0.2.0 with nothing changed since extension-v0.1.0: set it back to 0.1.0"

test "plan: an unchanged theme given a patch bump is refused"
repo 0.1.0 0.1.0 1.0.0
released theme-v0.1.0 extension-v0.1.0 bootshell-v1.0.0
change extension/XboxSeries.Extension/Guide.cs
bump extension 0.1.1
bump theme 0.1.1
run plan
expect_status 1
expect_text "The theme was bumped to 0.1.1 with nothing changed since theme-v0.1.0: set it back to 0.1.0"

test "plan: a changed component whose version went down is refused"
repo 0.1.0 0.1.0 1.2.0
released theme-v0.1.0 extension-v0.1.0 bootshell-v1.2.0
change bootshell/XboxSeries.BootShell/Program.cs
bump bootshell 1.1.9
run plan
expect_status 1
expect_text "Bump the Boot shell: it changed since bootshell-v1.2.0, and its version, 1.1.9, isn't higher than 1.2.0"

test "plan: an unchanged component is re-attached from the dated release that built it, the month counted on"
repo 0.1.0 0.1.0 1.0.0
released theme-v0.1.0 extension-v0.1.0 bootshell-v1.0.0
change extension/XboxSeries.Extension/Guide.cs
bump extension 0.1.1
released extension-v0.1.1 v2026.11.0
change bootshell/XboxSeries.BootShell/Program.cs
bump bootshell 1.0.1
released bootshell-v1.0.1 v2026.11.1
change bootshell/XboxSeries.BootShell/Program.cs
bump bootshell 1.0.2
run plan
expect_status 0
expect_line "tag=v2026.11.2"
expect_line "theme_from=theme-v0.1.0"
expect_line "extension_action=reattach"
expect_line "extension_from=v2026.11.0"
expect_line "extension_asset=XboxSeries_0.1.1.pext"
expect_line "bootshell_action=build"
RELEASE_MONTH=2026.12 run plan
expect_line "tag=v2026.12.0"

test "plan: nothing changed since the last release is refused"
repo 0.1.0 0.1.0 1.0.0
released theme-v0.1.0 extension-v0.1.0 bootshell-v1.0.0
change docs/notes.md
change extension/XboxSeries.Core.Tests/CoreTests.cs
run plan
expect_status 1
expect_text "Nothing changed since the last release: nothing to release"

test "plan: a reworded one-line comment in an MSBuild file isn't a change"
repo 0.1.0 0.1.0 1.0.0
released theme-v0.1.0 extension-v0.1.0 bootshell-v1.0.0
printf '<Project>\n  <!-- Reworded. -->\n  <PropertyGroup>\n    <Version>1.0.0</Version>\n  </PropertyGroup>\n</Project>\n' \
    > bootshell/XboxSeries.BootShell/XboxSeries.BootShell.csproj
commit "reword"
run plan
expect_status 1
expect_text "Nothing changed since the last release"

test "plan: a reworded multi-line comment in an MSBuild file isn't a change"
repo 0.1.0 0.1.0 1.0.0
printf '<Project>\n  <!--\n    Packs it.\n  -->\n  <Target Name="Pack" />\n</Project>\n' > build/Bundle.targets
commit "targets"
released theme-v0.1.0 extension-v0.1.0 bootshell-v1.0.0
printf '<Project>\n  <!--\n    Packs it,\n    reworded.\n  -->\n  <Target Name="Pack" />\n</Project>\n' > build/Bundle.targets
commit "reword"
run plan
expect_status 1
expect_text "Nothing changed since the last release"
printf '<Project>\n  <!-- Packs it. -->\n  <Target Name="Pack" DependsOnTargets="Build" />\n</Project>\n' > build/Bundle.targets
commit "a real change"
run plan
expect_text "Bump the extension: it changed since extension-v0.1.0"

test "plan: a property changed in Directory.Build.props changes every component"
repo 0.1.0 0.1.0 1.0.0
released theme-v0.1.0 extension-v0.1.0 bootshell-v1.0.0
printf '<Project>\n  <PropertyGroup>\n    <LangVersion>latest</LangVersion>\n  </PropertyGroup>\n</Project>\n' > Directory.Build.props
commit "props"
run plan
expect_status 1
expect_text "Bump the extension: it changed since extension-v0.1.0"
expect_text "Bump the Boot shell: it changed since bootshell-v1.0.0"
expect_text "Bump the theme: it changed since theme-v0.1.0"

test "plan: a theme change is an extension change too"
repo 0.1.0 0.1.0 1.0.0
released theme-v0.1.0 extension-v0.1.0 bootshell-v1.0.0
change theme/XboxSeries.Dashboard/Home.xaml
bump theme 0.1.1
run plan
expect_status 1
expect_text "Bump the extension: it changed since extension-v0.1.0, and its version is still 0.1.0"
expect_no_text "Bump the theme"

test "plan: the theme and the extension at different Major.Minor are refused"
repo 0.1.0 0.1.0 1.0.0
released theme-v0.1.0 extension-v0.1.0 bootshell-v1.0.0
change extension/XboxSeries.Extension/Guide.cs
bump extension 0.2.0
run plan
expect_status 1
expect_text "The theme (0.1.0) and the extension (0.2.0) must share Major.Minor"

echo "$passes passed, $failures failed"
((failures == 0))
