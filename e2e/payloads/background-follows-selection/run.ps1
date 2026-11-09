# The background follows the selection (#147): on Home, the hero behind the Recent row turns to the focused game's art
# soon after focus moves, not seconds later. The library is the seed's, its backgrounds 3840x2160 and grained, as large
# and as slow to decode as a player's own. The pad steps along the Recent row, then holds Right, then goes down to the
# Groups and back, and the extension's "[Hero timing]" lines in extensions.log say how long each took, in milliseconds
# from the moment the tile got focus:
#   asked     the core changed the hero (focus has settled)
#   decode    how long reading and decoding the art took, and on which thread
#   fading    the first frame with the new art in it: the crossfade begins
#   shown     the crossfade has ended: the new art is fully in
# Passes when, for every step along the row, the art begins to fade in within 450 ms of focus and is fully in within
# 850 ms (the settle of 150 ms, then 300 ms and 700 ms); when a held d-pad has no art read for the tiles it passes
# over; and when no art is asked for on the Groups, where the background is the swirl. The numbers of every step are
# in steps.txt and hero-timing.csv, whether it passes or not, so a run of a build before the fix gives the "before".
#
# Nowhere else does the background follow the selection with the extension: Groups, My games & apps, Search and
# Settings draw the swirl or a flat page, and the Game hub's art is its one game's.
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()
$rows = @()
$BeginsWithin = 450
$ShownWithin = 850

function Get-HeroLines([int] $After) {
    @((Get-PlayniteLog) -split "`n" | Select-Object -Skip $After | Where-Object { $_ -match '\[Hero timing\]' } | ForEach-Object { $_.Trim() })
}

# The time of day a log line was written at, in milliseconds; $null when the line doesn't begin with one.
function Get-LineTime([string] $Line) {
    if ($Line -match '(\d\d):(\d\d):(\d\d)[.,](\d{3})') { ((([int] $Matches[1] * 60) + [int] $Matches[2]) * 60 + [int] $Matches[3]) * 1000 + [int] $Matches[4] }
}

# What the lines say of the last art the hero turned to: the game focused, and the milliseconds from its focus.
function Read-HeroStep([string[]] $Lines) {
    $step = [ordered]@{ Focus = $null; Focuses = 0; ArtsAsked = 0; Asked = $null; Decode = $null; Thread = $null; Fading = $null; Shown = $null }
    foreach ($line in $Lines) {
        if ($line -match '\[Hero timing\] focus on (.+)$') {
            $step.Focus = $Matches[1]; $step.Focuses++
            # Each focus starts the clock again: what an earlier one led to is no longer this step's.
            $step.Asked = $null; $step.Decode = $null; $step.Thread = $null; $step.Fading = $null; $step.Shown = $null
        }
        elseif ($line -match '\[Hero timing\] the swirl ') { }
        elseif ($line -match ' asked for at (\d+) ms') { $step.Asked = [int] $Matches[1]; $step.ArtsAsked++ }
        elseif ($line -match ' decoded in (\d+) ms(?: (off|on) the UI thread)?, at \d+ ms') { $step.Decode = [int] $Matches[1]; $step.Thread = if ($Matches[2]) { $Matches[2] } else { 'on' } }
        elseif ($line -match ' fading in at (\d+) ms') { $step.Fading = [int] $Matches[1] }
        elseif ($line -match ' shown at (\d+) ms') { $step.Shown = [int] $Matches[1] }
    }
    [pscustomobject] $step
}

# Presses $Button for $HoldMs, waits for the hero to have shown the art of where focus stopped (6 s at most, and
# then once more), and returns what the log says of it.
function Step-Hero([string] $What, [string] $Button, [int] $HoldMs = 100) {
    $mark = Get-PlayniteLogLength
    Push-Button $Button $HoldMs
    $deadline = (Get-Date).AddSeconds(6)
    do {
        Start-Sleep -Milliseconds 250
        $step = Read-HeroStep (Get-HeroLines $mark)
    } while ($null -eq $step.Shown -and (Get-Date) -lt $deadline)
    $lines = Get-HeroLines $mark
    $lines | ForEach-Object { Add-Content (Join-Path $Out 'hero-lines.txt') "[$What] $_" -Encoding UTF8 }
    $step = Read-HeroStep $lines
    $step | Add-Member NoteProperty What $What
    $step | Add-Member NoteProperty Lines $lines
    Say ("{0}: focus on {1}; asked {2} ms, decode {3} ms {4} the UI thread, fading in {5} ms, shown {6} ms" -f
        $What, $step.Focus, $step.Asked, $step.Decode, $step.Thread, $step.Fading, $step.Shown)
    $step
}

function Test-HeroStep($Step) {
    $bad = @()
    if ($null -eq $Step.Focus) { return ,@("$($Step.What): no Recent-row tile got focus") }
    if ($null -eq $Step.Fading) { $bad += "$($Step.What): the art of $($Step.Focus) never began to fade in" }
    elseif ($Step.Fading -gt $BeginsWithin) { $bad += "$($Step.What): the art of $($Step.Focus) began to fade in $($Step.Fading) ms after focus, over $BeginsWithin ms" }
    if ($null -eq $Step.Shown) { $bad += "$($Step.What): the art of $($Step.Focus) was not fully in within 6 s" }
    elseif ($Step.Shown -gt $ShownWithin) { $bad += "$($Step.What): the art of $($Step.Focus) was fully in $($Step.Shown) ms after focus, over $ShownWithin ms" }
    ,$bad
}

try {
    Connect-Pad
    Start-Dashboard $PSScriptRoot -Library -BackgroundWidth 3840
    Start-Sleep -Seconds 4
    Save-Screen 'home'
    $start = Read-HeroStep (Get-HeroLines 0)
    Say "Home came up with focus on $($start.Focus)"
    if ($null -eq $start.Focus) { throw 'No Recent-row tile has focus on Home (no "[Hero timing] focus on" line): is this a build with the hero timing?' }

    # Single steps along the Recent row: three to games not shown yet, two back to ones shown before.
    $n = 0
    foreach ($button in 'Right', 'Right', 'Right', 'Left', 'Left') {
        $n++
        $step = Step-Hero "step $n ($button)" $button
        $rows += $step
        $failures += Test-HeroStep $step
        if ($n -eq 1) { Save-Screen 'step-1' }
        Start-Sleep -Milliseconds 700
    }

    # A held d-pad: Playnite repeats Right every 80 ms after 700 ms. The first step settles, as a single step; the
    # tiles passed over after it have no art read; the one focus stops on has, as soon as after a single step.
    $held = Step-Hero 'held Right' 'Right' 1050
    $rows += $held
    $failures += Test-HeroStep $held
    $focusTimes = @($held.Lines | Where-Object { $_ -match 'focus on' } | ForEach-Object { Get-LineTime $_ } | Where-Object { $null -ne $_ })
    $gaps = @(for ($i = 2; $i -lt $focusTimes.Count; $i++) { $focusTimes[$i] - $focusTimes[$i - 1] })
    Say "held Right: focus passed $($held.Focuses) tiles, art asked for $($held.ArtsAsked) time(s); between repeats: $(if ($gaps.Count) { ($gaps -join ', ') + ' ms' } else { 'n/a' })"
    if ($held.Focuses -lt 3) { $failures += "held Right: focus passed only $($held.Focuses) tiles, too few to tell" }
    elseif ($held.ArtsAsked -gt 2) { $failures += "held Right: art was asked for $($held.ArtsAsked) times while focus passed $($held.Focuses) tiles; only the first and the last should be" }
    Save-Screen 'held'

    # The Groups: the swirl is behind them, whichever tile has focus. No art is asked for.
    $mark = Get-PlayniteLogLength
    Push-Button Down
    Start-Sleep -Milliseconds 1500
    Push-Button Right
    Start-Sleep -Milliseconds 1500
    Push-Button Right
    Start-Sleep -Milliseconds 2500
    Save-Screen 'groups'
    $onGroups = Read-HeroStep (Get-HeroLines $mark)
    Say "Groups: art asked for $($onGroups.ArtsAsked) time(s) while focus moved along a Group (the swirl is behind them)"
    if ($onGroups.ArtsAsked) { $failures += "Groups: art was asked for $($onGroups.ArtsAsked) time(s); the swirl should stay" }

    # Back up into the Recent row from the swirl: the art shows at once, without the settle. Told, not judged: where
    # focus lands coming back is the row's own business.
    $back = Step-Hero 'back into the row (Up)' 'Up'
    $rows += $back
    Save-Screen 'back'

    $rows | Select-Object What, Focus, Asked, Decode, Thread, Fading, Shown | Export-Csv (Join-Path $Out 'hero-timing.csv') -NoTypeInformation -Encoding UTF8
    $judged = @($rows | Where-Object { $_.What -ne 'back into the row (Up)' -and $null -ne $_.Shown })
    if ($judged.Count) {
        $worst = { param($p) ($judged | Measure-Object $p -Maximum).Maximum }
        Say "worst along the row, from focus: asked $(& $worst Asked) ms, decode $(& $worst Decode) ms, fading in $(& $worst Fading) ms (limit $BeginsWithin), shown $(& $worst Shown) ms (limit $ShownWithin)"
    }

    if (-not (Get-Process Playnite.FullscreenApp -ErrorAction SilentlyContinue)) { $failures += 'Playnite exited' }
    $errors = @(([regex]'(?m)^.*\|ERROR\|.*$').Matches((Get-PlayniteLog)) | ForEach-Object Value)
    $errors | ForEach-Object { Say "logged: $_" }
    if ($errors.Count) { $failures += "$($errors.Count) errors logged" }

    if ($failures.Count -eq 0) { $result = 'PASS' }
} catch {
    $failures += "error: $_ $($_.ScriptStackTrace)"
    Save-Screen 'error'
} finally {
    $failures | ForEach-Object { Say "failed: $_" }
    Say "result: $result"
    Save-PlayniteFiles
    Stop-Playnite
    Disconnect-Pad
}
exit $(if ($result -eq 'PASS') { 0 } else { 1 })
