#!/usr/bin/env bash
# One end-to-end test on a throwaway Windows VM: fetches the Bundle CI built, clones a VM from the template, runs the
# test's payload on its desktop, brings back what it wrote, and destroys the VM whatever happened.
#
#   e2e/run.sh <test> [--run <Actions run id>] [--pext <file>] [--release] [--keep]
#
#   <test>       a folder under e2e/payloads/ with a run.ps1 (fresh-install, guide-over-game, seeded-home, see-all,
#                achievements, background-follows-selection, reduce-motion, guide-off, hub-panel, hub-queue,
#                play-history-off, pc-battery, cover-fit, probe)
#   --run        the CI run to take the Bundle from (its bundle artifact); default: the latest run on this branch that
#                built one; only runs of this repository's own code, never a fork's PR
#   --pext       a .pext to test instead of downloading one
#   --release    the Release .pext rather than the Debug one
#   --keep       leave the VM running after the test, to look at it in the Proxmox console; `e2e/proxmox/vm.sh down`
#                destroys it after
#
# Results land in artifacts/e2e/<test>-<time>/; exits 0 when the test passed.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/.." && pwd)"
vm="$here/proxmox/vm.sh"

test="${1:?usage: e2e/run.sh <test> [--run id] [--pext file] [--release] [--keep]}"; shift
run_id="" pext="" config="Debug" keep=0
while (($#)); do
    case "$1" in
        --run) run_id="$2"; shift 2 ;;
        --pext) pext="$2"; shift 2 ;;
        --release) config="Release"; shift ;;
        --keep) keep=1; shift ;;
        *) echo "unknown option $1" >&2; exit 2 ;;
    esac
done
payload_src="$here/payloads/$test"
[[ -f "$payload_src/run.ps1" ]] || { echo "no test $test (no $payload_src/run.ps1)" >&2; exit 2; }

log() { echo "[$(date +%H:%M:%S)] $*" >&2; }

stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT
cp -r "$payload_src/." "$stage/"
cp -r "$here/payloads/lib" "$stage/lib"

# The ViGEm client the tests drive their virtual pad with: managed, netstandard2.0, loaded by PowerShell 5.1 on the VM.
# Fetched once from NuGet, pinned by version and checksum, and kept under artifacts/e2e/.cache.
vigem_version="1.21.256"
vigem_sha256="4458301000b732d115521e99f9936f4edb70d6ceb3036ef158715e0e6b8902e0"
vigem="$repo/artifacts/e2e/.cache/Nefarius.ViGEm.Client-$vigem_version.dll"
if [[ ! -f "$vigem" ]] || ! echo "$vigem_sha256  $vigem" | sha256sum -c --quiet 2>/dev/null; then
    mkdir -p "$(dirname "$vigem")"
    nupkg="$(mktemp -d)"
    curl -fsSL -o "$nupkg/p.nupkg" \
        "https://api.nuget.org/v3-flatcontainer/nefarius.vigem.client/$vigem_version/nefarius.vigem.client.$vigem_version.nupkg"
    python3 -I -c 'import zipfile,sys; open(sys.argv[2],"wb").write(zipfile.ZipFile(sys.argv[1]).read("lib/netstandard2.0/Nefarius.ViGEm.Client.dll"))' \
        "$nupkg/p.nupkg" "$vigem"
    rm -rf "$nupkg"
    echo "$vigem_sha256  $vigem" | sha256sum -c --quiet || { rm -f "$vigem"; echo "ViGEm client checksum mismatch" >&2; exit 1; }
fi
cp "$vigem" "$stage/lib/Nefarius.ViGEm.Client.dll"

# The seeded library's art, real games' Xbox store art, one picture a line in lib/art.txt as
# "<cover|background> <url> <sha256> <game>": fetched once, pinned by checksum, kept under artifacts/e2e/.cache by its
# checksum, and staged as lib/art/<game>/<cover|background>.jpg, where Library.ps1 finds them.
mkdir -p "$stage/lib/art" "$repo/artifacts/e2e/.cache"
while read -r kind url sha256 game; do
    [[ -z "$kind" || "$kind" == \#* ]] && continue
    art="$repo/artifacts/e2e/.cache/xbox-art-$sha256.jpg"
    if [[ ! -f "$art" ]] || ! echo "$sha256  $art" | sha256sum -c --quiet 2>/dev/null; then
        curl -fsSL -o "$art" "$url"
        echo "$sha256  $art" | sha256sum -c --quiet || { rm -f "$art"; echo "checksum mismatch for $game's $kind ($url)" >&2; exit 1; }
    fi
    mkdir -p "$stage/lib/art/$game"
    cp "$art" "$stage/lib/art/$game/$kind.jpg"
done < "$here/payloads/lib/art.txt"
rm -f "$stage/lib/art.txt"

# The other add-ons a test installs beside the Bundle, one a line in its addons.txt as "<url> <sha256>": fetched once,
# pinned by checksum, kept under artifacts/e2e/.cache, and staged as addons/, where Initialize-Playnite finds them.
if [[ -f "$payload_src/addons.txt" ]]; then
    mkdir -p "$stage/addons" "$repo/artifacts/e2e/.cache"
    while read -r url sha256; do
        [[ -z "$url" || "$url" == \#* ]] && continue
        addon="$repo/artifacts/e2e/.cache/${url##*/}"
        if [[ ! -f "$addon" ]] || ! echo "$sha256  $addon" | sha256sum -c --quiet 2>/dev/null; then
            curl -fsSL -o "$addon" "$url"
            echo "$sha256  $addon" | sha256sum -c --quiet || { rm -f "$addon"; echo "checksum mismatch for $url" >&2; exit 1; }
        fi
        cp "$addon" "$stage/addons/"
    done < "$payload_src/addons.txt"
fi

if [[ "$test" != probe ]]; then
    if [[ -z "$pext" ]]; then
        # Only a run of this repository's own code: the workflow also runs on PRs from forks, whose branch can share a
        # name with ours, and a fork's .pext must never run on our VM.
        own="$(gh repo view --json nameWithOwner --jq .nameWithOwner)"
        branch="$(git -C "$repo" rev-parse --abbrev-ref HEAD)"
        if [[ -z "$run_id" ]]; then
            # CI uploads the bundle only when its extension job built and passed, which a PR that doesn't touch the
            # extension skips: the newest unexpired bundle of this branch, from a run of this repository's code.
            run_id="$(gh api "repos/$own/actions/artifacts?name=bundle&per_page=100" --jq "[.artifacts[] | select(.expired | not)
                | .workflow_run | select(.head_branch == \"$branch\" and .head_repository_id == .repository_id)][0].id // empty")"
            [[ -n "$run_id" ]] || { echo "no CI run of $own on $branch with a bundle to take" >&2; exit 1; }
        fi
        from="$(gh api "repos/$own/actions/runs/$run_id" --jq '.head_repository.full_name + " " + .head_branch + " " + .head_sha[0:7]')"
        [[ "${from%% *}" == "$own" ]] || { echo "run $run_id built ${from%% *}'s code, not $own's" >&2; exit 1; }
        log "taking the $config Bundle from CI run $run_id ($from)"
        gh run download "$run_id" --name bundle --dir "$stage/bundle"
        if [[ "$config" == Release ]]; then pext="$(ls "$stage"/bundle/Release/*.pext)"; else pext="$(ls "$stage"/bundle/*.pext)"; fi
        mv "$pext" "$stage/"
        rm -rf "$stage/bundle"
    else
        cp "$pext" "$stage/"
    fi
fi

out="$repo/artifacts/e2e/$test-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$out"

id="$("$vm" up | tail -1)"
cleanup() {
    if ((keep)); then
        log "kept VM $id; destroy it with: e2e/proxmox/vm.sh down $id"
    else
        "$vm" down "$id" || log "could not destroy VM $id: run e2e/proxmox/vm.sh down $id"
    fi
    rm -rf "$stage"
}
trap cleanup EXIT

set +e
"$vm" desktop "$id" "$stage" "$out" 900
code=$?
set -e

[[ -f "$out/steps.txt" ]] && cat "$out/steps.txt" >&2
if ((code == 0)); then log "PASS: $test ($out)"; else log "FAIL: $test, exit $code ($out)"; fi
exit "$code"
