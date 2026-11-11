# Home with a library: Playnite's library seeded before it starts (Start-Dashboard -Library), and Home as one page
# that scrolls (#206), down from the Recent row through the Groups and back. Passes when Playnite is still up, logged no
# error, the extension logged each resting point in the order the presses lead to, and the library read back after it
# stops holds the seed's fourteen games, each with its pictures, and "My Picks" as the first preset, shown in
# Fullscreen, selecting its six games.
#
# The presses, a press at a time, each waiting for the extension's "Home rests at ..." line it must lead to:
#   Down     from the profile, where Home starts, onto the Recent row       Home rests at RecentRow
#   Down     onto the wide tiles                                            Home rests at WideTiles
#   Down     into the Groups, on the first, My Picks                        Home rests at Group My Picks
#   Down     onto the second Group                                          Home rests at Group <another>
#   Up, Up   back to My Picks, then the wide tiles                          Group My Picks, WideTiles
#   Up       back onto the Recent row                                       Home rests at RecentRow
#   Down x2  to the wide tiles, then My Picks again                         WideTiles, Group My Picks
#   B        back to the Recent row's last game                             RecentRow
#
# The screenshots, judged by eye (the 300 ms slide is over by each):
#   home.jpg, recent-row.jpg   Home as it always was, the top bar over the hero's gap
#   wide-tiles.jpg             the page risen: the first Group's header and the top third of its cards under the wide
#                              tiles, the top bar where it was
#   group-1.jpg, group-2.jpg   a Group's header 64 from the screen's top, the next Group whole, part of a third cut by
#                              the screen's foot, the top bar gone
#   up-wide-tiles.jpg, up-recent-row.jpg   Up back out of the Groups: the top bar back
#   b-recent-row.jpg           B from the Groups: Home as it always was, focus on the game the Recent row last had
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()
$picks = 'Amber Circuit', 'Dune Signal', 'Ember Vale', 'Hollow Peak', 'Iron Tide', 'Lumen Drift'

function Get-Mark { @(Get-ExtensionLog).Count }

# The resting points the extension logged after line $After: 'RecentRow', 'WideTiles', 'Group My Picks'...
function Get-Rests([int] $After) {
    , @(Get-ExtensionLog | Select-Object -Skip $After | ForEach-Object { if ($_ -match 'Home rests at (.+?)\s*$') { $Matches[1] } })
}

# Presses $Button, and waits up to 3 s for the next resting point; then the slide's end. The resting point it led to
# ($null if none was logged), with a screenshot $Shot if named. A press that leads elsewhere than $Expected (a pattern) fails.
function Step-Rest([string] $Button, [string] $Expected, [string] $Shot) {
    $mark = Get-Mark
    Push-Button $Button
    $watch = [Diagnostics.Stopwatch]::StartNew()
    do {
        $rests = Get-Rests $mark
        if ($rests.Count) { break }
        Start-Sleep -Milliseconds 200
    } while ($watch.Elapsed.TotalSeconds -lt 3)
    Start-Sleep -Milliseconds 800
    if ($Shot) { Save-Screen $Shot }
    $rests = Get-Rests $mark
    $rest = if ($rests.Count) { $rests[-1] } else { $null }
    Say "${Button}: Home rests at $(if ($rest) { $rest } else { 'nothing new' })$(if ($rests.Count -gt 1) { " (by way of $($rests[0..($rests.Count - 2)] -join ', '))" })"
    if ($rests.Count -ne 1 -or $rest -notmatch "^(?:$Expected)`$") {
        $script:failures += "${Button}: Home rested at $(if ($rests.Count) { $rests -join ', then ' } else { 'nothing new' }), not $Expected"
    }
    $rest
}

try {
    Connect-Pad
    Start-Dashboard $PSScriptRoot -Library
    Start-Sleep -Seconds 6  # the "Hi <user>!" toast over Home's foot has gone
    Save-Screen 'home'
    $start = Get-Mark
    $before = Get-Rests 0
    Say "before any press, Home rests at: $(if ($before.Count) { $before -join ', ' } else { 'nothing logged' })"

    # Focus starts on the profile, with Home resting on the Recent row; if the row already took focus as Home came up,
    # it said so then, and Down goes on to the wide tiles. Either way the first Down ends on the Recent row or past it.
    if ($before.Count -and $before[-1] -eq 'RecentRow') {
        Say 'the Recent row already has focus: no Down onto it'
        Save-Screen 'recent-row'
    } else {
        Step-Rest Down 'RecentRow' 'recent-row' | Out-Null
    }
    Step-Rest Down 'WideTiles' 'wide-tiles' | Out-Null
    Step-Rest Down 'Group My Picks' 'group-1' | Out-Null
    $second = Step-Rest Down 'Group (?!My Picks$).+' 'group-2'
    Say "the second Group: $second"
    Step-Rest Up 'Group My Picks' | Out-Null
    Step-Rest Up 'WideTiles' 'up-wide-tiles' | Out-Null
    Step-Rest Up 'RecentRow' 'up-recent-row' | Out-Null
    $lastGame = (@(Get-ExtensionLog | Where-Object { $_ -match 'Home focus on the Recent tile (.+?)\s*$' }) | Select-Object -Last 1) -replace '^.*Home focus on the Recent tile ', ''
    Step-Rest Down 'WideTiles' | Out-Null
    Step-Rest Down 'Group My Picks' | Out-Null
    $mark = Get-Mark
    Step-Rest B 'RecentRow' 'b-recent-row' | Out-Null
    $focus = @(Get-ExtensionLog | Select-Object -Skip $mark | Where-Object { $_ -match 'Home focus on ' }) | Select-Object -Last 1
    Say "after B: $($focus -replace '^.*(Home focus on )', '$1')"
    if ($focus -notmatch "Home focus on the Recent tile $([regex]::Escape($lastGame.Trim()))\s*`$") {
        $failures += "B on the Groups did not put focus back on the Recent row's last game, $($lastGame.Trim()) (focus: $focus)"
    }
    Say "every resting point since the first press: $((Get-Rests $start) -join ', ')"

    if (-not (Get-Process Playnite.FullscreenApp -ErrorAction SilentlyContinue)) { $failures += 'Playnite exited' }
    $errors = @(([regex]'(?m)^.*\|ERROR\|.*$').Matches((Get-PlayniteLog)) | ForEach-Object Value)
    $errors | ForEach-Object { Say "logged: $_" }
    if ($errors.Count) { $failures += "$($errors.Count) errors logged" }

    # The library as Playnite left it.
    Save-PlayniteFiles
    Stop-Playnite
    Start-Sleep -Seconds 2
    $library = Get-Library
    ConvertTo-Json $library -Depth 4 | Set-Content (Join-Path $Out 'library-after.json') -Encoding UTF8
    $installed = @($library.Games | Where-Object IsInstalled).Count
    $favourites = @($library.Games | Where-Object Favorite).Count
    $pictures = @($library.Games | Where-Object { [IO.File]::Exists($_.CoverImage) -and [IO.File]::Exists($_.BackgroundImage) }).Count
    Say "library after Playnite: $($library.Games.Count) games, $installed installed, $favourites favourites, $pictures with both pictures"
    Write-LibrarySteps $library
    if ($library.Games.Count -ne 14) { $failures += "the library holds $($library.Games.Count) games, not 14" }
    if ($installed -ne 8) { $failures += "$installed games are installed, not 8" }
    if ($favourites -ne 6) { $failures += "$favourites games are favourites, not 6" }
    if ($pictures -ne 14) { $failures += "only $pictures games have a cover and a background" }
    $preset = $library.Presets | Select-Object -First 1
    if (-not $preset -or $preset.Name -ne 'My Picks' -or -not $preset.ShowInFullscreen) {
        $failures += 'My Picks is not the first preset, shown in Fullscreen'
    } elseif (($preset.Games -join ', ') -ne ($picks -join ', ')) { $failures += "My Picks selects $($preset.Games -join ', ')" }

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
