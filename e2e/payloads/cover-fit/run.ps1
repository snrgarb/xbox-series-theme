# Art fit: every game tile fills itself with its game's art, cropped to the tile's shape, with no bands
# top and bottom or at the sides. Screenshots only, judged by eye: the seed's library (real games' posters) plus three
# games whose covers have a red border and their edges labelled TOP, BOTTOM, L and R: a 2:3 cover (600x900), a Steam
# header (920x430) and a square one (512x512), played most recently so they lead the Recent row, added last so they lead
# empty Search, and tagged E2E Picks. On every shot, every tile is art to its edges, with no dark or blurred band; a
# cover of its tile's own shape (the square on the Recent row, the 2:3 on a Group card) shows all four red edges; one
# taller than its tile keeps TOP and loses from the bottom only, so a logo at its top stays; and one wider than its tile
# loses the same amount from both sides. The seed's real games show their Xbox box art or poster instead, once the
# extension has fetched it ("[Xbox art] <game>: ..." in its log).
#
#   home-portrait, home-header, home-square   the Recent row, focus on each in turn
#   hub-portrait, hub-header, hub-square      each one's Game hub, its cover card first
#   home-wide, groups-1 to groups-3           Down to the wide tiles, then through the Groups
#   search                                    Search, empty: the games most recently added
#   history                                   My games & apps' Play history rows, from the Recent row's Play history tile
#   games-1, games-2                          My games & apps' Games grid (the theme's tiles), then a row further down
#
# Home's focus starts on the profile after Start-up, so Down first takes it to the Recent row. Focus on Home and in My
# games & apps' nav is moved a press at a time until the extension's log says it is where wanted. Passes when every
# shot was taken; a step that loses its way leaves a shot of where it stopped and fails.
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
Add-Type -AssemblyName System.Drawing
$result = 'FAIL'

function New-EdgeCover([string] $Path, [int] $W, [int] $H, [string] $Label) {
    $bmp = New-Object System.Drawing.Bitmap $W, $H
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(30, 60, 110))
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::Red), ([single]([Math]::Max(8, $W / 40)))
    $g.DrawRectangle($pen, 4, 4, $W - 8, $H - 8)
    $f = New-Object System.Drawing.Font('Segoe UI', [single]([Math]::Min($W, $H) / 9), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat
    $b = [System.Drawing.Brushes]::Yellow
    $all = New-Object System.Drawing.RectangleF 0, 0, $W, $H
    $sf.Alignment = 'Center'; $sf.LineAlignment = 'Near'; $g.DrawString('TOP', $f, $b, $all, $sf)
    $sf.LineAlignment = 'Far'; $g.DrawString('BOTTOM', $f, $b, $all, $sf)
    $sf.Alignment = 'Near'; $sf.LineAlignment = 'Center'; $g.DrawString('L', $f, $b, $all, $sf)
    $sf.Alignment = 'Far'; $g.DrawString('R', $f, $b, $all, $sf)
    $sf.Alignment = 'Center'; $g.DrawString($Label, $f, [System.Drawing.Brushes]::White, $all, $sf)
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}

function Shot([string] $Name, [int] $Wait = 1500) { Start-Sleep -Milliseconds $Wait; Save-Screen $Name; Say "shot $Name" }

# The extension's own log, and the lines in it after $After matching $Pattern.
function Get-Mark { @(Get-ExtensionLog).Count }
function Get-LogLines([int] $After, [string] $Pattern) {
    @(Get-ExtensionLog | Select-Object -Skip $After | Where-Object { $_ -match $Pattern } | ForEach-Object { $_.Trim() })
}

# Presses $Button until the log's last line matching $Told also matches $Pattern, at most $Max times, waiting up to
# 2.5 s after each press for a new $Told line. Throws, after a screenshot "lost-<what>", if it never gets there.
function Step-To([string] $Button, [string] $Told, [string] $Pattern, [int] $Max, [string] $What) {
    foreach ($n in 0..$Max) {
        $last = Get-LogLines 0 $Told | Select-Object -Last 1
        if ($last -match $Pattern) { Start-Sleep -Milliseconds 500; return }
        if ($n -eq $Max) { break }
        $mark = Get-Mark
        Push-Button $Button
        foreach ($t in 1..10) { if (Get-LogLines $mark $Told) { break }; Start-Sleep -Milliseconds 250 }
        Start-Sleep -Milliseconds 300
    }
    Save-Screen "lost-$What"
    throw "focus never reached /$Pattern/ with $Max $Button for $What; the log's last word: $last"
}

function Step-Home([string] $Button, [string] $Pattern, [int] $Max, [string] $What) { Step-To $Button 'Home focus on ' $Pattern $Max $What }

# A on the Recent tile for $Game: its Game hub, then B back to Home.
function Show-Hub([string] $Game, [string] $Name) {
    Step-Home Right "Recent tile $Game$" 8 $Name
    Push-Button A; Shot $Name 4000
    $mark = Get-Mark
    Push-Button B
    foreach ($n in 1..20) { if (Get-LogLines $mark 'Home focus on ') { break }; Start-Sleep -Milliseconds 250 }
    Start-Sleep -Seconds 1
}

try {
    Connect-Pad
    Initialize-Playnite $PSScriptRoot
    Initialize-Library -Games @(
        @{ Name = 'Portrait Edges'; IsInstalled = $true; PlayedHoursAgo = 0.1; AddedMinutesAgo = -1; Playtime = 3600; Tags = 'E2E Picks'; Source = 'Steam'
           Description = 'A long description to fill the About card. ' * 8 }
        @{ Name = 'Header Edges'; IsInstalled = $false; PlayedHoursAgo = 0.2; AddedMinutesAgo = -2; Tags = 'E2E Picks'; Source = 'Steam' }
        @{ Name = 'Square Edges'; IsInstalled = $true; PlayedHoursAgo = 0.3; AddedMinutesAgo = -3; Tags = 'E2E Picks'; Source = 'GOG' })
    $lib = Get-Content (Join-Path $Out 'library.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($g in $lib.Games) {
        switch ($g.Name) {
            'Portrait Edges' { New-EdgeCover $g.CoverImage 600 900 '2:3' }
            'Header Edges' { New-EdgeCover $g.CoverImage 920 430 'header' }
            'Square Edges' { New-EdgeCover $g.CoverImage 512 512 '1:1' }
        }
    }
    Say "screen $([System.Windows.Forms.Screen]::PrimaryScreen.Bounds)"
    Enter-Dashboard
    Start-Sleep -Seconds 6  # the "Hi <user>!" toast over Home's foot has gone

    # Home's focus starts on the profile: Down to the Recent row, then along it.
    Push-Button Down; Start-Sleep -Milliseconds 700
    Step-Home Left 'Recent tile Portrait Edges$' 10 'home-portrait'; Shot 'home-portrait'
    Step-Home Right 'Recent tile Header Edges$' 3 'home-header'; Shot 'home-header'
    Step-Home Right 'Recent tile Square Edges$' 3 'home-square'; Shot 'home-square'

    Step-Home Left 'Recent tile Portrait Edges$' 3 'hub-portrait'
    Show-Hub 'Portrait Edges' 'hub-portrait'
    Show-Hub 'Header Edges' 'hub-header'
    Show-Hub 'Square Edges' 'hub-square'

    Push-Button Down; Shot 'home-wide'
    Push-Button Down; Shot 'groups-1' 2500
    Push-Button Down; Shot 'groups-2' 2500
    Push-Button Down; Shot 'groups-3' 2500

    # Back to the Recent row: Up through the Groups, from the first to the wide tiles, and from them to the Recent row.
    # (Left and Right stay within a Group, and B does nothing there.)
    Step-Home Up 'Recent tile ' 8 'back-home'

    # Search, empty, from the Recent row, and back. Search logs nothing: B puts its keyboard away first, if it is up,
    # then leaves the page, so a second B only while Home hasn't said where its focus is.
    Push-Button Y; Shot 'search' 3000
    $mark = Get-Mark
    Push-Button B; Start-Sleep -Milliseconds 1500
    if (-not (Get-LogLines $mark 'Home focus on ')) { Push-Button B; Start-Sleep -Seconds 2 }

    # My games & apps: Play history's rows, from the Recent row's last tile, then the Games grid through the nav.
    Step-Home Right 'Play history tile' 10 'history'
    Push-Button A; Shot 'history' 3000
    $mark = Get-Mark
    Push-Button Left
    foreach ($t in 1..10) { if (Get-LogLines $mark 'My games & apps nav focus on ') { break }; Start-Sleep -Milliseconds 250 }
    if (-not (Get-LogLines $mark 'My games & apps nav focus on ')) { Save-Screen 'lost-nav'; throw 'Left from Play history did not reach the nav' }
    Step-To Up 'My games & apps nav focus on ' 'nav focus on Games,' 6 'games-1'
    Push-Button Right; Shot 'games-1' 3000
    Push-Button Down; Push-Button Down; Shot 'games-2' 2000
    $result = 'PASS'
} catch {
    Say "error: $_`n$($_.ScriptStackTrace)"
    try { Save-Screen 'error' } catch {}
} finally {
    Say "result: $result"
    Save-PlayniteFiles
    Stop-Playnite
    Disconnect-Pad
}
exit $(if ($result -eq 'PASS') { 0 } else { 1 })
