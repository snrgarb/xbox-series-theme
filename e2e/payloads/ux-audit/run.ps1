# Throwaway UX audit (Discord suggestions): screenshots only, judged by eye. The seed's library plus three games
# whose covers show where cropping falls: a portrait 600x900, a Steam-header 920x430 and a square 512x512 cover, each
# with a red border and its edges labelled, played most recently so they lead the Recent row.
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

try {
    Connect-Pad
    Initialize-Playnite $PSScriptRoot
    Initialize-Library -Games @(
        @{ Name = 'Portrait Edges'; IsInstalled = $true; PlayedHoursAgo = 0.1; Playtime = 3600; Tags = 'E2E Picks'; Source = 'Steam'
           Description = 'A long description to fill the About card. ' * 8 }
        @{ Name = 'Header Edges'; IsInstalled = $false; PlayedHoursAgo = 0.2; Tags = 'E2E Picks'; Source = 'Steam' }
        @{ Name = 'Square Edges'; IsInstalled = $true; PlayedHoursAgo = 0.3; Tags = 'E2E Picks'; Source = 'GOG' })
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
    Shot 'home-1' 8000
    Push-Button Right; Shot 'home-2'
    Push-Button Right; Shot 'home-3'
    Push-Button Left; Push-Button Left; Start-Sleep -Milliseconds 800
    Push-Button A; Shot 'hub-portrait' 4000
    Push-Button B; Start-Sleep -Seconds 2
    Push-Button Right; Start-Sleep -Milliseconds 800
    Push-Button A; Shot 'hub-header' 4000
    Push-Button B; Start-Sleep -Seconds 2
    Push-Button Down; Shot 'home-wide'
    Push-Button Down; Shot 'groups-1' 2500
    Push-Button Down; Shot 'groups-2' 2500
    Push-Button Down; Shot 'groups-3' 2500
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
