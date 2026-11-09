# See all at the end of a Group on Home, with the pad only (#146): A on it opens My games & apps on Full library with
# the Group's filter preset selected, it stays there (it doesn't move to Games, then or as focus moves in the grid),
# and B goes back to Home with focus on that See all tile. Done for the first Group (the seed's "My Picks") and for
# the Group under it. The seed's library always has presets shown in Fullscreen, so the default Favourites Group,
# which has no preset and shows only without any, isn't covered.
#
# Judged by the extension's log lines, each quoted in steps.txt with its own time:
#   Home focus on See all of the Group <name>
#   My games & apps shows <item> / My games & apps closed
#   Filter preset <name> selected
#   My games & apps nav focus on <item>, showing <item>
# and, while the build logs them, the "DIAG #146" lines (every keyboard focus move in the window, with what took
# focus), first lines only; the whole of them, stack traces too, are in extensions.log.
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()
$told = 'Home focus on See all|My games & apps (shows|closed|nav focus)|Filter preset .* selected|DIAG #146'

# The extension's own log only, so a mark in it stays where it was as Playnite's log grows.
function Get-ExtensionLog { @(Get-Content (Join-Path $Data 'extensions.log') -ErrorAction SilentlyContinue) }
function Get-Mark { (Get-ExtensionLog).Count }
function Get-LogLines([int] $After, [string] $Pattern) {
    @(Get-ExtensionLog | Select-Object -Skip $After | Where-Object { $_ -match $Pattern } | ForEach-Object { $_.Trim() })
}

# Waits up to $Seconds for a line matching $Pattern after $After; the line, or $null.
function Wait-Line([string] $Pattern, [int] $Seconds, [int] $After) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        $hit = Get-LogLines $After $Pattern | Select-Object -First 1
        if ($hit) { return $hit }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    $null
}

# What the log told since $After, into steps.txt.
function Write-Told([int] $After) { Get-LogLines $After $told | ForEach-Object { Say "  log: $_" } }

# The item the page shows by the log's last word on it since $After: 'FullLibrary', 'Games'..., 'closed', or $null.
function Get-Shown([int] $After) {
    $last = Get-LogLines $After 'My games & apps (shows \w+|closed)' | Select-Object -Last 1
    if ($last -match 'My games & apps shows (\w+)') { $Matches[1] } elseif ($last) { 'closed' } else { $null }
}

# From a See all tile with focus, the Group named: A, and what must follow.
function Test-SeeAll([string] $Group, [string] $Shot) {
    $mark = Get-Mark
    Push-Button A
    Wait-Line 'My games & apps shows \w+' 5 $mark | Out-Null
    Start-Sleep -Milliseconds 1500
    Save-Screen "$Shot-1-after-a"
    Start-Sleep -Seconds 3
    Save-Screen "$Shot-2-settled"
    Write-Told $mark
    $opened = Get-LogLines $mark 'My games & apps shows \w+' | ForEach-Object { $_ -replace '^.*shows ', '' }
    $shown = Get-Shown $mark
    Say "See all on $Group opened: $(if ($opened) { $opened -join ', then ' } else { 'nothing' }); 4.5 s later the page shows $shown"
    if (-not $opened) { $script:failures += "See all on $Group opened nothing" }
    elseif ($opened[0] -ne 'FullLibrary') { $script:failures += "See all on $Group opened $($opened[0]), not Full library" }
    if ($opened -and $shown -ne 'FullLibrary') { $script:failures += "See all on ${Group}: the page moved from Full library to $shown by itself" }
    $preset = Get-LogLines $mark 'Filter preset (.*) selected' | Select-Object -Last 1
    if ($preset -notmatch "Filter preset $([regex]::Escape($Group)) selected`$") { $script:failures += "See all on ${Group}: its preset was not selected ($preset)" }

    # Moving in the grid keeps the page on Full library.
    $move = Get-Mark
    Push-Button Right; Start-Sleep -Milliseconds 800
    Push-Button Down; Start-Sleep -Milliseconds 1500
    Save-Screen "$Shot-3-after-right-down"
    Write-Told $move
    $moved = Get-Shown $move
    if ($moved) {
        Say "after Right and Down in the grid the page shows $moved"
        $script:failures += "See all on ${Group}: Right and Down in the grid moved the page to $moved"
    }

    # B: back to Home, focus on the See all tile it left.
    $back = Get-Mark
    Push-Button B
    $home_ = Wait-Line "Home focus on See all of the Group $([regex]::Escape($Group))`$" 5 $back
    Start-Sleep -Milliseconds 1200
    Save-Screen "$Shot-4-after-b"
    Write-Told $back
    if ((Get-Shown $back) -ne 'closed') { $script:failures += "See all on ${Group}: B did not close the page (it shows $(Get-Shown $back))" }
    elseif (-not $home_) { $script:failures += "See all on ${Group}: B went back to Home, but not to focus on its See all tile" }
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

    # Down to the Groups, then right along the first one, to its last tile: See all. A Group holds 6 games at most.
    $mark = Get-Mark
    $first = $null
    foreach ($try in 1..3) {
        Push-Button Down
        Start-Sleep -Milliseconds 1500
        foreach ($n in 1..7) { Push-Button Right; Start-Sleep -Milliseconds 400 }
        $first = Get-SeeAllGroup $mark
        if ($first) { break }
        Say "no See all tile has focus after $try Down and 7 Right"
    }
    Save-Screen '1-see-all'
    Write-Told $mark
    if (-not $first) { throw 'Never reached a See all tile on Home (see 1-see-all.jpg)' }
    Say "focus on See all of the Group $first"
    Test-SeeAll $first 'first'

    # The Group under it: Down keeps the place in the row, so its See all tile, or its last game if it is shorter.
    $mark = Get-Mark
    Push-Button Down
    Start-Sleep -Milliseconds 1500
    foreach ($n in 1..7) { Push-Button Right; Start-Sleep -Milliseconds 400 }
    $second = Get-SeeAllGroup $mark
    Save-Screen '2-see-all'
    Write-Told $mark
    if (-not $second -or $second -eq $first) { $failures += "Never reached the second Group's See all tile (see 2-see-all.jpg)" }
    else {
        Say "focus on See all of the Group $second"
        Test-SeeAll $second 'second'
    }

    if (-not (Get-Process Playnite.FullscreenApp -ErrorAction SilentlyContinue)) { $failures += 'Playnite exited' }
    $errors = @(([regex]'(?m)^.*\|ERROR\|.*$').Matches((Get-PlayniteLog)) | ForEach-Object Value)
    $errors | ForEach-Object { Say "logged: $_" }  # told, not judged: this test is about See all

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
