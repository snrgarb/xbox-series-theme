# The background follows the selection (#147): on Home, the hero behind the Recent row turns to the focused game's art
# after the wait Settings' Background delay sets: 1.5 s with it on, as it is until the player turns it off, and 150 ms
# with it off. One run measures both. The library is the seed's, its backgrounds 3840x2160 and grained, as large and as
# slow to decode as a player's own. The extension's "[Hero timing]" lines in extensions.log say how long each step
# took, in milliseconds from the moment the tile got focus:
#   asked     the core changed the hero (focus has rested long enough)
#   decode    how long reading and decoding the art took, and on which thread
#   fading    the first frame with the new art in it: the crossfade begins
#   shown     the crossfade has ended: the new art is fully in
#
# 1. Background delay on, untouched: three steps along the Recent row. Each must have its art asked for 1400 to 2000 ms
#    after focus.
# 2. Background delay turned off with the pad, through the Settings page: Menu on the focused tile, Customise Home (the
#    fourth row of a seed game's tile menu), which opens Settings on Personalisation, Down twice to Background delay,
#    A, then B until Settings closes. The extension's "[Settings]" lines must say each of it happened: opened on
#    Personalisation, Background delay turned off, closed.
# 3. Background delay off, with no restart: seven steps along the row. Each must have its art asked for within 250 ms
#    of focus. Then Right held: no art is read for the tiles passed over. Then the Groups: no art asked for there, where
#    the background is the swirl.
#
# In both states the art must be decoded off the UI thread, begin to fade in within 1000 ms of being asked for and be
# fully in within 1500 ms of it. Those two are loose on purpose: decoding 4K art and drawing its first frame take as
# long as the machine takes, and the test VM has no GPU. What the steps took is in steps.txt and hero-timing.csv
# whether they pass or not, with a note for each step off the delay that missed 450 ms to fading in or 850 ms to shown.
#
# Nowhere else does the background follow the selection with the extension: Groups, My games & apps, Search and
# Settings draw the swirl or a flat page, and the Game hub's art is its one game's.
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()
$rows = @()
$OnAskedFrom = 1400     # Background delay on: the art is asked for 1.5 s after focus
$OnAskedTo = 2000
$OffAskedWithin = 250   # off: after 150 ms
$FadingAfterAsked = 1000
$ShownAfterAsked = 1500
$TightFading = 450      # off, on a machine that decodes and draws fast: noted, not judged
$TightShown = 850

function Get-HeroLines([int] $After) {
    @(Get-ExtensionLog | Select-Object -Skip $After | Where-Object { $_ -match '\[Hero timing\]' } | ForEach-Object { $_.Trim() })
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

# Presses $Button for $HoldMs, waits for the hero to have shown the art of where focus stopped (6 s at most), and
# returns what the log says of it.
function Step-Hero([string] $What, [string] $Button, [int] $HoldMs = 100) {
    $mark = @(Get-ExtensionLog).Count
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

# What is wrong with a step along the row, with Background delay $On or off.
function Test-HeroStep($Step, [bool] $On) {
    $what = "$($Step.What), to $($Step.Focus)"
    if ($null -eq $Step.Focus) { return ,@("$($Step.What): no Recent-row tile got focus") }
    if ($null -eq $Step.Asked) { return ,@("${what}: its art was never asked for") }
    $bad = @()
    if ($On -and ($Step.Asked -lt $OnAskedFrom -or $Step.Asked -gt $OnAskedTo)) {
        $bad += "${what}: its art was asked for $($Step.Asked) ms after focus, not $OnAskedFrom to $OnAskedTo ms, with Background delay on"
    }
    if (-not $On -and $Step.Asked -gt $OffAskedWithin) {
        $bad += "${what}: its art was asked for $($Step.Asked) ms after focus, over $OffAskedWithin ms, with Background delay off"
    }
    if ($Step.Thread -eq 'on') { $bad += "${what}: its art was decoded on the UI thread" }
    if ($null -eq $Step.Fading) { $bad += "${what}: its art never began to fade in" }
    elseif ($Step.Fading - $Step.Asked -gt $FadingAfterAsked) { $bad += "${what}: its art began to fade in $($Step.Fading - $Step.Asked) ms after it was asked for, over $FadingAfterAsked ms (decode $($Step.Decode) ms)" }
    if ($null -eq $Step.Shown) { $bad += "${what}: its art was not fully in within 6 s" }
    elseif ($Step.Shown - $Step.Asked -gt $ShownAfterAsked) { $bad += "${what}: its art was fully in $($Step.Shown - $Step.Asked) ms after it was asked for, over $ShownAfterAsked ms (decode $($Step.Decode) ms)" }
    if (-not $On -and $null -ne $Step.Shown -and ($Step.Fading -gt $TightFading -or $Step.Shown -gt $TightShown)) {
        Say "note: ${what}: fading in $($Step.Fading) ms and shown $($Step.Shown) ms after focus; a fast machine does it in $TightFading and $TightShown ms"
    }
    ,$bad
}

# The worst of the steps judged, in a line.
function Say-Worst([string] $Of, $Steps) {
    $done = @($Steps | Where-Object { $null -ne $_.Shown })
    if (-not $done.Count) { return }
    $worst = { param($p) ($done | Measure-Object $p -Maximum).Maximum }
    $least = ($done | Measure-Object Asked -Minimum).Minimum
    Say "$Of, of $($done.Count) steps, from focus: asked $least to $(& $worst Asked) ms, decode up to $(& $worst Decode) ms, fading in by $(& $worst Fading) ms, shown by $(& $worst Shown) ms"
}

# The extension's "[Settings]" lines after line $After of the log.
function Get-SettingsLines([int] $After) {
    @(Get-ExtensionLog | Select-Object -Skip $After | Where-Object { $_ -match '\[Settings\]' } | ForEach-Object { $_.Trim() })
}

# Waits up to $Seconds for a "[Settings]" line matching $Pattern after line $After of the log; whether one came.
function Wait-SettingsLine([string] $Pattern, [int] $After, [int] $Seconds = 6) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        if (@(Get-SettingsLines $After | Where-Object { $_ -match $Pattern }).Count) { return $true }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    $false
}

try {
    Connect-Pad
    Start-Dashboard $PSScriptRoot -Library -BackgroundWidth 3840
    Start-Sleep -Seconds 4
    Save-Screen 'home'
    $start = Read-HeroStep (Get-HeroLines 0)
    Say "Home came up with focus on $($start.Focus)"
    if ($null -eq $start.Focus) { throw 'No Recent-row tile has focus on Home (no "[Hero timing] focus on" line): is this a build with the hero timing?' }
    $said = @(Get-SettingsLines 0)
    $said | ForEach-Object { Say "logged: $_" }
    if (-not @($said | Where-Object { $_ -match 'Background delay is on' }).Count) {
        throw 'Background delay is not on at the start (no "[Settings] Background delay is on" line): is this a build with the setting, and is on still its default?'
    }

    # 1. Background delay on, as it is until turned off: three steps along the Recent row, ending on its second tile.
    $n = 0
    foreach ($button in 'Right', 'Right', 'Left') {
        $n++
        $step = Step-Hero "on, step $n ($button)" $button
        $rows += $step
        $failures += Test-HeroStep $step $true
        if ($n -eq 1) { Save-Screen 'on-step-1' }
        Start-Sleep -Milliseconds 700
    }
    Say-Worst 'Background delay on' $rows

    # 2. Background delay turned off with the pad. Menu on the tile opens its tile menu; a seed game's rows are Play
    # game or Install, Add to or Remove from Favourites, Manage game, Customise Home, More options (no Uninstall: no
    # library plugin owns a seed game), so Customise Home is three down. It opens Settings on Theme settings'
    # Personalisation, focus on Boot video, the first box; Who's playing today? is under it and Background delay under
    # that. B then steps back to Theme settings' boxes, to the rail, and to Home, focus on the tile again.
    $mark = @(Get-ExtensionLog).Count
    Push-Button Start
    Start-Sleep -Milliseconds 1200
    Save-Screen 'tile-menu'
    foreach ($i in 1..3) { Push-Button Down; Start-Sleep -Milliseconds 400 }
    Save-Screen 'tile-menu-customise'
    Push-Button A
    if (-not (Wait-SettingsLine 'opened on ThemeSettings, page Personalisation' $mark)) {
        Save-Screen 'settings-not-opened'
        Get-SettingsLines $mark | ForEach-Object { Say "logged: $_" }
        throw 'Customise Home did not open Settings on Personalisation (Menu, Down x3, A on the focused tile)'
    }
    Say 'Settings opened on Personalisation'
    Start-Sleep -Milliseconds 1500
    Save-Screen 'personalisation'
    foreach ($i in 1..2) { Push-Button Down; Start-Sleep -Milliseconds 400 }
    Save-Screen 'background-delay-focused'
    Push-Button A
    if (-not (Wait-SettingsLine 'Background delay turned off' $mark)) {
        Save-Screen 'background-delay-not-turned-off'
        Get-SettingsLines $mark | ForEach-Object { Say "logged: $_" }
        throw 'A did not turn Background delay off (Down x2 from Boot video, A)'
    }
    Say 'Background delay turned off'
    Start-Sleep -Milliseconds 500
    Save-Screen 'background-delay-off'
    $closed = $false
    foreach ($i in 1..4) {
        Push-Button B
        if (Wait-SettingsLine 'closed' $mark 2) { $closed = $true; break }
    }
    Get-SettingsLines $mark | ForEach-Object { Say "logged: $_" }
    if (-not $closed) { Save-Screen 'settings-not-closed'; throw 'Settings did not close after B four times' }
    $toggles = @(Get-SettingsLines $mark | Where-Object { $_ -match 'Background delay turned' })
    if ($toggles.Count -ne 1) { throw "Background delay was turned over $($toggles.Count) times, not once" }
    Start-Sleep -Milliseconds 2500
    Save-Screen 'home-again'
    $back = Read-HeroStep (Get-HeroLines $mark)
    Say "Home is back with focus on $($back.Focus)"
    if ($null -eq $back.Focus) { throw 'No Recent-row tile got focus as Settings closed' }

    # 3. Background delay off, in the same run: seven steps along the row from its second tile, to its fifth (the
    # fourth and fifth not shown yet) and back to its first.
    $off = @()
    $n = 0
    foreach ($button in 'Right', 'Right', 'Right', 'Left', 'Left', 'Left', 'Left') {
        $n++
        $step = Step-Hero "off, step $n ($button)" $button
        $off += $step
        $failures += Test-HeroStep $step $false
        if ($n -eq 1) { Save-Screen 'off-step-1' }
        Start-Sleep -Milliseconds 700
    }

    # A held d-pad: Playnite repeats Right every 80 ms after 700 ms, so 950 ms moves focus four tiles or so along the
    # row's seven, short of the Play history tile after them. The first step settles, as a single step; the tiles
    # passed over after it have no art read; the one focus stops on has, as soon as after a single step.
    $held = Step-Hero 'off, held Right' 'Right' 950
    $off += $held
    $failures += Test-HeroStep $held $false
    $focusTimes = @($held.Lines | Where-Object { $_ -match 'focus on' } | ForEach-Object { Get-LineTime $_ } | Where-Object { $null -ne $_ })
    $gaps = @(for ($i = 2; $i -lt $focusTimes.Count; $i++) { $focusTimes[$i] - $focusTimes[$i - 1] })
    Say "held Right: focus passed $($held.Focuses) tiles, art asked for $($held.ArtsAsked) time(s); between repeats: $(if ($gaps.Count) { ($gaps -join ', ') + ' ms' } else { 'n/a' })"
    if ($held.Focuses -lt 3) { $failures += "held Right: focus passed only $($held.Focuses) tiles, too few to tell" }
    elseif ($held.ArtsAsked -gt 2) { $failures += "held Right: art was asked for $($held.ArtsAsked) times while focus passed $($held.Focuses) tiles; only the first and the last should be" }
    Save-Screen 'held'
    Say-Worst 'Background delay off' $off
    $rows += $off

    # The Groups: the swirl is behind them, whichever tile has focus. No art is asked for.
    $mark = @(Get-ExtensionLog).Count
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

    # Back up into the Recent row from the swirl: the art shows at once, without the wait. Told, not judged: where
    # focus lands coming back is the row's own business.
    $up = Step-Hero 'off, back into the row (Up)' 'Up'
    $rows += $up
    Save-Screen 'back'

    $rows | Select-Object What, Focus, Asked, Decode, Thread, Fading, Shown | Export-Csv (Join-Path $Out 'hero-timing.csv') -NoTypeInformation -Encoding UTF8

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
