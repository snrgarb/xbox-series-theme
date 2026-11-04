# The Guide button over a running game: a virtual pad's Guide tap opens the Guide over the game in front, and a second
# tap closes it, the game getting the foreground back. The game is a stand-in, a full-screen window of its own process.
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()
$game = $null
try {
    # Connected before Playnite starts, as a pad already on is.
    Connect-Pad
    Start-Dashboard $PSScriptRoot
    Start-Sleep -Seconds 3
    Save-Screen 'home'

    $game = Start-StandInGame
    Start-Sleep -Seconds 1
    Save-Screen 'game'

    # Open: the extension says over what, and the Overlay is the window in front.
    $mark = Get-PlayniteLogLength
    Push-Button Guide
    $opened = Wait-PlayniteLog 'Guide opened over' 5 -After $mark
    Start-Sleep -Milliseconds 900
    Save-Screen 'guide-open'
    $front = Get-Foreground
    Say "after the first tap: $(if ($opened) { $opened.Trim() } else { 'no Guide opened line' }); foreground $($front.Process) '$($front.Title)'"
    if (-not $opened) { $failures += 'the Guide did not open' }
    elseif ($opened -notmatch 'Guide opened over powershell') { $failures += "the Guide opened over something else: $opened" }
    if ($front.Title -ne 'Xbox Series Guide') { $failures += "the Guide is not in front: $($front.Process) '$($front.Title)'" }

    # Close: the game is back in front.
    Push-Button Guide
    Start-Sleep -Milliseconds 1200
    Save-Screen 'guide-closed'
    $front = Get-Foreground
    Say "after the second tap: foreground $($front.Process) '$($front.Title)'"
    if ($front.Title -ne 'Stand-in game') { $failures += "the game did not get the foreground back: $($front.Process) '$($front.Title)'" }

    if ($game.HasExited) { $failures += 'the stand-in game exited' }
    if ((Get-Process -Id (Get-Process Playnite.FullscreenApp).Id -ErrorAction SilentlyContinue) -eq $null) { $failures += 'Playnite exited' }

    if ($failures.Count -eq 0) { $result = 'PASS' }
} catch {
    $failures += "error: $_"
    Save-Screen 'error'
} finally {
    $failures | ForEach-Object { Say "failed: $_" }
    Say "result: $result"
    Save-PlayniteFiles
    if ($game -and -not $game.HasExited) { Stop-Process -Id $game.Id -Force }
    Stop-Playnite
    Disconnect-Pad
}
exit $(if ($result -eq 'PASS') { 0 } else { 1 })
