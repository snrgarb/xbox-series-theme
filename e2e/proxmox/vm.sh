#!/usr/bin/env bash
# A throwaway Windows 11 test VM on the Proxmox host: linked-cloned from the e2e template, put on the DMZ bridge, booted
# until Windows has signed its test user in, and destroyed after. Driven through the Proxmox API with the ci@pve token.
#
#   vm.sh up              clone, boot, wait for the signed-in user; prints the new VM's id last
#   vm.sh info <vmid>     its state, IP and signed-in users
#   vm.sh exec <vmid> <command...>   run a command in the VM as SYSTEM (guest agent), print its output
#   vm.sh down <vmid>     stop and destroy it; refuses anything not made by `up`
#   vm.sh desktop <vmid> <payload-dir> <out-dir> [timeout-s]
#                         run <payload-dir>/run.ps1 on the signed-in user's desktop, as that user; its files written to
#                         $env:E2E_OUT come back into <out-dir>; exits with run.ps1's exit code
#   vm.sh list            the test VMs that exist now
#
# Settings come from ~/.config/xbox-e2e/proxmox.env (kept out of the repo; it holds the token):
#   PVE_HOST=https://<host>:8006  PVE_NODE=<node>  PVE_TOKEN=ci@pve!e2e=<secret>  PVE_TEMPLATE=<template vmid>
# and optionally PVE_BRIDGE (default vmbr2, the DMZ) and PVE_VMID_FROM (default 9000, where test VM ids start).
set -euo pipefail

ENV_FILE="${XBOX_E2E_ENV:-$HOME/.config/xbox-e2e/proxmox.env}"
[[ -r "$ENV_FILE" ]] || { echo "No settings at $ENV_FILE" >&2; exit 2; }
set -a; . "$ENV_FILE"; set +a
: "${PVE_HOST:?}" "${PVE_NODE:?}" "${PVE_TOKEN:?}" "${PVE_TEMPLATE:?}"
PVE_BRIDGE="${PVE_BRIDGE:-vmbr2}"
PVE_VMID_FROM="${PVE_VMID_FROM:-9000}"
TAG="xbox-e2e"

log() { echo "[$(date +%H:%M:%S)] $*" >&2; }
die() { log "error: $*"; exit 1; }

# api METHOD PATH [key=value...] -> the response's "data" as JSON on stdout; fails on any HTTP error.
api() {
    local method="$1" path="$2"; shift 2
    local args=(-sS -k -m 60 -X "$method" -H "Authorization: PVEAPIToken=$PVE_TOKEN" -w '\n%{http_code}')
    # GET's parameters go in the query string, the rest in a form body.
    [[ "$method" == GET ]] && args+=(-G)
    local kv; for kv in "$@"; do args+=(--data-urlencode "$kv"); done
    local out code
    out="$(curl "${args[@]}" "$PVE_HOST/api2/json$path")" || return 1
    code="${out##*$'\n'}"; out="${out%$'\n'*}"
    if [[ "$code" != 2* ]]; then
        echo "$method $path -> HTTP $code: $out" >&2
        return 1
    fi
    python3 -c 'import sys,json; print(json.dumps(json.load(sys.stdin).get("data")))' <<<"$out"
}

# py EXPR < json -> evaluates EXPR with the JSON as d, prints the result (strings bare).
py() { python3 -c "import sys,json; d=json.load(sys.stdin); r=($1); print(r if isinstance(r,str) else json.dumps(r))"; }

wait_task() {
    local upid="$1" what="$2" status
    for _ in $(seq 1 600); do
        status="$(api GET "/nodes/$PVE_NODE/tasks/$(python3 -c 'import sys,urllib.parse; print(urllib.parse.quote(sys.argv[1], safe=""))' "$upid")/status")"
        if [[ "$(py 'd["status"]' <<<"$status")" == stopped ]]; then
            local exit; exit="$(py 'd.get("exitstatus","")' <<<"$status")"
            [[ "$exit" == OK ]] || die "$what failed: $exit"
            return 0
        fi
        sleep 2
    done
    die "$what did not finish"
}

vm() { echo "/nodes/$PVE_NODE/qemu/$1"; }

# Only a VM `up` made: tagged, not a template, not the template's id.
assert_ours() {
    local id="$1" config
    [[ "$id" =~ ^[0-9]+$ ]] || die "not a VM id: $id"
    [[ "$id" != "$PVE_TEMPLATE" ]] || die "$id is the template"
    config="$(api GET "$(vm "$id")/config")" || die "no VM $id"
    [[ "$(py 'str(d.get("template",0))' <<<"$config")" == 0 ]] || die "$id is a template"
    [[ ";$(py 'd.get("tags","")' <<<"$config");" == *";$TAG;"* ]] || die "$id is not a test VM (no $TAG tag)"
}

free_vmid() {
    local id
    for ((id = PVE_VMID_FROM; id < PVE_VMID_FROM + 100; id++)); do
        api GET "/cluster/nextid" "vmid=$id" >/dev/null 2>&1 && { echo "$id"; return; }
    done
    die "no free VM id from $PVE_VMID_FROM"
}

agent_ok() { api POST "$(vm "$1")/agent/ping" >/dev/null 2>&1; }

ipv4() {
    api GET "$(vm "$1")/agent/network-get-interfaces" 2>/dev/null | py '
next((a["ip-address"] for i in d["result"] if not i["name"].lower().startswith("loopback")
      for a in i.get("ip-addresses",[]) if a["ip-address-type"]=="ipv4" and not a["ip-address"].startswith("169.254.")), "")'
}

users() { api GET "$(vm "$1")/agent/get-users" 2>/dev/null | py '",".join(u["user"] for u in d["result"])'; }

cmd_up() {
    local id name started
    id="$(free_vmid)"
    name="xbox-e2e-$(date +%Y%m%d-%H%M%S)"
    log "cloning template $PVE_TEMPLATE to $id ($name), linked"
    wait_task "$(api POST "$(vm "$PVE_TEMPLATE")/clone" "newid=$id" "name=$name" "full=0" | py 'd')" "clone"
    # From here on a failure leaves a VM behind: destroy it rather than leak it.
    trap 'log "up failed, destroying $id"; cmd_down "$id" || true' ERR
    log "moving its network to $PVE_BRIDGE (new MAC) and tagging it $TAG"
    api PUT "$(vm "$id")/config" "net0=virtio,bridge=$PVE_BRIDGE,firewall=1" "tags=$TAG" "onboot=0" \
        "description=Throwaway test VM made by e2e/proxmox/vm.sh; destroyed after its run." >/dev/null
    log "starting"
    wait_task "$(api POST "$(vm "$id")/status/start" | py 'd')" "start"
    started=$SECONDS
    log "waiting for the guest agent"
    until agent_ok "$id"; do
        (( SECONDS - started < 600 )) || die "no guest agent after 10 minutes"
        sleep 5
    done
    log "agent up after $((SECONDS - started))s; waiting for Autologon"
    until [[ -n "$(users "$id")" ]]; do
        (( SECONDS - started < 600 )) || die "nobody signed in after 10 minutes"
        sleep 5
    done
    log "signed in: $(users "$id") after $((SECONDS - started))s; waiting for an IP"
    until [[ -n "$(ipv4 "$id")" ]]; do
        (( SECONDS - started < 600 )) || die "no IPv4 after 10 minutes"
        sleep 5
    done
    log "ready: VM $id at $(ipv4 "$id")"
    trap - ERR
    echo "$id"
}

cmd_info() {
    local id="$1"
    assert_ours "$id"
    echo "vmid:    $id"
    echo "status:  $(api GET "$(vm "$id")/status/current" | py 'd["status"]')"
    if agent_ok "$id"; then
        echo "ip:      $(ipv4 "$id")"
        echo "users:   $(users "$id")"
        echo "os:      $(api GET "$(vm "$id")/agent/get-osinfo" | py 'd["result"].get("pretty-name","")')"
    else
        echo "agent:   not answering"
    fi
}

cmd_exec() {
    local id="$1"; shift
    assert_ours "$id"
    local args=() a pid st
    for a in "$@"; do args+=("command=$a"); done
    pid="$(api POST "$(vm "$id")/agent/exec" "${args[@]}" | py 'd["pid"]')"
    for _ in $(seq 1 600); do
        st="$(api GET "$(vm "$id")/agent/exec-status" "pid=$pid")"
        if [[ "$(py 'str(d.get("exited",0))' <<<"$st")" == 1 ]]; then
            py 'd.get("out-data","")' <<<"$st"
            py 'd.get("err-data","")' <<<"$st" >&2
            return "$(py 'str(d.get("exitcode",0))' <<<"$st")"
        fi
        sleep 1
    done
    die "exec did not finish"
}

cmd_down() {
    local id="$1"
    assert_ours "$id"
    if [[ "$(api GET "$(vm "$id")/status/current" | py 'd["status"]')" != stopped ]]; then
        log "stopping $id"
        wait_task "$(api POST "$(vm "$id")/status/stop" | py 'd')" "stop"
    fi
    log "destroying $id"
    wait_task "$(api DELETE "$(vm "$id")?purge=1&destroy-unreferenced-disks=1" | py 'd')" "destroy"
    log "gone: $id"
}

# The payload goes in, and the run's output comes back, over exchange.py on this machine; the guest agent, which runs
# as SYSTEM in session 0 with no desktop, only sets up a scheduled task that runs it in the signed-in user's session.
cmd_desktop() {
    local id="$1" payload="$2" out="$3" timeout="${4:-1800}"
    assert_ours "$id"
    [[ -f "$payload/run.ps1" ]] || die "no run.ps1 in $payload"
    local ip user bind port token work
    ip="$(ipv4 "$id")"; [[ -n "$ip" ]] || die "VM $id has no IP"
    user="$(users "$id" | cut -d, -f1)"; [[ -n "$user" ]] || die "nobody signed in on VM $id"
    bind="$(ip -4 route get "$ip" | sed -n 's/.* src \([0-9.]*\).*/\1/p')"
    port=$((20000 + RANDOM % 20000))
    token="$(python3 -c 'import secrets; print(secrets.token_hex(16))')"
    work="$(mktemp -d)"
    python3 -c 'import shutil,sys; shutil.make_archive(sys.argv[1], "zip", root_dir=sys.argv[2])' "$work/payload" "$payload"

    local here; here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    python3 "$here/exchange.py" --bind "$bind" --port "$port" --token "$token" \
        --payload "$work/payload.zip" --out "$work/out.zip" --timeout "$timeout" &
    local exchange=$!
    trap 'kill '"$exchange"' 2>/dev/null || true; rm -rf "'"$work"'"' RETURN

    local base="http://$bind:$port/$token"
    # Run in the user's session: fetch the payload, run it, send back its output whatever happens.
    local boot
    boot=$(cat <<EOF
\$ErrorActionPreference = 'Stop'; \$ProgressPreference = 'SilentlyContinue'
\$root = 'C:\\e2e'; \$out = "\$root\\out"; \$base = '$base'
New-Item -ItemType Directory \$out -Force | Out-Null
\$code = 255
Start-Transcript "\$out\\boot.log" | Out-Null
try {
    Invoke-WebRequest -UseBasicParsing "\$base/payload.zip" -OutFile "\$root\\payload.zip"
    Expand-Archive "\$root\\payload.zip" "\$root\\payload" -Force
    \$env:E2E_OUT = \$out
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "\$root\\payload\\run.ps1"
    \$code = \$LASTEXITCODE
} catch {
    \$_ | Out-String | Set-Content "\$out\\boot-error.txt"
} finally {
    Stop-Transcript | Out-Null
}
Set-Content "\$out\\exitcode.txt" \$code
Compress-Archive "\$out\\*" "\$root\\out.zip" -Force
Invoke-WebRequest -UseBasicParsing -Method Put -InFile "\$root\\out.zip" -ContentType application/zip "\$base/out.zip" | Out-Null
EOF
)
    local setup
    setup=$(cat <<EOF
\$ErrorActionPreference = 'Stop'; \$ProgressPreference = 'SilentlyContinue'
Remove-Item C:\\e2e -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory C:\\e2e | Out-Null
Set-Content C:\\e2e\\boot.ps1 -Encoding UTF8 -Value @'
$boot
'@
# Headless: no console window, so nothing of the harness takes the foreground from what it tests.
\$action = New-ScheduledTaskAction -Execute conhost.exe -Argument '--headless powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\\e2e\\boot.ps1'
\$principal = New-ScheduledTaskPrincipal -UserId '$user' -LogonType Interactive -RunLevel Highest
\$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Seconds $timeout) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName xbox-e2e -Action \$action -Principal \$principal -Settings \$settings -Force | Out-Null
Start-ScheduledTask -TaskName xbox-e2e
EOF
)
    local encoded
    encoded="$(printf '%s' "$setup" | iconv -t UTF-16LE | base64 -w0)"
    log "starting the payload on $user's desktop (VM $id, $ip), output via $bind:$port"
    cmd_exec "$id" powershell.exe -NoProfile -NonInteractive -EncodedCommand "$encoded" >/dev/null
    log "waiting up to ${timeout}s for its output"
    wait "$exchange" || die "no output from VM $id within ${timeout}s"
    mkdir -p "$out"
    python3 -c 'import zipfile,sys; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])' "$work/out.zip" "$out"
    local code; code="$(tr -dc '0-9' < "$out/exitcode.txt")"
    log "run.ps1 exited $code; output in $out"
    return "${code:-255}"
}

cmd_list() {
    api GET "/cluster/resources" "type=vm" | TAG="$TAG" py '"\n".join("%s %s %s" % (v["vmid"], v.get("name",""), v["status"])
        for v in sorted(d, key=lambda v: v["vmid"]) if __import__("os").environ["TAG"] in (v.get("tags") or "").split(";"))'
}

case "${1:-}" in
    up) cmd_up ;;
    info) cmd_info "${2:?vmid}" ;;
    exec) shift; id="${1:?vmid}"; shift; cmd_exec "$id" "$@" ;;
    down) cmd_down "${2:?vmid}" ;;
    desktop) cmd_desktop "${2:?vmid}" "${3:?payload dir}" "${4:?out dir}" "${5:-1800}" ;;
    list) cmd_list ;;
    *) sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//'; exit 2 ;;
esac
