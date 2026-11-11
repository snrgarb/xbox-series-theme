# The Game hub's Queue action (#208), on the seeded library. The VM has no Steam to download with, so a Debug build
# fakes one download when Playnite starts with XBOXSERIES_FAKE_DOWNLOAD set to a game's name: that game, not installed,
# reads as one Steam is installing, and joins the Queue, Queued. A Release build ignores the variable. What the log says:
#   [Queue] download faked by XBOXSERIES_FAKE_DOWNLOAD=<name>: ...  a Debug build taking the variable
#   [Hub] Queue action shown|hidden for <game>                      as a hub shows, and as the Queue changes under it
#   [Hub] focus on Queue for <game>                                 Queue took focus
#   Manage Queue opened from the Game hub for <game>                A on Queue
#   My games & apps shows ManageQueue | My games & apps closed      the page
#   [Hub] back from Manage Queue, focus on Queue for <game>         B on Manage Queue, back in the hub
#
# 1. A game not in the Queue (Alan Wake 2, the Recent row's first): its hub logs "Queue action hidden".
# 2. Dead Cells, queued by the variable: its hub logs "Queue action shown" (hub-queue.jpg: Queued, Manage, then the
#    download glyph). Right to Queue, which logs its focus and names itself above (hub-queue-focus.jpg).
# 3. A on Queue: Manage Queue opens with Dead Cells in it (manage-queue.jpg).
# 4. B: the hub opens again on Dead Cells, focus on Queue (hub-back.jpg), not on Manage.
# 5. B again: the hub closes, back to Home (home-after.jpg).
#
# No press is counted blind: each move waits for the extension's log, by a stopwatch and by the log's line count,
# never by its times: the VM's clock can jump mid-run.
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()
$queued = 'Dead Cells'
$plain = 'Alan Wake 2'
$told = 'Home focus on|My games & apps (shows|closed)|Game hub opened|Manage Queue opened|\[Hub\]|\[Queue\]'

function Get-Mark { (Get-ExtensionLog).Count }
function Get-LogLines([int] $After, [string] $Pattern) {
    @(Get-ExtensionLog | Select-Object -Skip $After | Where-Object { $_ -match $Pattern } | ForEach-Object { $_.Trim() })
}

# Waits up to $Seconds, by a stopwatch, for a line matching $Pattern after the mark $After; the first such line, or $null.
function Wait-Line([string] $Pattern, [double] $Seconds, [int] $After) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    do {
        $hit = Get-LogLines $After $Pattern | Select-Object -First 1
        if ($hit) { return $hit }
        Start-Sleep -Milliseconds 200
    } while ($watch.Elapsed.TotalSeconds -lt $Seconds)
    $null
}

function Fail([string] $What) { $script:failures += $What; Say "CHECK FAILED: $What" }
function Write-Told([int] $After) { Get-LogLines $After $told | ForEach-Object { Say "  log: $_" } }

# Home's focus by the log's last word on it, without its time; $null with none yet.
function Get-Focus {
    $last = Get-LogLines 0 'Home focus on ' | Select-Object -Last 1
    if ($last) { $last -replace '^.*(Home focus on )', '$1' }
}

# Presses $Button a step at a time until Home's focus is on what $Pattern tells, at most $Max times. Whether it got there.
function Step-To([string] $Button, [string] $Pattern, [int] $Max) {
    foreach ($n in 0..$Max) {
        if ((Get-Focus) -match $Pattern) { return $true }
        if ($n -eq $Max) { break }
        $mark = Get-Mark
        Push-Button $Button | Out-Null
        Wait-Line 'Home focus on ' 2 $mark | Out-Null
        Start-Sleep -Milliseconds 300
    }
    Say "focus never reached /$Pattern/ with $Max $Button; it is on: $(Get-Focus)" | Out-Null
    $false
}

# A on the focused Recent tile, after the double-tap window: the hub must open on $Game. Whether it did.
function Open-Hub([string] $Game) {
    $mark = Get-Mark
    Push-Button A | Out-Null
    if (-not (Wait-Line "Game hub opened for $([regex]::Escape($Game))" 8 $mark)) { Write-Told $mark | Out-Null; return $false }
    Start-Sleep -Seconds 2
    $true
}

try {
    $env:XBOXSERIES_FAKE_DOWNLOAD = $queued
    Connect-Pad
    Start-Dashboard $PSScriptRoot -Library
    Start-Sleep -Seconds 6  # the "Hi <user>!" toast over Home's foot has gone
    $start = Get-Mark
    $faked = Wait-Line '\[Queue\] download faked by XBOXSERIES_FAKE_DOWNLOAD' 10 0
    if ($faked) { Say "logged: $faked" } else { throw 'No "[Queue] download faked" line: a Release build, which ignores XBOXSERIES_FAKE_DOWNLOAD?' }

    if (-not (Step-To Down 'Home focus on the Recent tile ' 2)) { Save-Screen 'no-recent-row'; throw 'Never reached the Recent row (see no-recent-row.jpg)' }
    Start-Sleep -Milliseconds 800

    # 1. A game not in the Queue: no Queue on its hub.
    if (-not (Step-To Left "Home focus on the Recent tile $plain$" 8)) { Fail "Never reached $plain on the Recent row" }
    else {
        $mark = Get-Mark
        if (-not (Open-Hub $plain)) { Fail "A on $plain did not open its hub" }
        else {
            Save-Screen 'hub-plain'
            if (-not (Get-LogLines $mark "\[Hub\] Queue action hidden for $plain$")) { Fail "$plain's hub did not log 'Queue action hidden'" }
            if (Get-LogLines $mark "\[Hub\] Queue action shown for $plain$") { Fail "$plain's hub shows Queue, though it isn't in the Queue" }
            $back = Get-Mark
            Push-Button B
            Wait-Line 'Home focus on ' 4 $back | Out-Null
            Start-Sleep -Milliseconds 800
        }
    }

    # 2. The queued game: Queue shows, and Right takes focus to it.
    if (-not (Step-To Right "Home focus on the Recent tile $queued$" 8)) { Save-Screen 'no-queued-tile'; throw "Never reached $queued on the Recent row (see no-queued-tile.jpg)" }
    $mark = Get-Mark
    if (-not (Open-Hub $queued)) { Save-Screen 'hub-not-opened'; throw "A on $queued did not open its hub" }
    Save-Screen 'hub-queue'
    if (-not (Wait-Line "\[Hub\] Queue action shown for $queued$" 3 $mark)) { Fail "$queued's hub did not log 'Queue action shown'" }
    $focused = $null
    foreach ($n in 1..3) {
        $step = Get-Mark
        Push-Button Right
        $focused = Wait-Line "\[Hub\] focus on Queue for $queued$" 1.5 $step
        if ($focused) { break }
    }
    Start-Sleep -Milliseconds 600
    Save-Screen 'hub-queue-focus'
    if (-not $focused) { Write-Told $mark; throw "Right never took focus to Queue on $queued's hub (see hub-queue-focus.jpg)" }

    # 3. A on Queue: Manage Queue.
    $mark = Get-Mark
    Push-Button A
    if (-not (Wait-Line "Manage Queue opened from the Game hub for $queued$" 4 $mark)) { Fail 'A on Queue did not log "Manage Queue opened from the Game hub"' }
    if (-not (Wait-Line 'My games & apps shows ManageQueue' 5 $mark)) { Write-Told $mark; Fail 'A on Queue did not show Manage Queue' }
    Start-Sleep -Seconds 2
    Save-Screen 'manage-queue'

    # 4. B: back to the hub, focus on Queue.
    $mark = Get-Mark
    Push-Button B
    $back = Wait-Line '\[Hub\] back from Manage Queue, focus on ' 6 $mark
    $afterBack = Get-Mark
    Start-Sleep -Seconds 2
    Save-Screen 'hub-back'
    Write-Told $mark
    if (-not $back) { Fail 'B on Manage Queue did not log "[Hub] back from Manage Queue"' }
    elseif ($back -notmatch "focus on Queue for $queued$") { Fail "B on Manage Queue went back to the hub but not to Queue: $back" }
    if (-not (Get-LogLines $mark "Game hub opened for $queued$")) { Fail "B on Manage Queue did not open $queued's hub again" }
    if (Get-LogLines $mark 'My games & apps shows Manage$') { Fail 'B on Manage Queue went to Manage' }
    # Nothing under the hub took focus from Queue after it.
    $later = Get-LogLines $afterBack 'Home focus on |My games & apps nav focus on '
    if ($later) { Fail "focus moved on after the hub came back: $($later -join ' / ')" }

    # 5. B in the hub: back to Home.
    $mark = Get-Mark
    Push-Button B
    if (-not (Wait-Line '\[Hub\] backdrop snapshot freed|Home focus on ' 5 $mark)) { Write-Told $mark; Fail 'B in the hub did not close it' }
    Start-Sleep -Seconds 1
    Save-Screen 'home-after'

    Write-Told $start
    if (-not (Get-Process Playnite.FullscreenApp -ErrorAction SilentlyContinue)) { Fail 'Playnite exited' }
    $errors = @(([regex]'(?m)^.*\|ERROR\|.*$').Matches((Get-PlayniteLog)) | ForEach-Object Value)
    $errors | ForEach-Object { Say "logged: $_" }
    if ($errors.Count) { Fail "$($errors.Count) errors logged" }

    if ($failures.Count -eq 0) { $result = 'PASS' }
} catch {
    $failures += "error: $_`n$($_.ScriptStackTrace)"
    try { Save-Screen 'error' } catch {}
} finally {
    $failures | ForEach-Object { Say "failed: $_" }
    Say "result: $result"
    Save-PlayniteFiles
    Stop-Playnite
    Disconnect-Pad
    Remove-Item Env:XBOXSERIES_FAKE_DOWNLOAD -ErrorAction SilentlyContinue
}
exit $(if ($result -eq 'PASS') { 0 } else { 1 })
