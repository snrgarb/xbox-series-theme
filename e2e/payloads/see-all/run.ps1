# Pages that open over what had focus, with the pad only (#146). First Settings: Menu on the Recent row's first game,
# the Tile menu's Customise Home, A: Settings opens on Theme settings' Personalisation, focus goes into it, and it
# stays there (it doesn't go back to Theme settings' boxes); B, B closes it. Then My games & apps from a tile on Home:
# A on the tile opens the page on the tile's item, it stays there (it doesn't move to Games, then or as focus moves in
# the content), and B goes back to Home, for See all with focus on that tile (from Play history the Recent row takes
# focus on its last focused game). Done for the Recent row's Play history tile (Play history), for See all on the
# first Group, the seed's "My Picks" (Full library, that preset selected), and for See all on the Group under it. The
# seed's library always has presets shown in Fullscreen, so the default Favourites Group, which has no preset and
# shows only without any, isn't covered.
#
# Home is one page that scrolls (#206): the presses take focus down the Recent row, the wide tiles and the Groups, and
# the page comes to rest on each Group whose See all is pressed ("Home rests at Group <name>"). Back from My games &
# apps it must still rest there: nothing new is logged of where it rests from A on See all to focus back on the tile.
#
# Focus on Home moves a press at a time, until the log tells it is on the tile wanted (the Recent row wraps), and A
# is pressed only with the log's last focus line on that tile.
#
# Before the fix, the tile hiding under the page left focus on nothing, Playnite put it on the window's first focusable
# element, the nav's Games item, and focus on a nav item opened it: the log read "shows FullLibrary" then "shows Games".
#
# Judged by the extension's log lines, in the order logged (never by their times: the VM's clock may jump), each
# quoted in steps.txt:
#   Home focus on the Recent tile <game> / Home focus on the Group tile <game>
#   Home focus on the Play history tile
#   Home focus on See all of the Group <name>
#   Home rests at RecentRow / Home rests at WideTiles / Home rests at Group <name>
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
$told = 'Home focus on|Home rests at|My games & apps (shows|closed|nav focus)|Filter preset .* selected|\[Settings\]'

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

# Home's focus by the log's last word on it: "Home focus on ..." as logged, without its time; $null with none yet.
function Get-Focus {
    $last = Get-LogLines 0 'Home focus on ' | Select-Object -Last 1
    if ($last) { $last -replace '^.*(Home focus on )', '$1' }
}
function Test-FocusOn([string] $Pattern) { [bool]((Get-Focus) -match $Pattern) }

# Presses $Button a step at a time until Home's focus is on what $Pattern tells, at most $Max times, never once more
# than needed (a row may wrap). Whether it got there; if not, a screenshot "$Shot" shows where it stopped.
function Step-To([string] $Button, [string] $Pattern, [int] $Max, [string] $Shot) {
    foreach ($n in 0..$Max) {
        if (Test-FocusOn $Pattern) { return $true }
        if ($n -eq $Max) { break }
        $mark = Get-Mark
        Push-Button $Button
        Wait-Line 'Home focus on ' 2 $mark | Out-Null
        Start-Sleep -Milliseconds 300
    }
    Say "focus never reached /$Pattern/ with $Max $Button; it is on: $(Get-Focus)"
    Save-Screen $Shot
    $false
}

# Something is still in front of Home after a case (no focus line since $After): B, up to three times, until Home's
# focus is told again. Throws if it never is: nothing after could be trusted.
function Restore-Home([int] $After, [string] $What) {
    foreach ($n in 1..3) {
        Say "${What}: Home has not taken focus back; pressing B"
        Push-Button B
        if (Wait-Line 'Home focus on ' 3 $After) { Start-Sleep -Milliseconds 800; return }
    }
    Save-Screen 'not-home'
    throw "${What}: could not get back to Home (see not-home.jpg)"
}

# From the tile $What, which must have focus by the log's last focus line (the pattern $FocusLine): A, and what must
# follow. $Item is the item the page must open on and keep; $Preset, the filter preset that must be selected with it,
# if any; with -BackOnTile, B must put focus back on the tile.
function Test-Opens([string] $What, [string] $Item, [string] $FocusLine, [string] $Shot, [string] $Preset, [switch] $BackOnTile) {
    if (-not (Test-FocusOn $FocusLine)) { $script:failures += "${What}: not pressed, focus is elsewhere ($(Get-Focus))"; return }
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

    # B: back to Home; for See all, focus on the tile it left.
    $back = Get-Mark
    Push-Button B
    Wait-Line 'My games & apps closed' 5 $back | Out-Null
    $home_ = Wait-Line 'Home focus on ' 5 $back
    Start-Sleep -Milliseconds 1200
    Save-Screen "$Shot-4-after-b"
    Write-Told $back
    $after = Get-Shown $back
    if ($after -ne 'closed') { $script:failures += "${What}: B did not close the page (it shows $after)" }
    elseif ($BackOnTile -and -not (Test-FocusOn $FocusLine)) { $script:failures += "${What}: B went back to Home, but not to focus on the tile ($(Get-Focus))" }
    if (-not $home_) { Restore-Home $back $What }
}

# Where Home's page rests by the log's last word on it since $After: 'RecentRow', 'WideTiles', 'Group <name>', or $null.
function Get-Rest([int] $After = 0) {
    $last = Get-LogLines $After 'Home rests at ' | Select-Object -Last 1
    if ($last -match 'Home rests at (.+)$') { $Matches[1].Trim() }
}

# See all on $Group, opened and closed by Test-Opens since $After: Home must rest on that Group before and after, and
# have said nothing new of where it rests in between.
function Test-RestKept([string] $Group, [int] $After) {
    $moved = @(Get-LogLines $After 'Home rests at ')
    if ($moved.Count) { $script:failures += "See all on ${Group}: Home's page moved while My games & apps was up or as it closed ($($moved -join '; '))" }
    if ((Get-Rest) -ne "Group $Group") { $script:failures += "See all on ${Group}: Home rests at $(Get-Rest), not Group $Group" }
}

# The Group whose See all tile has focus by the log's last focus line; $null when focus is elsewhere.
function Get-SeeAllGroup {
    if ((Get-Focus) -match 'Home focus on See all of the Group (.*)$') { $Matches[1].Trim() }
}

try {
    Connect-Pad
    Start-Dashboard $PSScriptRoot -Library
    Start-Sleep -Seconds 6  # the "Hi <user>!" toast over Home's foot has gone
    Save-Screen '0-home'

    # Home starts with focus on the top bar: Down to the Recent row's first game.
    if (-not (Step-To Down 'Home focus on the Recent tile ' 2 'no-recent-row')) { throw 'Never reached the Recent row (see no-recent-row.jpg)' }

    # Settings. Menu opens the game's Tile menu, whose fourth row is Customise Home (the menu's rows tell the log
    # nothing, so three Downs, and settings-0-tile-menu.jpg to show where they ended).
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
        $opened = $open -replace '^.*\[Settings\] ', ''
        if ($open -notmatch 'opened on ThemeSettings, page Personalisation$') { $failures += "Customise Home: Settings $opened" }
        $pages = @(Get-LogLines $mark '\[Settings\] page \w+$' | ForEach-Object { $_ -replace '^.*\[Settings\] page ', '' })
        if ($pages.Count) { $failures += "Customise Home: Settings left Personalisation by itself, for the page $($pages -join ', then ')" }
        if (-not (Get-LogLines $mark '\[Settings\] focus in page Personalisation: True$')) { $failures += 'Customise Home: focus did not go into Personalisation' }
        Say "Customise Home: Settings $opened; page changes after: $(if ($pages.Count) { $pages -join ', ' } else { 'none' })"
    }

    # B from Personalisation goes back to Theme settings' boxes, B again closes Settings; if it never opened, B closes
    # the Tile menu. Either way Home's Recent row has focus again after.
    $back = Get-Mark
    foreach ($n in 1..3) {
        Push-Button B
        if (Wait-Line 'Home focus on ' 2 $back) { break }
    }
    Start-Sleep -Milliseconds 800
    Save-Screen 'settings-2-after-b'
    Write-Told $back
    if ($open -and -not (Get-LogLines $back '\[Settings\] closed$')) { $failures += 'B did not close Settings (see settings-2-after-b.jpg)' }
    if (-not (Get-LogLines $back 'Home focus on ')) { Restore-Home $back 'Customise Home' }

    # Play history: right along the Recent row to its last tile.
    if (-not (Test-FocusOn 'Home focus on the Recent tile ')) { $failures += "After Settings, focus is not on the Recent row ($(Get-Focus))" }
    $history = 'Home focus on the Play history tile$'
    if (Step-To Right $history 10 '1-no-play-history-tile') {
        Save-Screen '1-play-history-tile'
        Test-Opens 'The Play history tile' 'PlayHistory' $history 'history'
    } else { $failures += 'Never reached the Play history tile (see 1-no-play-history-tile.jpg)' }

    # See all: down past the four wide tiles to the Groups, then right along the first Group to its last tile.
    if (-not (Step-To Down 'Home focus on (the Group tile |See all of the Group )' 4 '2-no-groups')) { throw 'Never reached the Groups (see 2-no-groups.jpg)' }
    if (-not (Step-To Right 'Home focus on See all of the Group ' 8 '2-no-see-all')) { throw 'Never reached a See all tile (see 2-no-see-all.jpg)' }
    $first = Get-SeeAllGroup
    Save-Screen '2-see-all'
    Say "focus on See all of the Group $first"
    $firstLine = "Home focus on See all of the Group $([regex]::Escape($first))`$"
    $mark = Get-Mark
    Test-Opens "See all on $first" 'FullLibrary' $firstLine 'first' $first -BackOnTile
    Test-RestKept $first $mark

    # The Group under it: one Down, which may keep the place in the row, then right to its See all.
    if (-not (Test-FocusOn $firstLine)) { $failures += "After See all on $first, focus is not back on it ($(Get-Focus)); the second Group is not tried" }
    else {
        $mark = Get-Mark
        Push-Button Down
        Wait-Line 'Home focus on ' 3 $mark | Out-Null
        Start-Sleep -Milliseconds 800
        $other = "Home focus on See all of the Group (?!$([regex]::Escape($first))`$)"
        if ((Test-FocusOn $firstLine) -or -not (Step-To Right $other 8 '3-no-see-all')) { $failures += "Never reached the second Group's See all tile (focus: $(Get-Focus))" }
        else {
            $second = Get-SeeAllGroup
            Save-Screen '3-see-all'
            Say "focus on See all of the Group $second"
            $mark = Get-Mark
            Test-Opens "See all on $second" 'FullLibrary' "Home focus on See all of the Group $([regex]::Escape($second))`$" 'second' $second -BackOnTile
            Test-RestKept $second $mark
        }
    }

    if (-not (Get-Process Playnite.FullscreenApp -ErrorAction SilentlyContinue)) { $failures += 'Playnite exited' }
    $errors = @(([regex]'(?m)^.*\|ERROR\|.*$').Matches((Get-PlayniteLog)) | ForEach-Object Value)
    $errors | ForEach-Object { Say "logged: $_" }  # told, not judged: this test is about what opens where

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
