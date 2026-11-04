#!/usr/bin/env bash
# One end-to-end test on a throwaway Windows VM: fetches the Bundle CI built, clones a VM from the template, runs the
# test's payload on its desktop, brings back what it wrote, and destroys the VM whatever happened.
#
#   e2e/run.sh <test> [--run <Actions run id>] [--pext <file>] [--release] [--keep]
#
#   <test>       a folder under e2e/payloads/ with a run.ps1 (fresh-install, probe)
#   --run        the Extension workflow run to take the Bundle from; default: the latest successful one on this branch
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

if [[ "$test" != probe ]]; then
    if [[ -z "$pext" ]]; then
        branch="$(git -C "$repo" rev-parse --abbrev-ref HEAD)"
        if [[ -z "$run_id" ]]; then
            run_id="$(gh run list --workflow Extension --branch "$branch" --status success --limit 1 \
                --json databaseId --jq '.[0].databaseId // empty')"
            [[ -n "$run_id" ]] || { echo "no successful Extension run on $branch to take the Bundle from" >&2; exit 1; }
        fi
        log "taking the $config Bundle from Extension run $run_id ($(gh run view "$run_id" --json headSha,headBranch --jq '.headBranch + " " + .headSha[0:7]'))"
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
