# Pages that open over what had focus, with the pad only (#146). First Settings: Menu on the Recent row's first game,
# the Tile menu's Customise Home, A: Settings opens on Theme settings' Personalisation, focus goes into it, and it
# stays there (it doesn't go back to Theme settings' boxes); B, B closes it. Then My games & apps from a tile on Home: A on the tile opens the page on the tile's
# item, it stays there (it doesn't move to Games, then or as focus moves in the content), and B goes back to Home, for
# See all with focus on that tile (from Play history the Recent row takes focus on its last focused game). Done for the Recent row's Play history tile (Play history), for See all on the first Group, the
# seed's "My Picks" (Full library, that preset selected), and for See all on the Group under it. The seed's library
# always has presets shown in Fullscreen, so the default Favourites Group, which has no preset and shows only without
# any, isn't covered.
#
# Before the fix, the tile hiding under the page left focus on nothing, Playnite put it on the window's first focusable
# element, the nav's Games item, and focus on a nav item opened it: the log read "shows FullLibrary" then "shows Games".
#
# Judged by the extension's log lines, in the order logged (never by their times: the VM's clock may jump), each
# quoted in steps.txt:
#   Home focus on the Play history tile
#   Home focus on See all of the Group <name>
#   My games & apps shows <item> / My games & apps closed
#   Filter preset <name> selected
#   My games & apps nav focus on <item>, showing <item>
#   My games & apps nav focus on <item> from nothing, not a move along the nav: still showing <item>
#   [Settings] opened on <category>, page <page> / [Settings] page <page> / [Settings] closed
#   [Settings] focus in page <page>: True
#   [Settings] rail focus on <category> from nothing, not a move along the rail
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()
$told = 'Home focus on|My games & apps (shows|closed|nav focus)|Filter preset .* selected|\[Settings\]'

# The extension's own log only, so a mark in it stays where it was as Playnite's log grows.
function Get-ExtensionLog { @(Get-Content (Join-Path $Data 'extensions.log') -ErrorAction SilentlyContinue) }
function Get-Mark { (Get-ExtensionLog).Count }
function Get-LogLines([int] $After, [string] $Pattern) {
    @(Get-ExtensionLog | Select-Object -Skip $After | Where-Object { $_ -match $Pattern } | ForEach-Object { $_.Trim() })
}

# Waits up to $Seconds (by a stopwatch, not the clock) for a line matching $Pattern after $After; the line, or $null.
function Wait-Line([string] $Pattern, [int] $Seconds, [int] $After) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    do {
        $hit = Get-LogLines $After $Pattern | Select-Object -First 1
        if ($hit) { return $hit }
        Start-Sleep -Milliseconds 250
    } while ($watch.Elapsed.TotalSeconds -lt $Seconds)
    $null
}

# What the log told since $After, into steps.txt.
function Write-Told([int] $After) { Get-LogLines $After $told | ForEach-Object { Say "  log: $_" } }

# Every item the page showed since $After, in order: 'FullLibrary', 'Games'...; always an array.
function Get-Opened([int] $After) {
    , @(Get-LogLines $After 'My games & apps shows \w+' | ForEach-Object { if ($_ -match 'My games & apps shows (\w+)') { $Matches[1] } })
}

# What the page shows by the log's last word on it since $After: an item, 'closed', or $null with no word.
function Get-Shown([int] $After) {
    $last = Get-LogLines $After 'My games & apps (shows \w+|closed)' | Select-Object -Last 1
    if ($last -match 'My games & apps shows (\w+)') { $Matches[1] } elseif ($last) { 'closed' } else { $null }
}

# From the tile $What with focus: A, and what must follow. $Item is the item the page must open on and keep; $HomeLine,
# a pattern for the log line that says focus is back on the tile after B, if it must be; $Preset, the filter preset
# that must be selected with the item, if any.
function Test-Opens([string] $What, [string] $Item, [string] $HomeLine, [string] $Shot, [string] $Preset) {
    $mark = Get-Mark
    Push-Button A
    Wait-Line 'My games & apps shows \w+' 5 $mark | Out-Null
    Start-Sleep -Milliseconds 1500
    Save-Screen "$Shot-1-after-a"
    Start-Sleep -Seconds 3
    Save-Screen "$Shot-2-settled"
    Write-Told $mark
    $opened = Get-Opened $mark
    $shown = Get-Shown $mark
    Say "$What opened: $(if ($opened.Count) { $opened -join ', then ' } else { 'nothing' }); 4.5 s later the page shows $shown"
    if (-not $opened.Count) { $script:failures += "$What opened nothing"; return }
    if ($opened[0] -ne $Item) { $script:failures += "$What opened $($opened[0]), not $Item" }
    if ($shown -ne $Item) { $script:failures += "${What}: the page moved to $shown by itself, from $Item" }
    if ($Preset) {
        $selected = Get-LogLines $mark 'Filter preset .* selected$' | Select-Object -Last 1
        if ($selected -notmatch "Filter preset $([regex]::Escape($Preset)) selected`$") { $script:failures += "${What}: the preset $Preset was not selected ($selected)" }
    }

    # Moving in the content keeps the page on its item.
    $move = Get-Mark
    Push-Button Right; Start-Sleep -Milliseconds 800
    Push-Button Down; Start-Sleep -Milliseconds 1500
    Save-Screen "$Shot-3-after-right-down"
    Write-Told $move
    $moved = Get-Shown $move
    if ($moved) {
        Say "after Right and Down in the content the page shows $moved"
        $script:failures += "${What}: Right and Down in the content moved the page to $moved"
    }

    # B: back to Home, focus on the tile it left where that is told.
    $back = Get-Mark
    Push-Button B
    Wait-Line 'My games & apps closed' 5 $back | Out-Null
    $home_ = if ($HomeLine) { Wait-Line $HomeLine 5 $back } else { $true }
    Start-Sleep -Milliseconds 1200
    Save-Screen "$Shot-4-after-b"
    Write-Told $back
    $after = Get-Shown $back
    if ($after -ne 'closed') { $script:failures += "${What}: B did not close the page (it shows $after)" }
    elseif (-not $home_) { $script:failures += "${What}: B went back to Home, but not to focus on the tile" }
}

# The Group whose See all tile has focus by the log's last word since $After.
function Get-SeeAllGroup([int] $After) {
    $last = Get-LogLines $After 'Home focus on See all of the Group' | Select-Object -Last 1
    if ($last -match 'Home focus on See all of the Group (.*)$') { $Matches[1].Trim() }
}

try {
    Connect-Pad
    Start-Dashboard $PSScriptRoot -Library
    Start-Sleep -Seconds 6  # the "Hi <user>!" toast over Home's foot has gone
    Save-Screen '0-home'

    # Down from the top bar to the Recent row's first game; Menu opens its Tile menu, whose fourth row is Customise Home.
    Push-Button Down
    Start-Sleep -Milliseconds 1500
    $mark = Get-Mark
    Push-Button Start
    Start-Sleep -Milliseconds 1500
    foreach ($n in 1..3) { Push-Button Down; Start-Sleep -Milliseconds 500 }
    Save-Screen 'settings-0-tile-menu'
    Push-Button A
    $open = Wait-Line '\[Settings\] opened on ' 5 $mark
    Start-Sleep -Seconds 3
    Save-Screen 'settings-1-personalisation'
    Write-Told $mark
    if (-not $open) { $failures += 'Customise Home did not open Settings (see settings-0-tile-menu.jpg)' }
    else {
        if ($open -notmatch 'opened on ThemeSettings, page Personalisation$') { $failures += "Customise Home: Settings $($open -replace '^.*\[Settings\] ', '')" }
        $pages = @(Get-LogLines $mark '\[Settings\] page \w+$' | ForEach-Object { $_ -replace '^.*\[Settings\] page ', '' })
        if ($pages.Count) { $failures += "Customise Home: Settings left Personalisation by itself, for the page $($pages -join ', then ')" }
        if (-not (Get-LogLines $mark '\[Settings\] focus in page Personalisation: True$')) { $failures += 'Customise Home: focus did not go into Personalisation' }
        Say "Customise Home: Settings $($open -replace '^.*\[Settings\] ', ''); page changes after: $(if ($pages.Count) { $pages -join ', ' } else { 'none' })"
    }

    # B from Personalisation goes back to Theme settings' boxes, B again closes Settings.
    $back = Get-Mark
    foreach ($n in 1..3) {
        if (Get-LogLines $back '\[Settings\] closed$') { break }
        Push-Button B
        Start-Sleep -Milliseconds 1500
    }
    Save-Screen 'settings-2-after-b'
    Write-Told $back
    if ($open -and -not (Get-LogLines $back '\[Settings\] closed$')) { $failures += 'B did not close Settings (see settings-2-after-b.jpg)' }

    # Back on the Recent row: right along its seven games to its last tile, Play history.
    $mark = Get-Mark
    foreach ($n in 1..8) { Push-Button Right; Start-Sleep -Milliseconds 500 }
    Save-Screen '1-play-history-tile'
    Write-Told $mark
    if (Get-LogLines $mark 'Home focus on the Play history tile$') {
        Test-Opens 'The Play history tile' 'PlayHistory' '' 'history'
    } else { $failures += 'Never reached the Play history tile (see 1-play-history-tile.jpg)' }

    # Down past the four wide tiles to the Groups, then right along the first one, to its last tile: See all. A Group
    # holds 6 games at most.
    $mark = Get-Mark
    $first = $null
    foreach ($try in 1..3) {
        Push-Button Down
        Start-Sleep -Milliseconds 1500
        foreach ($n in 1..7) { Push-Button Right; Start-Sleep -Milliseconds 500 }
        $first = Get-SeeAllGroup $mark
        if ($first) { break }
        Say "no See all tile has focus after $try Down and 7 Right"
    }
    Save-Screen '2-see-all'
    Write-Told $mark
    if (-not $first) { throw 'Never reached a See all tile on Home (see 2-see-all.jpg)' }
    Say "focus on See all of the Group $first"
    Test-Opens "See all on $first" 'FullLibrary' "Home focus on See all of the Group $([regex]::Escape($first))`$" 'first' $first

    # The Group under it: Down keeps the place in the row, so its See all tile, or its last game if it is shorter.
    $mark = Get-Mark
    Push-Button Down
    Start-Sleep -Milliseconds 1500
    foreach ($n in 1..7) { Push-Button Right; Start-Sleep -Milliseconds 500 }
    $second = Get-SeeAllGroup $mark
    Save-Screen '3-see-all'
    Write-Told $mark
    if (-not $second -or $second -eq $first) { $failures += "Never reached the second Group's See all tile (see 3-see-all.jpg)" }
    else {
        Say "focus on See all of the Group $second"
        Test-Opens "See all on $second" 'FullLibrary' "Home focus on See all of the Group $([regex]::Escape($second))`$" 'second' $second
    }

    if (-not (Get-Process Playnite.FullscreenApp -ErrorAction SilentlyContinue)) { $failures += 'Playnite exited' }
    $errors = @(([regex]'(?m)^.*\|ERROR\|.*$').Matches((Get-PlayniteLog)) | ForEach-Object Value)
    $errors | ForEach-Object { Say "logged: $_" }  # told, not judged: this test is about what the tiles open

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
