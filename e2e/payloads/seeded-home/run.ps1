# Home with a library: Playnite's library seeded before it starts (Start-Dashboard -Library), a screenshot of Home,
# then one after each of four presses of Down, through the Recent row to the Groups. Passes when Playnite is still up,
# logged no error, and the library read back after it stops holds the seed's fourteen games, each with its pictures,
# and "My Picks" as the first preset, shown in Fullscreen, selecting its six games. Nothing Playnite logs says what
# Home shows: look at home.jpg and down-1.jpg to down-4.jpg for the seed's tiles and the My Picks Group.
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()
$picks = 'Amber Circuit', 'Dune Signal', 'Ember Vale', 'Hollow Peak', 'Iron Tide', 'Lumen Drift'
try {
    Connect-Pad
    Start-Dashboard $PSScriptRoot -Library
    Start-Sleep -Seconds 6  # the "Hi <user>!" toast over Home's foot has gone
    Save-Screen 'home'
    foreach ($n in 1..4) {
        Push-Button Down
        Start-Sleep -Seconds 2
        Save-Screen "down-$n"
    }

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
