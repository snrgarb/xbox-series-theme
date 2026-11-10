# Reduce motion (#156): Settings > Theme settings > Personalisation's checkbox that turns the Dashboard's animations
# off, for a PC they cost frames on. One run turns it on with the pad and shows from extensions.log that it took effect
# at once and survived a restart of Playnite. The library is the seed's. What the log says:
#   [Settings] Reduce motion is off|on        as the extension starts
#   [Settings] Reduce motion turned on|off    as A turns it over
#   [Motion] swirl moving|still               a green swirl shown in front began to move, or stands still for the setting
#   [Hero timing] ... fading in|shown at N ms the hero's crossfade to a Recent-row game's art began, and ended
#
# 1. Untouched: "Reduce motion is off", and three steps along the Recent row, each crossfade (fading in to shown)
#    300 ms or more: it is 400 ms. By then a swirl has moved (Who's playing today?'s, or Home's under a crossfade).
# 2. Turned on with the pad, through the Settings page: Menu on the focused tile, Customise Home (the fourth row of a
#    seed game's tile menu), which opens Settings; then to Personalisation's Reduce motion box a press at a time, each
#    chosen by where the extension's "[Settings]" lines say Settings is and checked to have moved it; A, which must log
#    "Reduce motion turned on", once; then B until Settings closes.
# 3. On, with no restart: three steps along the row, each crossfade under 300 ms and at least 100 ms shorter than the
#    shortest before: it is 150 ms. Then Down to the Groups, where the swirl is the background: "swirl still" must
#    have been logged since the setting was turned on, and "swirl moving" not.
# 4. Playnite stopped and started again: "Reduce motion is on", a swirl still and none moving, and a step along the
#    row crossfading in under 300 ms.
#
# The hero waits 1.5 s on a tile before it turns to its art (Background delay, on as it is until turned off), so a step
# takes two seconds; only the crossfade is judged here. What each step took is in steps.txt and hero-timing.csv.
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()
$rows = @()
$FullCrossfadeFrom = 300    # moving: the crossfade is 400 ms
$ReducedCrossfadeWithin = 300   # reduced: 150 ms
$ReducedShorterBy = 100

function Get-HeroLines([int] $After) {
    @(Get-ExtensionLog | Select-Object -Skip $After | Where-Object { $_ -match '\[Hero timing\]' } | ForEach-Object { $_.Trim() })
}

# The extension's lines matching $Pattern after line $After of the log.
function Get-LogLines([string] $Pattern, [int] $After) {
    @(Get-ExtensionLog | Select-Object -Skip $After | Where-Object { $_ -match $Pattern } | ForEach-Object { $_.Trim() })
}

# What the lines say of the last art the hero turned to: the game focused, and the milliseconds from its focus.
function Read-HeroStep([string[]] $Lines) {
    $step = [ordered]@{ Focus = $null; Asked = $null; Fading = $null; Shown = $null; Crossfade = $null }
    foreach ($line in $Lines) {
        if ($line -match '\[Hero timing\] focus on (.+?)(?: \(\d+ ms after the last\))?$') {
            # Each focus starts the clock again: what an earlier one led to is no longer this step's.
            $step.Focus = $Matches[1]; $step.Asked = $null; $step.Fading = $null; $step.Shown = $null
        }
        elseif ($line -match '\[Hero timing\] the swirl ') { }
        elseif ($line -match ' asked for at (\d+) ms') { $step.Asked = [int] $Matches[1] }
        elseif ($line -match ' fading in at (\d+) ms') { $step.Fading = [int] $Matches[1] }
        elseif ($line -match ' shown at (\d+) ms') { $step.Shown = [int] $Matches[1] }
    }
    if ($null -ne $step.Fading -and $null -ne $step.Shown) { $step.Crossfade = $step.Shown - $step.Fading }
    [pscustomobject] $step
}

# Presses $Button, waits for the hero to have shown the art of where focus stopped (6 s at most), and returns what
# the log says of it, alone: what Say writes on the way goes to the host, not into what the caller is handed.
function Step-Hero([string] $What, [string] $Button) {
    $mark = @(Get-ExtensionLog).Count
    Push-Button $Button 100 | Out-Host
    $waited = [Diagnostics.Stopwatch]::StartNew()
    do {
        Start-Sleep -Milliseconds 250
        $step = Read-HeroStep (Get-HeroLines $mark)
    } while ($null -eq $step.Shown -and $waited.ElapsedMilliseconds -lt 6000)
    $lines = Get-HeroLines $mark
    $lines | ForEach-Object { Add-Content (Join-Path $Out 'hero-lines.txt') "[$What] $_" -Encoding UTF8 }
    $step = Read-HeroStep $lines
    $step | Add-Member NoteProperty What $What
    Say ("{0}: focus on {1}; asked {2} ms, fading in {3} ms, shown {4} ms: a crossfade of {5} ms" -f
        $What, $step.Focus, $step.Asked, $step.Fading, $step.Shown, $step.Crossfade) | Out-Host
    $step
}

# What is wrong with a step along the row, with Reduce motion $On or off; $Shortest is the shortest crossfade measured
# with it off, for a step with it on.
function Test-HeroStep($Step, [bool] $On, $Shortest) {
    $what = "$($Step.What), to $($Step.Focus)"
    if ($null -eq $Step.Focus) { return ,@("$($Step.What): no Recent-row tile got focus") }
    if ($null -eq $Step.Crossfade) { return ,@("${what}: its art did not fade in and show within 6 s (fading in $($Step.Fading), shown $($Step.Shown))") }
    $bad = @()
    if (-not $On -and $Step.Crossfade -lt $FullCrossfadeFrom) {
        $bad += "${what}: the crossfade took $($Step.Crossfade) ms, under $FullCrossfadeFrom ms, with Reduce motion off"
    }
    if ($On -and $Step.Crossfade -ge $ReducedCrossfadeWithin) {
        $bad += "${what}: the crossfade took $($Step.Crossfade) ms, not under $ReducedCrossfadeWithin ms, with Reduce motion on"
    }
    if ($On -and $null -ne $Shortest -and $Step.Crossfade -gt $Shortest - $ReducedShorterBy) {
        $bad += "${what}: the crossfade took $($Step.Crossfade) ms, not $ReducedShorterBy ms shorter than the $Shortest ms it took with Reduce motion off"
    }
    ,$bad
}

# With no Recent-row tile focused since line $After of the log, Down puts focus on the row's first game. Returns the
# game with focus, or $null.
function Get-RowFocus([int] $After) {
    $at = Read-HeroStep (Get-HeroLines $After)
    if ($null -eq $at.Focus) {
        Push-Button Down | Out-Host
        Start-Sleep -Seconds 4
        $at = Read-HeroStep (Get-HeroLines $After)
    }
    $at.Focus
}

# The extension's "[Settings]" lines after line $After of the log.
function Get-SettingsLines([int] $After) { Get-LogLines '\[Settings\]' $After }

# Waits up to $Seconds for a line of the extension's matching $Pattern after line $After of the log; whether one came.
function Wait-LogLine([string] $Pattern, [int] $After, [int] $Seconds = 6) {
    $waited = [Diagnostics.Stopwatch]::StartNew()
    do {
        if (@(Get-LogLines $Pattern $After).Count) { return $true }
        Start-Sleep -Milliseconds 250
    } while ($waited.ElapsedMilliseconds -lt $Seconds * 1000)
    $false
}

# Where Settings is, by its "[Settings]" lines after line $After of the log: the page of Theme settings it shows (None
# for Theme settings' own boxes) and the rail item or box with focus, by name, as "Personalisation/BootVideoBox".
function Get-SettingsPlace([int] $After) {
    $page = $null; $focus = $null
    foreach ($line in Get-SettingsLines $After) {
        if ($line -match '\[Settings\] opened on \w+, page (\w+)') { $page = $Matches[1] }
        elseif ($line -match '\[Settings\] page (\w+)') { $page = $Matches[1] }
        elseif ($line -match '\[Settings\] focus on (\w+)') { $focus = $Matches[1] }
    }
    "$page/$focus"
}

# Takes Settings, wherever it opened and whatever has focus, to Personalisation's Reduce motion box, a press at a time:
# each press is chosen by where the log says Settings is, and must move it. A is pressed only on Theme settings'
# Personalisation box. Throws when the log doesn't say where Settings is, or a press changes nothing.
function Move-ToReduceMotion([int] $After) {
    $press = @{
        'None/ThemeSettingsItem'             = 'Right'   # the rail: into Theme settings' boxes, on Personalisation
        'None/PlayniteSettingsItem'          = 'Up'
        'None/MorePlayniteOptionsItem'       = 'Up'
        'None/PersonalisationBox'            = 'A'       # opens Personalisation, focus on Boot video
        'None/IdleBox'                       = 'Left'
        'None/GuideBox'                      = 'Up'
        'None/DiscordBox'                    = 'Up'
        'Personalisation/BootVideoBox'       = 'Down'    # to Who's playing today?
        'Personalisation/ProfilesBox'        = 'Down'
        'Personalisation/AskBox'             = 'Down'    # to Background delay
        'Personalisation/BackgroundDelayBox' = 'Down'    # to Reduce motion
    }
    foreach ($i in 1..14) {
        $place = Get-SettingsPlace $After
        if ($place -eq 'Personalisation/ReduceMotionBox') { return }
        $button = $press[$place]
        if (-not $button) { Save-Screen 'settings-lost'; throw "Settings is at $place, from where the test knows no way to Reduce motion" }
        Say "Settings is at ${place}: $button"
        Push-Button $button
        $waited = [Diagnostics.Stopwatch]::StartNew()
        while ((Get-SettingsPlace $After) -eq $place -and $waited.ElapsedMilliseconds -lt 3000) { Start-Sleep -Milliseconds 200 }
        if ((Get-SettingsPlace $After) -eq $place) { Save-Screen 'settings-stuck'; throw "$button at $place changed nothing" }
        # A page opening says so before its first box gets focus: let both be logged.
        Start-Sleep -Milliseconds 800
    }
    Save-Screen 'settings-lost'
    throw "Settings is at $(Get-SettingsPlace $After) after 14 presses, not on Reduce motion"
}

# The errors Playnite and the extension have logged, said and counted.
function Get-LoggedErrors {
    $errors = @(([regex]'(?m)^.*\|ERROR\|.*$').Matches((Get-PlayniteLog)) | ForEach-Object Value)
    $errors | ForEach-Object { Say "logged: $_" | Out-Host }
    $errors.Count
}

try {
    Connect-Pad
    Start-Dashboard $PSScriptRoot -Library
    Start-Sleep -Seconds 4
    Save-Screen 'home'
    $focus = Get-RowFocus 0
    Say "Home came up with focus on $focus"
    if ($null -eq $focus) { throw 'No Recent-row tile has focus on Home (no "[Hero timing] focus on" line)' }

    # 1. Untouched: off, the hero crossfading in its 400 ms, and a swirl moving: Who's playing today?'s, or Home's,
    # which shows in front under each crossfade at the latest.
    $said = @(Get-LogLines '\[Settings\] Reduce motion' 0)
    $said | ForEach-Object { Say "logged: $_" }
    if (-not @($said | Where-Object { $_ -match '\[Settings\] Reduce motion is off' }).Count) {
        throw 'Reduce motion is not off at the start (no "[Settings] Reduce motion is off" line): is this a build with the setting, and is off still its default?'
    }

    $moving = @()
    $n = 0
    foreach ($button in 'Right', 'Right', 'Left') {
        $n++
        $step = Step-Hero "moving, step $n ($button)" $button
        $moving += $step
        $failures += Test-HeroStep $step $false $null
        if ($n -eq 1) { Save-Screen 'moving-step-1' }
        Start-Sleep -Milliseconds 700
    }
    $rows += $moving
    $measured = @($moving | Where-Object { $null -ne $_.Crossfade })
    if (-not $measured.Count) { throw 'No crossfade could be measured with Reduce motion off' }
    $shortest = ($measured | Measure-Object Crossfade -Minimum).Minimum
    Say "Reduce motion off: crossfades of $(($measured | ForEach-Object Crossfade) -join ', ') ms"
    $said = @(Get-LogLines '\[Motion\]' 0)
    $said | ForEach-Object { Say "logged: $_" }
    if (-not @($said | Where-Object { $_ -match '\[Motion\] swirl moving' }).Count) {
        throw 'No swirl has moved with Reduce motion off (no "[Motion] swirl moving" line): is this a build that says so?'
    }
    if (@($said | Where-Object { $_ -match '\[Motion\] swirl still' }).Count) { $failures += 'A swirl stands still for Reduce motion while it is off' }

    # 2. Reduce motion turned on with the pad. Menu on the tile opens its tile menu; a seed game's rows are Play game or
    # Install, Add to or Remove from Favourites, Manage game, Customise Home, More options, so Customise Home is three
    # down. It opens Settings, on Personalisation or, while #146's kind of bug stands, on the rail: either way
    # Move-ToReduceMotion goes from where the log says Settings is. B then steps back a level at a time until it closes.
    $mark = @(Get-ExtensionLog).Count
    Push-Button Start
    Start-Sleep -Milliseconds 1200
    Save-Screen 'tile-menu'
    foreach ($i in 1..3) { Push-Button Down; Start-Sleep -Milliseconds 400 }
    Save-Screen 'tile-menu-customise'
    Push-Button A
    if (-not (Wait-LogLine '\[Settings\] opened on ThemeSettings' $mark)) {
        Save-Screen 'settings-not-opened'
        Get-SettingsLines $mark | ForEach-Object { Say "logged: $_" }
        throw 'Customise Home did not open Settings (Menu, Down x3, A on the focused tile)'
    }
    Start-Sleep -Milliseconds 1500
    Save-Screen 'settings-opened'
    Say "Settings opened, at $(Get-SettingsPlace $mark)"
    try { Move-ToReduceMotion $mark } finally { Get-SettingsLines $mark | ForEach-Object { Say "logged: $_" } }
    Save-Screen 'reduce-motion-focused'
    $toggle = @(Get-ExtensionLog).Count
    Push-Button A
    if (-not (Wait-LogLine '\[Settings\] Reduce motion turned on' $toggle)) {
        Save-Screen 'reduce-motion-not-turned-on'
        Get-SettingsLines $toggle | ForEach-Object { Say "logged: $_" }
        throw 'A on Reduce motion did not turn it on'
    }
    Say 'Reduce motion turned on'
    Start-Sleep -Milliseconds 500
    Save-Screen 'reduce-motion-on'
    $closed = $false
    foreach ($i in 1..4) {
        Push-Button B
        if (Wait-LogLine '\[Settings\] closed' $mark 2) { $closed = $true; break }
    }
    Get-SettingsLines $toggle | ForEach-Object { Say "logged: $_" }
    if (-not $closed) { Save-Screen 'settings-not-closed'; throw 'Settings did not close after B four times' }
    $toggles = @(Get-LogLines '\[Settings\] Reduce motion turned' $mark)
    if ($toggles.Count -ne 1) { throw "Reduce motion was turned over $($toggles.Count) times, not once" }
    Start-Sleep -Milliseconds 2500
    $focus = Get-RowFocus $toggle
    Save-Screen 'home-again'
    Say "Home is back with focus on $focus"
    if ($null -eq $focus) { throw 'No Recent-row tile got focus after Settings closed' }

    # 3. On, in the same run: the hero crossfades in 150 ms.
    $reduced = @()
    $n = 0
    foreach ($button in 'Right', 'Right', 'Left') {
        $n++
        $step = Step-Hero "reduced, step $n ($button)" $button
        $reduced += $step
        $failures += Test-HeroStep $step $true $shortest
        if ($n -eq 1) { Save-Screen 'reduced-step-1' }
        Start-Sleep -Milliseconds 700
    }
    $rows += $reduced
    Say "Reduce motion on: crossfades of $((@($reduced | Where-Object { $null -ne $_.Crossfade }) | ForEach-Object Crossfade) -join ', ') ms; the shortest with it off was $shortest ms"

    # The Groups: the swirl is behind them, and shows in front as the art fades out over it. It stands still.
    Push-Button Down
    Start-Sleep -Milliseconds 2500
    Save-Screen 'groups'
    $swirl = @(Get-LogLines '\[Motion\]' $toggle)
    $swirl | ForEach-Object { Say "logged: $_" }
    if (-not (Wait-LogLine '\[Motion\] swirl still' $toggle 4)) { $failures += 'No swirl stood still after Reduce motion was turned on (no "[Motion] swirl still" line)' }
    if (@(Get-LogLines '\[Motion\] swirl moving' $toggle).Count) { $failures += 'A swirl began to move after Reduce motion was turned on' }

    if (-not (Get-Process Playnite.FullscreenApp -ErrorAction SilentlyContinue)) { $failures += 'Playnite exited' }
    $errors = Get-LoggedErrors
    if ($errors) { $failures += "$errors errors logged before the restart" }

    # 4. Playnite stopped and started again, its first logs kept as first-*.log so the lines read are the new start's.
    # Closed as its window's close does it, so Playnite saves that it shows the Dashboard, which it chose on this first
    # start; killed, it would come up again on its own theme and the extension would stay dormant.
    $playnite = Get-Process Playnite.FullscreenApp -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($playnite) {
        $playnite.CloseMainWindow() | Out-Null
        if (-not $playnite.WaitForExit(20000)) { Say 'Playnite did not close in 20 s; stopping it' }
    }
    Stop-Playnite
    Start-Sleep -Seconds 2
    Save-PlayniteFiles -Move 'first'
    Say 'Playnite stopped; starting it again'
    Enter-Dashboard
    Start-Sleep -Seconds 4
    Save-Screen 'restarted-home'
    $focus = Get-RowFocus 0
    Say "Home came up again with focus on $focus"
    $said = @(Get-LogLines '\[Settings\] Reduce motion' 0)
    $said | ForEach-Object { Say "logged: $_" }
    if (-not @($said | Where-Object { $_ -match '\[Settings\] Reduce motion is on' }).Count) {
        $failures += 'Reduce motion is not on after the restart (no "[Settings] Reduce motion is on" line): it was not saved'
    }
    if ($null -eq $focus) { $failures += 'No Recent-row tile has focus on Home after the restart' }
    else {
        $step = Step-Hero 'restarted, reduced (Right)' 'Right'
        $rows += $step
        $failures += Test-HeroStep $step $true $shortest
        Save-Screen 'restarted-step'
    }
    $said = @(Get-LogLines '\[Motion\]' 0)
    $said | ForEach-Object { Say "logged: $_" }
    if (-not @($said | Where-Object { $_ -match '\[Motion\] swirl still' }).Count) {
        $failures += 'No swirl stands still after the restart (no "[Motion] swirl still" line)'
    }
    if (@($said | Where-Object { $_ -match '\[Motion\] swirl moving' }).Count) { $failures += 'A swirl moved after the restart, with Reduce motion on' }

    $rows | Select-Object What, Focus, Asked, Fading, Shown, Crossfade | Export-Csv (Join-Path $Out 'hero-timing.csv') -NoTypeInformation -Encoding UTF8

    if (-not (Get-Process Playnite.FullscreenApp -ErrorAction SilentlyContinue)) { $failures += 'Playnite exited after the restart' }
    $errors = Get-LoggedErrors
    if ($errors) { $failures += "$errors errors logged after the restart" }

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
