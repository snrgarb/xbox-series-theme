# The Game hub as a panel over the screen it was opened from (#207), on the seeded library. The hub is opened from
# Home's Recent row (home-hub.jpg), from My games & apps' Play history (mygames-hub.jpg) and from Search
# (search-hub.jpg), each beside a screenshot of the screen it was opened from (home.jpg, mygames.jpg, search.jpg).
# Then it is opened and closed twenty times from Home's Recent row, and Playnite's working set taken after the first
# cycle and after the last.
#
# Passes when each opening from Home and My games & apps logs "[Hub] backdrop snapshot taken (<w>x<h>)" and each
# closing "[Hub] backdrop snapshot freed"; when the twenty cycles log twenty of each, paired, taken then freed; when
# Playnite's working set after the twenty is within 30 MB of what it was after the first (logged either way, with the
# private bytes the extension logged); and when Playnite is still up. Search is tried, not judged: a hub that doesn't
# open from it is said in steps.txt.
#
# What the hub shows is for looking at: in home-hub.jpg Home's tiles blurred and darkened above and below the band,
# the game's art inside it; in mygames-hub.jpg and search-hub.jpg that page behind instead.
#
# No press is counted blind: each move waits for the extension's log ("Home focus on", "My games & apps shows",
# "[Hub] backdrop snapshot ..."), by a stopwatch and by the log's line count, never by its times: the VM's clock can
# jump mid-run.
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()

$taken = '\[Hub\] backdrop snapshot taken \(\d+x\d+\)'
$freed = '\[Hub\] backdrop snapshot freed'
$told = 'Home focus on|My games & apps (shows|closed)|Game hub opened|\[Hub\]'
$cycles = 20
$allowedGrowthMb = 30

# The extension's own log, by line count: a mark in it stays where it was whatever the clock does.
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

# What the log told since $After, into steps.txt.
function Write-Told([int] $After) { Get-LogLines $After $told | ForEach-Object { Say "  log: $_" } }

# Home's focus by the log's last word on it, without its time; $null with none yet.
function Get-Focus {
    $last = Get-LogLines 0 'Home focus on ' | Select-Object -Last 1
    if ($last) { $last -replace '^.*(Home focus on )', '$1' }
}
function Test-FocusOn([string] $Pattern) { [bool]((Get-Focus) -match $Pattern) }

# Presses $Button a step at a time until Home's focus is on what $Pattern tells, at most $Max times. Whether it got there.
function Step-To([string] $Button, [string] $Pattern, [int] $Max) {
    foreach ($n in 0..$Max) {
        if (Test-FocusOn $Pattern) { return $true }
        if ($n -eq $Max) { break }
        $mark = Get-Mark
        Push-Button $Button | Out-Null
        Wait-Line 'Home focus on ' 2 $mark | Out-Null
        Start-Sleep -Milliseconds 300
    }
    Say "focus never reached /$Pattern/ with $Max $Button; it is on: $(Get-Focus)" | Out-Null
    $false
}

# Playnite's working set now, in MB, after a moment for the screen to settle.
function Get-WorkingSetMb {
    Start-Sleep -Seconds 2
    $p = Get-Process Playnite.FullscreenApp -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $p) { throw 'Playnite is not running' }
    [math]::Round($p.WorkingSet64 / 1MB, 1)
}

# A, with focus on a game: the hub must open and log its snapshot. The screenshot $Shot once it has shown. Whether it opened.
function Open-Hub([string] $From, [string] $Shot) {
    $mark = Get-Mark
    Push-Button A | Out-Null
    $line = Wait-Line $taken 8 $mark
    if (-not $line) {
        Write-Told $mark | Out-Null
        if ($Shot) { Save-Screen "$Shot-not-opened" }
        return $false
    }
    Start-Sleep -Seconds 2
    if ($Shot) { Save-Screen $Shot }
    Say "$From`: $($line -replace '^.*(\[Hub\])', '$1')" | Out-Null
    $true
}

# B in the hub: it must close and let its snapshot go. Whether it did.
function Close-Hub([string] $From) {
    $mark = Get-Mark
    Push-Button B | Out-Null
    $line = Wait-Line $freed 5 $mark
    if (-not $line) { Write-Told $mark | Out-Null; return $false }
    Start-Sleep -Milliseconds 800
    $true
}

# Back on Home, focus on a Recent tile: B until Home tells of focus, then Up to the Recent row if it is elsewhere.
function Restore-Home([string] $What) {
    foreach ($n in 1..4) {
        if (Test-FocusOn 'Home focus on the Recent tile ') { return }
        $mark = Get-Mark
        Push-Button B
        if (Wait-Line 'Home focus on ' 3 $mark) { Start-Sleep -Milliseconds 800 }
    }
    if (-not (Step-To Up 'Home focus on the Recent tile ' 3)) {
        Save-Screen "$What-not-home"
        throw "${What}: could not get back to the Recent row (see $What-not-home.jpg)"
    }
}

try {
    Connect-Pad
    Start-Dashboard $PSScriptRoot -Library
    Start-Sleep -Seconds 6  # the "Hi <user>!" toast over Home's foot has gone
    $start = Get-Mark

    # Home starts with focus on the top bar: Down to the Recent row's first game.
    if (-not (Step-To Down 'Home focus on the Recent tile ' 2)) { Save-Screen 'no-recent-row'; throw 'Never reached the Recent row (see no-recent-row.jpg)' }
    Start-Sleep -Milliseconds 800

    # 1. From Home.
    Save-Screen 'home'
    if (-not (Open-Hub 'Home' 'home-hub')) { Fail 'A on a Recent tile did not open a hub with a snapshot (see home-hub-not-opened.jpg)' }
    elseif (-not (Close-Hub 'Home')) { Fail 'B in the hub opened from Home did not let its snapshot go' }
    Restore-Home 'home'

    # 2. From My games & apps: the Recent row's last tile, Play history, opens it with focus on its first game.
    if (-not (Step-To Right 'Home focus on the Play history tile$' 10)) { Fail 'Never reached the Play history tile' }
    else {
        $mark = Get-Mark
        Push-Button A
        if (-not (Wait-Line 'My games & apps shows PlayHistory' 5 $mark)) { Fail 'A on the Play history tile did not open My games & apps on Play history' }
        else {
            Start-Sleep -Seconds 3
            Save-Screen 'mygames'
            if (-not (Open-Hub 'My games & apps' 'mygames-hub')) { Fail 'A on a Play history game did not open a hub with a snapshot (see mygames-hub-not-opened.jpg)' }
            elseif (-not (Close-Hub 'My games & apps')) { Fail 'B in the hub opened from My games & apps did not let its snapshot go' }
            else { Start-Sleep -Seconds 1; Save-Screen 'mygames-after-hub' }
            $mark = Get-Mark
            Push-Button B
            Wait-Line 'My games & apps closed' 5 $mark | Out-Null
            Start-Sleep -Milliseconds 800
        }
    }
    Restore-Home 'mygames'

    # 3. From Search, tried: Y opens it with its keyboard up, B puts the keyboard away, focus going to the first game of
    # those most recently added, and A opens that game's hub.
    $mark = Get-Mark
    Push-Button Y
    Start-Sleep -Seconds 2
    Push-Button B
    Start-Sleep -Milliseconds 1500
    Save-Screen 'search'
    if (Open-Hub 'Search' 'search-hub') {
        if (-not (Close-Hub 'Search')) { Fail 'B in the hub opened from Search did not let its snapshot go' }
    } else { Say 'Search: no hub opened from it (see search-hub-not-opened.jpg); not judged' }
    # B leaves Search (the keyboard away first, if the hub never opened), back to Home's Recent row.
    foreach ($n in 1..3) {
        $back = Get-Mark
        Push-Button B
        if (Wait-Line 'Home focus on ' 3 $back) { Start-Sleep -Milliseconds 800; break }
    }
    Write-Told $mark
    Restore-Home 'search'

    # 4. Twenty openings and closings from the Recent row.
    $cycleMark = Get-Mark
    $afterFirst = $null
    foreach ($n in 1..$cycles) {
        if (-not (Test-FocusOn 'Home focus on the Recent tile ')) { Restore-Home "cycle-$n" }
        if (-not (Open-Hub "cycle $n" $(if ($n -eq 1 -or $n -eq $cycles) { "cycle-$n-hub" })) ) { Fail "cycle $n`: the hub did not open with a snapshot"; break }
        if (-not (Close-Hub "cycle $n")) { Fail "cycle $n`: the hub did not let its snapshot go"; break }
        Start-Sleep -Milliseconds 800
        if ($n -eq 1) {
            $afterFirst = Get-WorkingSetMb
            Say "working set after the first cycle: $afterFirst MB"
        }
    }
    $afterLast = Get-WorkingSetMb
    Say "working set after $cycles cycles: $afterLast MB"

    $cycleLines = Get-LogLines $cycleMark "$taken|$freed"
    $cycleLines | ForEach-Object { Say "  log: $($_ -replace '^.*(\[Hub\])', '$1')" }
    $tookCount = @($cycleLines | Where-Object { $_ -match $taken }).Count
    $freedCount = @($cycleLines | Where-Object { $_ -match $freed }).Count
    Say "the $cycles cycles: $tookCount taken, $freedCount freed"
    if ($tookCount -ne $cycles -or $freedCount -ne $cycles) { Fail "the $cycles cycles logged $tookCount taken and $freedCount freed" }
    for ($i = 0; $i -lt $cycleLines.Count; $i++) {
        $want = if ($i % 2 -eq 0) { $taken } else { $freed }
        if ($cycleLines[$i] -notmatch $want) { Fail "the snapshots don't pair up, taken then freed: line $($i + 1) is '$($cycleLines[$i])'"; break }
    }
    if ($null -ne $afterFirst) {
        $growth = [math]::Round($afterLast - $afterFirst, 1)
        Say "working set grew $growth MB over cycles 2 to $cycles (allowed: $allowedGrowthMb MB)"
        if ($growth -gt $allowedGrowthMb) { Fail "Playnite's working set grew $growth MB over $cycles hub openings, more than $allowedGrowthMb MB" }
    }

    # Over the whole run, every snapshot taken was freed.
    $all = Get-LogLines $start "$taken|$freed"
    $allTaken = @($all | Where-Object { $_ -match $taken }).Count
    $allFreed = @($all | Where-Object { $_ -match $freed }).Count
    Say "over the whole run: $allTaken taken, $allFreed freed"
    if ($allTaken -ne $allFreed) { Fail "over the whole run $allTaken snapshots were taken and $allFreed freed" }

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
}
exit $(if ($result -eq 'PASS') { 0 } else { 1 })
