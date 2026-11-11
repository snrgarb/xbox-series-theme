# The Guide turned off: Settings > Theme settings > Guide's checkbox, Use the Guide. One run shows the Guide button
# opening the Guide while it is on, turns it off with the pad, and shows from Playnite's log that neither a tap nor a
# hold opens anything then, at once and after a restart of Playnite. The library is the seed's. What the logs say:
#   [Settings] Guide is on|off           as the extension starts
#   [Settings] Guide turned on|off       as A turns it over
#   Guide opened over <process>; ...     the Overlay took the foreground: the Guide, or Power or a prompt on their own
#
# 1. Untouched: "Guide is on", and a tap of the Guide button opens the Guide, a second closing it.
# 2. Turned off with the pad, through the Settings page: Menu on the focused tile, Customise Home (the fourth row of a
#    seed game's tile menu), which opens Settings; then to the Guide page's checkbox a press at a time, each chosen by
#    where the extension's "[Settings]" lines say Settings is and checked to have moved it; A, which must log "Guide
#    turned off", once; then B until Settings closes.
# 3. Off, with no restart: a tap, then a 1.5 s hold, open nothing: no "Guide opened over" line, the Overlay not in front.
# 4. Playnite stopped and started again: "Guide is off", and a tap opens nothing.
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

# Where Settings is, by its "[Settings]" lines after line $After of the log, as "Guide/GuideOnBox".
function Get-SettingsPlace([int] $After) {
    $page = $null; $focus = $null
    foreach ($line in Get-SettingsLines $After) {
        if ($line -match '\[Settings\] opened on \w+, page (\w+)') { $page = $Matches[1] }
        elseif ($line -match '\[Settings\] page (\w+)') { $page = $Matches[1] }
        elseif ($line -match '\[Settings\] focus on (\w+)') { $focus = $Matches[1] }
    }
    "$page/$focus"
}

# Takes Settings, wherever it opened, to the Guide page's checkbox, a press at a time, each chosen by where the log says
# Settings is, and each having to move it. Theme settings' boxes are two wide: Personalisation and Idle, then Guide and
# Discord. Throws when the log doesn't say where Settings is, or a press changes nothing.
function Move-ToGuideCheckbox([int] $After) {
    $press = @{
        'None/ThemeSettingsItem'    = 'Right'   # the rail: into Theme settings' boxes, on Personalisation
        'None/PlayniteSettingsItem' = 'Up'
        'None/MorePlayniteOptionsItem' = 'Up'
        'None/PersonalisationBox'   = 'Down'    # to Guide
        'None/IdleBox'              = 'Left'
        'None/DiscordBox'           = 'Left'
        'None/GuideBox'             = 'A'       # opens the Guide page, focus on its checkbox
    }
    foreach ($i in 1..12) {
        $place = Get-SettingsPlace $After
        if ($place -eq 'Guide/GuideOnBox') { return }
        $button = $press[$place]
        # On any of Personalisation's boxes, B steps back to Theme settings' boxes, on Personalisation.
        if (-not $button -and $place -like 'Personalisation/*') { $button = 'B' }
        if (-not $button) { Save-Screen 'settings-lost'; throw "Settings is at $place, from where the test knows no way to the Guide checkbox" }
        Say "Settings is at ${place}: $button"
        Push-Button $button
        $waited = [Diagnostics.Stopwatch]::StartNew()
        while ((Get-SettingsPlace $After) -eq $place -and $waited.ElapsedMilliseconds -lt 3000) { Start-Sleep -Milliseconds 200 }
        if ((Get-SettingsPlace $After) -eq $place) { Save-Screen 'settings-stuck'; throw "$button at $place changed nothing" }
        Start-Sleep -Milliseconds 800
    }
    Save-Screen 'settings-lost'
    throw "Settings is at $(Get-SettingsPlace $After) after 12 presses, not on the Guide checkbox"
}

# What is wrong when the Guide button, pressed for $HoldMs, opened something with the Guide off: a "Guide opened
# over" line within 3 s, or the Overlay in front; alone, what Say writes on the way going to the host.
function Test-NothingOpens([string] $What, [int] $HoldMs) {
    $mark = @(Get-ExtensionLog).Count
    Push-Button Guide $HoldMs | Out-Host
    $opened = if (Wait-LogLine 'Guide opened over' $mark 3) { @(Get-LogLines 'Guide opened over' $mark)[0] } else { $null }
    Save-Screen $What | Out-Host
    $front = Get-Foreground
    Say "${What}: $(if ($opened) { $opened.Trim() } else { 'no Guide opened line' }); foreground $($front.Process) '$($front.Title)'" | Out-Host
    $bad = @()
    if ($opened) { $bad += "${What}: the Overlay opened with the Guide off: $($opened.Trim())" }
    if ($front.Title -eq 'Xbox Series Guide') { $bad += "${What}: the Overlay is in front with the Guide off" }
    if ($bad.Count) { Push-Button B | Out-Host; Start-Sleep -Milliseconds 800 }
    ,$bad
}

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

    # 1. Untouched: on, and a tap opens the Guide.
    $said = @(Get-LogLines '\[Settings\] Guide' 0)
    $said | ForEach-Object { Say "logged: $_" }
    if (-not @($said | Where-Object { $_ -match '\[Settings\] Guide is on' }).Count) {
        throw 'The Guide is not on at the start (no "[Settings] Guide is on" line): is this a build with the setting?'
    }
    $mark = @(Get-ExtensionLog).Count
    Push-Button Guide
    $opened = if (Wait-LogLine 'Guide opened over' $mark 5) { @(Get-LogLines 'Guide opened over' $mark)[0] } else { $null }
    Start-Sleep -Milliseconds 900
    Save-Screen 'guide-open'
    Say "with the Guide on, a tap: $(if ($opened) { $opened.Trim() } else { 'no Guide opened line' })"
    if (-not $opened) { throw 'With the Guide on, a tap did not open it' }
    Push-Button Guide
    Start-Sleep -Milliseconds 1500
    Save-Screen 'guide-closed'
    if ((Get-Foreground).Title -eq 'Xbox Series Guide') { throw 'A second tap did not close the Guide' }

    # Focus on a Recent-row tile for its menu: with none focused yet, Down puts it on the row's first game.
    if (-not @(Get-LogLines '\[Hero timing\] focus on' 0).Count) {
        Push-Button Down
        Start-Sleep -Seconds 2
    }
    if (-not @(Get-LogLines '\[Hero timing\] focus on' 0).Count) { throw 'No Recent-row tile has focus on Home' }

    # 2. Turned off with the pad, through Customise Home's Settings.
    $mark = @(Get-ExtensionLog).Count
    Push-Button Start
    Start-Sleep -Milliseconds 1200
    Save-Screen 'tile-menu'
    foreach ($i in 1..3) { Push-Button Down; Start-Sleep -Milliseconds 400 }
    Push-Button A
    if (-not (Wait-LogLine '\[Settings\] opened on ThemeSettings' $mark)) {
        Save-Screen 'settings-not-opened'
        Get-SettingsLines $mark | ForEach-Object { Say "logged: $_" }
        throw 'Customise Home did not open Settings (Menu, Down x3, A on the focused tile)'
    }
    Start-Sleep -Milliseconds 1500
    Say "Settings opened, at $(Get-SettingsPlace $mark)"
    try { Move-ToGuideCheckbox $mark } finally { Get-SettingsLines $mark | ForEach-Object { Say "logged: $_" } }
    Save-Screen 'guide-checkbox-focused'
    $toggle = @(Get-ExtensionLog).Count
    Push-Button A
    if (-not (Wait-LogLine '\[Settings\] Guide turned off' $toggle)) {
        Save-Screen 'guide-not-turned-off'
        throw 'A on the Guide checkbox did not turn the Guide off'
    }
    Start-Sleep -Milliseconds 500
    Save-Screen 'guide-checkbox-off'
    $closed = $false
    foreach ($i in 1..4) {
        Push-Button B
        if (Wait-LogLine '\[Settings\] closed' $mark 2) { $closed = $true; break }
    }
    if (-not $closed) { Save-Screen 'settings-not-closed'; throw 'Settings did not close after B four times' }
    $toggles = @(Get-LogLines '\[Settings\] Guide turned' $mark)
    if ($toggles.Count -ne 1) { throw "The Guide was turned over $($toggles.Count) times, not once" }
    Start-Sleep -Seconds 2

    # 3. Off, with no restart: a tap and a hold open nothing.
    $failures += Test-NothingOpens 'off-tap' 120
    $failures += Test-NothingOpens 'off-hold' 1500

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
    Start-Sleep -Seconds 4
    Save-Screen 'restarted-home'
    $said = @(Get-LogLines '\[Settings\] Guide' 0)
    $said | ForEach-Object { Say "logged: $_" }
    if (-not @($said | Where-Object { $_ -match '\[Settings\] Guide is off' }).Count) {
        $failures += 'The Guide is not off after the restart (no "[Settings] Guide is off" line): it was not saved'
    }
    $failures += Test-NothingOpens 'restarted-tap' 120

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
