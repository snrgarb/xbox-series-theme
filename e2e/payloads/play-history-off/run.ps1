# Show Play history turned off (#210): Settings > Theme settings > Personalisation's checkbox, under Reduce motion. One
# run walks Home's Recent row with it on, turns it off with the pad, and walks the row again, at once and after a restart
# of Playnite. The library is the seed's fourteen games, enough to fill the row either way. What the log says:
#   [Settings] Play history is on|off          as the extension starts
#   [Settings] Play history turned on|off      as A turns it over
#   Home focus on the Recent tile <game>       a game tile of the Recent row took focus
#   Home focus on the Play history tile        the Play history tile took focus
#
# 1. Untouched: "Play history is on", and Right from the row's first game, a press at a time, reaches seven games and
#    then the Play history tile.
# 2. Turned off with the pad, through the Settings page: Menu on the focused tile, Customise Home (the fourth row of a
#    seed game's tile menu), which opens Settings on Personalisation; then down to Show Play history a press at a time,
#    each chosen by where the extension's "[Settings]" lines say Settings is and checked to have moved it; A, which must
#    log "Play history turned off", once; then B until Settings closes.
# 3. Off, with no restart: the same walk reaches eight games and never the Play history tile.
# 4. Playnite stopped and started again: "Play history is off", and the walk still reaches eight games and no tile.
#
# The row either stops at its last tile or wraps to its first; the walk ends at whichever comes first. The tiles each
# walk reached are in steps.txt, and row-on.jpg, row-off.jpg and row-restarted.jpg show the row at its end.
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

# Home's focus by the log's last word on it, as "the Recent tile Amber Circuit" or "the Play history tile"; $null with none.
function Get-HomeFocus {
    $last = Get-LogLines 'Home focus on ' 0 | Select-Object -Last 1
    if ($last) { $last -replace '^.*Home focus on ', '' }
}

# Puts focus on the Recent row's first game: Down from the top bar, where Home starts, then Left until the log says
# focus stopped moving or wrapped. Throws when no Recent tile takes focus.
function Enter-RowStart {
    foreach ($i in 1..3) {
        if ((Get-HomeFocus) -like 'the Recent tile *') { break }
        $mark = @(Get-ExtensionLog).Count
        Push-Button Down
        Wait-LogLine 'Home focus on ' $mark 2 | Out-Null
        Start-Sleep -Milliseconds 400
    }
    if ((Get-HomeFocus) -notlike 'the Recent tile *') { Save-Screen 'no-recent-row'; throw "No Recent-row tile has focus on Home; it is on: $(Get-HomeFocus)" }

    # Left to the first: a press that logs no new focus has stopped at it; one that lands on a tile seen already wrapped,
    # and Right takes it back to the first.
    $seen = @(Get-HomeFocus)
    foreach ($i in 1..10) {
        $mark = @(Get-ExtensionLog).Count
        Push-Button Left
        if (-not (Wait-LogLine 'Home focus on ' $mark 2)) { return }
        Start-Sleep -Milliseconds 300
        $now = Get-HomeFocus
        if ($seen -contains $now) {
            $mark = @(Get-ExtensionLog).Count
            Push-Button Right
            Wait-LogLine 'Home focus on ' $mark 2 | Out-Null
            Start-Sleep -Milliseconds 300
            return
        }
        $seen += $now
    }
}

# Walks the Recent row from its first game with Right, a press at a time, until a press moves focus nowhere or back to a
# tile already reached; every tile reached, in order, as Get-HomeFocus names them. A screenshot "$Shot" at the end.
function Get-RowTiles([string] $Shot) {
    Enter-RowStart
    $tiles = @(Get-HomeFocus)
    foreach ($i in 1..12) {
        $mark = @(Get-ExtensionLog).Count
        Push-Button Right
        if (-not (Wait-LogLine 'Home focus on ' $mark 2)) { break }
        Start-Sleep -Milliseconds 300
        $now = Get-HomeFocus
        if ($tiles -contains $now) { break }
        $tiles += $now
    }
    Save-Screen $Shot
    Say "${Shot}: $($tiles -join ' | ')"
    , $tiles
}

# What is wrong with the row's tiles $Tiles, walked $When, for Show Play history $On: on, seven games then the Play
# history tile; off, eight games and no tile.
function Test-Row($Tiles, [bool] $On, [string] $When) {
    $games = @($Tiles | Where-Object { $_ -like 'the Recent tile *' })
    $history = @($Tiles | Where-Object { $_ -eq 'the Play history tile' })
    $want = if ($On) { 7 } else { 8 }
    $bad = @()
    if ($games.Count -ne $want) { $bad += "${When}: the Recent row has $($games.Count) games, not $want" }
    if ($On -and ($history.Count -ne 1 -or $Tiles[-1] -ne 'the Play history tile')) { $bad += "${When}: the Recent row does not end in the Play history tile" }
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
    $failures += Test-Row (Get-RowTiles 'row-on') $true 'on'

    # 2. Turned off with the pad, through Customise Home's Settings, from the row's first game.
    Enter-RowStart
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
    $failures += Test-Row (Get-RowTiles 'row-off') $false 'off'

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
    $failures += Test-Row (Get-RowTiles 'row-restarted') $false 'after the restart'

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
