# Writes or reads Playnite's library with Playnite's own code (its Playnite.dll, loaded here), so what is written is
# what Playnite reads. Run by Initialize-Library and Get-Library (E2E.psm1) in a 32-bit PowerShell, as Playnite is
# 32-bit, and only while Playnite isn't running: the library's files are open to one process at a time.
#   -Write <file>   adds the games in that JSON file (Initialize-Library's, each with its Id) and the "My Picks" preset
#   -Read <file>    writes what the library holds to that JSON file
#   -BackgroundWidth <pixels>   with -Write: the backgrounds that wide (16:9) and grained, rather than 1920x1080 and flat
# Prints nothing and exits 0 when done; else prints what went wrong and exits 1.
param([string] $Program, [string] $Library, [string] $Write, [string] $Read, [int] $BackgroundWidth = 0)
$ErrorActionPreference = 'Stop'

# A picture of one colour (a hue in degrees, at a brightness of 0 to 1) with $Text in its middle; .png or .jpg by $Path.
# With -Grain, coarse noise shows faintly through the colour: a flat picture is a few kilobytes that decode in no time,
# and a test of how long large art takes to show needs one as heavy as a photograph.
function New-SeedImage([string] $Path, [string] $Text, [int] $Width, [int] $Height, [double] $Hue, [double] $Brightness, [switch] $Grain) {
    # Doubles throughout: with an integer first, PowerShell picks Math.Max(int, int) and rounds the hue to a sixth.
    $part = { param([double] $n) $k = ($n + $Hue / 60.0) % 6.0
        [int](255.0 * $Brightness * (1.0 - 0.7 * [Math]::Max(0.0, [Math]::Min([Math]::Min($k, 4.0 - $k), 1.0)))) }
    $bmp = New-Object System.Drawing.Bitmap $Width, $Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb((& $part 5), (& $part 3), (& $part 1)))
    if ($Grain) {
        $noise = New-Object System.Drawing.Bitmap 480, 270, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $all = New-Object System.Drawing.Rectangle 0, 0, 480, 270
        $data = $noise.LockBits($all, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, $noise.PixelFormat)
        $bytes = New-Object byte[] ($data.Stride * 270)
        (New-Object System.Random ([int] $Hue)).NextBytes($bytes)
        [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
        $noise.UnlockBits($data)
        $faint = New-Object System.Drawing.Imaging.ColorMatrix
        $faint.Matrix33 = 0.25
        $how = New-Object System.Drawing.Imaging.ImageAttributes
        $how.SetColorMatrix($faint)
        $g.DrawImage($noise, (New-Object System.Drawing.Rectangle 0, 0, $Width, $Height), 0, 0, 480, 270, [System.Drawing.GraphicsUnit]::Pixel, $how)
        $how.Dispose(); $noise.Dispose()
    }
    $font = New-Object System.Drawing.Font('Segoe UI', [single]($Width / 12), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $centre = New-Object System.Drawing.StringFormat
    $centre.Alignment = 'Center'; $centre.LineAlignment = 'Center'
    $g.DrawString($Text, $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, 0, $Width, $Height), $centre)
    $format = if ($Path -like '*.png') { [System.Drawing.Imaging.ImageFormat]::Png } else { [System.Drawing.Imaging.ImageFormat]::Jpeg }
    $bmp.Save($Path, $format)
    $centre.Dispose(); $font.Dispose(); $g.Dispose(); $bmp.Dispose()
}

try {
    Add-Type -AssemblyName System.Drawing
    Add-Type -Path (Join-Path $Program 'Playnite.SDK.dll'), (Join-Path $Program 'Playnite.dll')
    $db = New-Object Playnite.Database.GameDatabase $Library
    $db.OpenDatabase()
    try {
        if ($Write) {
            $specs = Get-Content $Write -Raw -Encoding UTF8 | ConvertFrom-Json
            $specs = @($specs)  # ConvertFrom-Json gives the array as one object
            $now = Get-Date
            for ($i = 0; $i -lt $specs.Count; $i++) {
                $spec = $specs[$i]
                $game = New-Object Playnite.SDK.Models.Game ([string] $spec.Name)
                $hue = ($i * 360 / 14 + [Math]::Floor($i / 14) * 13) % 360
                $addedAgo = $i
                foreach ($field in $spec.PSObject.Properties) {
                    $value = $field.Value
                    switch ($field.Name) {
                        'Name' { }
                        'Hue' { $hue = $value }
                        'Tags' { $game.TagIds = [guid[]] @($value | ForEach-Object { $db.Tags.Add([string] $_).Id }) }
                        'Categories' { $game.CategoryIds = [guid[]] @($value | ForEach-Object { $db.Categories.Add([string] $_).Id }) }
                        'Platforms' { $game.PlatformIds = [guid[]] @($value | ForEach-Object { $db.Platforms.Add([string] $_).Id }) }
                        'Source' { $game.SourceId = $db.Sources.Add([string] $value).Id }
                        'Publishers' { $game.PublisherIds = [guid[]] @($value | ForEach-Object { $db.Companies.Add([string] $_).Id }) }
                        'Features' { $game.FeatureIds = [guid[]] @($value | ForEach-Object { $db.Features.Add([string] $_).Id }) }
                        'AgeRatings' { $game.AgeRatingIds = [guid[]] @($value | ForEach-Object { $db.AgeRatings.Add([string] $_).Id }) }
                        'ReleaseYear' { $game.ReleaseDate = New-Object Playnite.SDK.Models.ReleaseDate ([int] $value) }
                        'PlayedHoursAgo' { $game.LastActivity = $now.AddHours(-$value) }
                        'AddedMinutesAgo' { $addedAgo = $value }
                        default { $game.($field.Name) = $value }
                    }
                }
                $files = $db.GetFileStoragePath($game.Id)
                # A real game's poster where e2e/run.sh staged one for this place in the list (lib\posters\NN.jpg, from
                # lib\posters.txt), so tiles look as they do on a real library; else a drawn one with the name on it.
                $poster = Join-Path $PSScriptRoot ('posters\{0:D2}.jpg' -f $i)
                if (Test-Path $poster) {
                    Copy-Item $poster (Join-Path $files 'cover.jpg')
                    $game.CoverImage = "$($game.Id)\cover.jpg"
                } else {
                    New-SeedImage (Join-Path $files 'cover.png') $game.Name 600 900 $hue 0.85
                    $game.CoverImage = "$($game.Id)\cover.png"
                }
                if ($BackgroundWidth) {
                    New-SeedImage (Join-Path $files 'background.jpg') "$($game.Name) background" $BackgroundWidth ([int]($BackgroundWidth * 9 / 16)) $hue 0.45 -Grain
                } else {
                    New-SeedImage (Join-Path $files 'background.jpg') "$($game.Name) background" 1920 1080 $hue 0.45
                }
                $game.BackgroundImage = "$($game.Id)\background.jpg"
                $db.Games.Add($game)
                # Playnite stamps Added as it adds; the Recent row pads itself with the most recently added. A minute
                # a game, in the order given, unless the game says otherwise (AddedMinutesAgo, below 0 for newer than all).
                $game.Added = $now.AddMinutes(-$addedAgo)
                $db.Games.Update($game)
            }

            $preset = New-Object Playnite.SDK.Models.FilterPreset
            $preset.Name = 'My Picks'
            $preset.ShowInFullscreeQuickSelection = $true  # Playnite's own spelling
            $preset.SortingOrder = [Playnite.SDK.Models.SortOrder]::Name
            $preset.SortingOrderDirection = [Playnite.SDK.Models.SortOrderDirection]::Descending
            $preset.GroupingOrder = [Playnite.SDK.Models.GroupableField]::None
            $preset.Settings = New-Object Playnite.SDK.Models.FilterPresetSettings
            $preset.Settings.Tag = New-Object Playnite.SDK.Models.IdItemFilterItemProperties ($db.Tags.Add('E2E Picks').Id)
            $db.FilterPresets.Add($preset)
            # First in the user's order of presets; those not in it follow by name.
            $order = New-Object Playnite.Database.FilterPresetsSettings
            $order.SortingOrder.Add($preset.Id)
            $db.SetFilterPresetsSettings($order)
        }
        if ($Read) {
            $path = { param($file) if ($file) { $db.GetFullFilePath($file) } }
            $all = [pscustomobject]@{
                Games = @($db.Games | Sort-Object Name | ForEach-Object {
                    [pscustomobject]@{ Name = $_.Name; Id = $_.Id; IsInstalled = $_.IsInstalled; Favorite = $_.Favorite; Hidden = $_.Hidden
                        Playtime = $_.Playtime; LastActivity = $(if ($_.LastActivity) { $_.LastActivity.ToString('s') })
                        Source = $(if ($_.Source) { $_.Source.Name }); Tags = @($_.Tags | ForEach-Object Name)
                        CoverImage = (& $path $_.CoverImage); BackgroundImage = (& $path $_.BackgroundImage) } })
                Presets = @($db.GetSortedFilterPresets() | ForEach-Object {
                    [pscustomobject]@{ Name = $_.Name; Id = $_.Id; ShowInFullscreen = $_.ShowInFullscreeQuickSelection
                        Games = @($db.GetFilteredGames($_.Settings) | ForEach-Object Name | Sort-Object) } })
            }
            ConvertTo-Json $all -Depth 4 | Set-Content $Read -Encoding UTF8
        }
    } finally { $db.Dispose() }
} catch {
    Write-Output "$_`n$($_.ScriptStackTrace)"
    exit 1
}
exit 0
