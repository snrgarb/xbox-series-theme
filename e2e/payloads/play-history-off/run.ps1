# Show Play history turned off (#210): Settings > Theme settings > Personalisation's checkbox, under Reduce motion. One
# run walks Home's Recent row with it on, turns it off with the pad, and walks the row again, at once and after a restart
# of Playnite. The library is the seed's fourteen games, enough to fill the row either way. What the log says:
#   [Settings] Play history is on|off          as the extension starts
#   [Settings] Play history turned on|off      as A turns it over
#   Home focus on the Recent tile <game>       a game tile of the Recent row took focus
#   Home focus on the Play history tile        the Play history tile took focus
#
# 1. Untouched: "Play history is on", and a walk along the row reaches seven distinct games and the Play history tile.
# 2. Turned off with the pad, through the Settings page: Menu on the focused tile, Customise Home (the fourth row of a
#    seed game's tile menu), which opens Settings on Personalisation; then down to Show Play history a press at a time,
#    each chosen by where the extension's "[Settings]" lines say Settings is and checked to have moved it; A, which must
#    log "Play history turned off", once; then B until Settings closes.
# 3. Off, with no restart: the same walk reaches eight distinct games and never the Play history tile.
# 4. Playnite stopped and started again: "Play history is off", and the walk still reaches eight games and no tile.
#
# The row wraps, so a walk starts wherever focus is in the row and keeps each distinct tile until one comes round again
# (or, should the row stop at its ends, walks back the other way). The tiles each walk reached are in steps.txt, and row-on.jpg, row-off.jpg and row-restarted.jpg show the row at its end.
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()

# The extension's lines matching $Pattern after line $After of the log.
function Get-LogLines([string] $Pattern, [int] $After) {
    @(Get-ExtensionLog | Select-Object -Skip $After | Where-Object { $_ -match $Pattern } | ForEach-Object { $_.Trim() })
}

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

# Home's focus by the log's last word on it, as "the Recent tile Alan Wake 2" or "the Play history tile"; $null with none.
function Get-HomeFocus {
    $last = Get-LogLines 'Home focus on ' 0 | Select-Object -Last 1
    if ($last) { $last -replace '^.*Home focus on ', '' }
}

# Whether Home's focus, by the log, is on the Recent row: one of its games or its Play history tile.
function Test-InRow { [bool]((Get-HomeFocus) -match '^the (Recent tile .+|Play history tile)$') }

# Puts focus in the Recent row, wherever along it: Down from the top bar, where Home starts, until the log says a tile
# of the row has it. Throws when none does. Say and Push-Button write to the output, so all of it goes to the host.
function Enter-Row {
    foreach ($i in 1..3) {
        if (Test-InRow) { return }
        $mark = @(Get-ExtensionLog).Count
        Push-Button Down | Out-Host
        Wait-LogLine 'Home focus on ' $mark 2 | Out-Null
        Start-Sleep -Milliseconds 400
    }
    if (-not (Test-InRow)) { Save-Screen 'no-recent-row' | Out-Host; throw "No Recent-row tile has focus on Home; it is on: $(Get-HomeFocus)" }
}

# Presses $Button once; the tile focus moved to by the log, or $null when it moved nowhere.
function Step-Row([string] $Button) {
    $mark = @(Get-ExtensionLog).Count
    Push-Button $Button | Out-Host
    if (-not (Wait-LogLine 'Home focus on ' $mark 2)) { return $null }
    Start-Sleep -Milliseconds 300
    Get-HomeFocus
}

# Walks the Recent row from wherever focus is in it with Right, a press at a time, keeping each distinct tile reached,
# until a press reaches one already kept (the row wraps) or moves nowhere (it stopped at its end: then Left from there
# to the other end, the same way). The tiles go into $script:RowTiles, in the order reached; nothing is returned, as
# Say's lines would mix with it. A screenshot "$Shot" at the end.
function Read-Row([string] $Shot) {
    Enter-Row
    $tiles = [Collections.Generic.List[string]]::new()
    $tiles.Add((Get-HomeFocus))
    foreach ($button in 'Right', 'Left') {
        $ended = $false
        foreach ($i in 1..12) {
            $now = Step-Row $button
            if ($null -eq $now) { break }
            if ($tiles.Contains($now)) { $ended = $true; break }
            $tiles.Add($now)
        }
        if ($ended) { break }
    }
    Save-Screen $Shot | Out-Host
    Say "${Shot}: $($tiles.Count) tiles: $($tiles -join ' | ')" | Out-Host
    $script:RowTiles = @($tiles)
}
# What is wrong with the row Read-Row last walked ($script:RowTiles), walked $When, for Show Play history $On: on,
# seven distinct games and the Play history tile; off, eight distinct games and no tile. The walk may start mid-row, as
# the row wraps, so where the tile comes in the walk isn't judged.
function Test-Row([bool] $On, [string] $When) {
    $games = @($script:RowTiles | Where-Object { $_ -like 'the Recent tile *' } | Sort-Object -Unique)
    $history = @($script:RowTiles | Where-Object { $_ -eq 'the Play history tile' })
    $want = if ($On) { 7 } else { 8 }
    $bad = @()
    if ($games.Count -ne $want) { $bad += "${When}: the Recent row has $($games.Count) games, not $want" }
    if ($On -and $history.Count -ne 1) { $bad += "${When}: the Recent row has no Play history tile" }
    if (-not $On -and $history.Count) { $bad += "${When}: the Play history tile took focus with Show Play history off" }
    , $bad
}

# Where Settings is, by its "[Settings]" lines after line $After of the log, as "Personalisation/ShowPlayHistoryBox".
function Get-SettingsPlace([int] $After) {
    $page = $null; $focus = $null
    foreach ($line in Get-SettingsLines $After) {
        if ($line -match '\[Settings\] opened on \w+, page (\w+)') { $page = $Matches[1] }
        elseif ($line -match '\[Settings\] page (\w+)') { $page = $Matches[1] }
        elseif ($line -match '\[Settings\] focus on (\w+)') { $focus = $Matches[1] }
    }
    "$page/$focus"
}

# Takes Settings, wherever it opened and whatever has focus, to Personalisation's Show Play history box, a press at a
# time: each press is chosen by where the log says Settings is, and must move it. Throws when the log doesn't say where
# Settings is, or a press changes nothing.
function Move-ToShowPlayHistory([int] $After) {
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
        'Personalisation/ReduceMotionBox'    = 'Down'    # to Show Play history
    }
    foreach ($i in 1..14) {
        $place = Get-SettingsPlace $After
        if ($place -eq 'Personalisation/ShowPlayHistoryBox') { return }
        # Personalisation has opened but its first box hasn't said it has focus yet: wait for it rather than press.
        if ($place -eq 'Personalisation/PersonalisationBox') {
            $waited = [Diagnostics.Stopwatch]::StartNew()
            while ((Get-SettingsPlace $After) -eq $place -and $waited.ElapsedMilliseconds -lt 3000) { Start-Sleep -Milliseconds 200 }
            if ((Get-SettingsPlace $After) -eq $place) { Save-Screen 'settings-stuck'; throw "Personalisation opened but none of its boxes took focus" }
            continue
        }
        $button = $press[$place]
        if (-not $button) { Save-Screen 'settings-lost'; throw "Settings is at $place, from where the test knows no way to Show Play history" }
        Say "Settings is at ${place}: $button"
        Push-Button $button
        $waited = [Diagnostics.Stopwatch]::StartNew()
        while ((Get-SettingsPlace $After) -eq $place -and $waited.ElapsedMilliseconds -lt 3000) { Start-Sleep -Milliseconds 200 }
        if ((Get-SettingsPlace $After) -eq $place) { Save-Screen 'settings-stuck'; throw "$button at $place changed nothing" }
        # A page opening says so before its first box gets focus: let both be logged.
        Start-Sleep -Milliseconds 800
    }
    Save-Screen 'settings-lost'
    throw "Settings is at $(Get-SettingsPlace $After) after 14 presses, not on Show Play history"
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
    Start-Sleep -Seconds 6  # the "Hi <user>!" toast over Home's foot has gone
    Save-Screen 'home'

    # 1. Untouched: on, seven games and the Play history tile.
    $said = @(Get-LogLines '\[Settings\] Play history' 0)
    $said | ForEach-Object { Say "logged: $_" }
    if (-not @($said | Where-Object { $_ -match '\[Settings\] Play history is on' }).Count) {
        throw 'Show Play history is not on at the start (no "[Settings] Play history is on" line): is this a build with the setting?'
    }
    Read-Row 'row-on'
    $failures += Test-Row $true 'on'

    # 2. Turned off with the pad, through Customise Home's Settings, from a game of the row.
    Enter-Row
    if ((Get-HomeFocus) -notlike 'the Recent tile *') { Step-Row Right | Out-Null }
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
    Say "Settings opened, at $(Get-SettingsPlace $mark)"
    try { Move-ToShowPlayHistory $mark } finally { Get-SettingsLines $mark | ForEach-Object { Say "logged: $_" } }
    Save-Screen 'show-play-history-focused'
    $toggle = @(Get-ExtensionLog).Count
    Push-Button A
    if (-not (Wait-LogLine '\[Settings\] Play history turned off' $toggle)) {
        Save-Screen 'show-play-history-not-turned-off'
        throw 'A on Show Play history did not turn it off'
    }
    Start-Sleep -Milliseconds 500
    Save-Screen 'show-play-history-off'
    $closed = $false
    foreach ($i in 1..4) {
        Push-Button B
        if (Wait-LogLine '\[Settings\] closed' $mark 2) { $closed = $true; break }
    }
    if (-not $closed) { Save-Screen 'settings-not-closed'; throw 'Settings did not close after B four times' }
    $toggles = @(Get-LogLines '\[Settings\] Play history turned' $mark)
    if ($toggles.Count -ne 1) { $failures += "Show Play history was turned over $($toggles.Count) times, not once" }
    Start-Sleep -Seconds 2

    # 3. Off, with no restart: eight games, no Play history tile.
    Read-Row 'row-off'
    $failures += Test-Row $false 'off'

    if (-not (Get-Process Playnite.FullscreenApp -ErrorAction SilentlyContinue)) { $failures += 'Playnite exited' }
    $errors = Get-LoggedErrors
    if ($errors) { $failures += "$errors errors logged before the restart" }

    # 4. Playnite closed as its window's close does it, so it keeps the Dashboard, and started again.
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
    Start-Sleep -Seconds 6
    Save-Screen 'restarted-home'
    $said = @(Get-LogLines '\[Settings\] Play history' 0)
    $said | ForEach-Object { Say "logged: $_" }
    if (-not @($said | Where-Object { $_ -match '\[Settings\] Play history is off' }).Count) {
        $failures += 'Show Play history is not off after the restart (no "[Settings] Play history is off" line): it was not saved'
    }
    Read-Row 'row-restarted'
    $failures += Test-Row $false 'after the restart'

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
