# The PC's own battery in the Status area (#209). The VM has no battery, so a Debug build fakes one when Playnite starts
# with XBOXSERIES_FAKE_BATTERY set; a Release build ignores it. What the log says:
#   [Status] PC battery: none|57%, charging                        on the Dashboard, then as one comes or goes or charges
#   [Status] PC battery faked by XBOXSERIES_FAKE_BATTERY=...: ...  a Debug build taking the variable
#   Guide opened over <process>; ...                               the Guide took the foreground
#
# 1. No variable, as the VM is: "PC battery: none", and no "faked" line. Home's top bar and the Guide show the Status
#    area as today, with no PC battery: compare home-plain-topbar.jpg and guide-plain-status.jpg with guide-over-game's.
# 2. Playnite closed, and started again with XBOXSERIES_FAKE_BATTERY=57,charging and XBOXSERIES_FAKE_PAD_BATTERY=88
#    (the VM's virtual pad is on a cable, so its battery is faked too, to show both side by side): the "faked" lines, and
#    "PC battery: 57%, charging". Home's top bar and the Guide show 57% and Windows' upright charging battery, bolt inside,
#    before the pad's 88% battery, which lies on its side (home-fake-topbar.jpg, guide-fake-status.jpg).
# In both, everything on the top bar is centred on one line, within 1 px (Test-TopBarLine).
# Screenshots are whole screens, with the top bar and the Guide's top right cut out of them beside.
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()
$fake = '57,charging'
$padFake = '88'

# The extension's lines matching $Pattern after line $After of its log.
function Get-LogLines([string] $Pattern, [int] $After = 0) {
    @(Get-ExtensionLog | Select-Object -Skip $After | Where-Object { $_ -match $Pattern } | ForEach-Object { $_.Trim() })
}

# Waits up to $Seconds for a line of the extension's matching $Pattern after line $After of the log; the first, or $null.
function Wait-LogLine([string] $Pattern, [int] $After = 0, [int] $Seconds = 6) {
    $waited = [Diagnostics.Stopwatch]::StartNew()
    do {
        $lines = @(Get-LogLines $Pattern $After)
        if ($lines.Count) { return $lines[0] }
        Start-Sleep -Milliseconds 250
    } while ($waited.ElapsedMilliseconds -lt $Seconds * 1000)
    $null
}

# A part of the screen, as fractions of its width and height from its top left, saved as $Name.jpg.
function Save-ScreenPart([string] $Name, [double] $Left, [double] $Top, [double] $Width, [double] $Height) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $part = New-Object System.Drawing.Rectangle ([int]($b.Width * $Left)), ([int]($b.Height * $Top)), ([int]($b.Width * $Width)), ([int]($b.Height * $Height))
    $bmp = New-Object System.Drawing.Bitmap $part.Width, $part.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($part.Location, [System.Drawing.Point]::Empty, $part.Size)
    $bmp.Save((Join-Path $env:E2E_OUT "$Name.jpg"), [System.Drawing.Imaging.ImageFormat]::Jpeg)
    $g.Dispose(); $bmp.Dispose()
}

# Whether everything on Home's top bar sits on one line: the bar is 60 tall with its top 84 down a 1080-line screen, so
# its middle is 114 down. The white ink of each thing on it (the picture, the gamertag, the icons, the percents, the
# batteries, the clock), told apart by gaps of more than 8 columns, must be centred within 1 px of that. Text's ink sits
# off its line's middle by a font's own amount, so a bar laid out by its boxes alone can still look ragged; this
# measures what shows. What is off, if anything.
function Test-TopBarLine([string] $Phase) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $scale = $b.Height / 1080.0
    $top = [int](70 * $scale); $height = [int](90 * $scale)
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen((New-Object System.Drawing.Point 0, $top), [System.Drawing.Point]::Empty, $bmp.Size)
    $g.Dispose()
    $inkTop = New-Object int[] $b.Width; $inkBottom = New-Object int[] $b.Width
    for ($x = 0; $x -lt $b.Width; $x++) {
        $inkTop[$x] = -1
        for ($y = 0; $y -lt $height; $y++) {
            $p = $bmp.GetPixel($x, $y)
            if ($p.R -gt 200 -and $p.G -gt 200 -and $p.B -gt 200) {
                if ($inkTop[$x] -lt 0) { $inkTop[$x] = $y }
                $inkBottom[$x] = $y
            }
        }
    }
    $bmp.Dispose()
    $middle = 114 * $scale
    $bad = @()
    $x = 0
    while ($x -lt $b.Width) {
        if ($inkTop[$x] -lt 0) { $x++; continue }
        $start = $x; $end = $x; $t = [int]::MaxValue; $bt = -1; $gap = 0
        while ($x -lt $b.Width -and $gap -le 8) {
            if ($inkTop[$x] -ge 0) { $end = $x; $gap = 0; $t = [Math]::Min($t, $inkTop[$x]); $bt = [Math]::Max($bt, $inkBottom[$x]) } else { $gap++ }
            $x++
        }
        $centre = $top + ($t + $bt) / 2.0
        $off = $centre - $middle
        Say ("${Phase}: top bar ink x {0}-{1}, y {2}-{3}, centre {4:0.0}, {5:+0.0;-0.0;0} from the line" -f $start, $end, ($top + $t), ($top + $bt), $centre, $off) | Out-Host
        if ([Math]::Abs($off) -gt 1 * $scale) { $bad += ("${Phase}: top bar ink at x {0}-{1} is centred {2:+0.0;-0.0} px off the bar's line" -f $start, $end, $off) }
    }
    ,$bad
}

# Home's top bar, then the Guide opened over Home with its Status area at the top right, and closed again; what is
# wrong, if anything.
function Save-StatusArea([string] $Phase) {
    $bad = @()
    Save-Screen "home-$Phase" | Out-Host
    Save-ScreenPart "home-$Phase-topbar" 0.5 0 0.5 0.12 | Out-Host
    $bad += Test-TopBarLine $Phase
    $mark = @(Get-ExtensionLog).Count
    Push-Button Guide | Out-Host
    $opened = Wait-LogLine 'Guide opened over' $mark 5
    Start-Sleep -Milliseconds 900
    Save-Screen "guide-$Phase" | Out-Host
    Save-ScreenPart "guide-$Phase-status" 0.6 0 0.4 0.2 | Out-Host
    Say "${Phase}: $(if ($opened) { $opened } else { 'no Guide opened line' })" | Out-Host
    if (-not $opened) { $bad += "${Phase}: the Guide did not open" }
    Push-Button Guide | Out-Host
    Start-Sleep -Milliseconds 1500
    if ((Get-Foreground).Title -eq 'Xbox Series Guide') { $bad += "${Phase}: a second tap did not close the Guide" }
    ,$bad
}

function Get-LoggedErrors {
    $errors = @(([regex]'(?m)^.*\|ERROR\|.*$').Matches((Get-PlayniteLog)) | ForEach-Object Value)
    $errors | ForEach-Object { Say "logged: $_" | Out-Host }
    $errors.Count
}

try {
    # 1. No variable: nothing new.
    Remove-Item Env:XBOXSERIES_FAKE_BATTERY -ErrorAction SilentlyContinue
    Remove-Item Env:XBOXSERIES_FAKE_PAD_BATTERY -ErrorAction SilentlyContinue
    Connect-Pad
    Start-Dashboard $PSScriptRoot -Library
    Start-Sleep -Seconds 4
    $said = Get-LogLines '\[Status\] PC battery'
    $said | ForEach-Object { Say "logged: $_" }
    if (-not @($said | Where-Object { $_ -match '\[Status\] PC battery: none$' }).Count) {
        $failures += 'Without the variable, no "[Status] PC battery: none" line: is this a build with the PC battery?'
    }
    if (@($said | Where-Object { $_ -match 'faked by' }).Count) { $failures += 'Without the variable, the battery was faked' }
    if (@($said | Where-Object { $_ -match '\[Status\] PC battery: \d' }).Count) { $failures += 'Without the variable, a PC battery shows' }
    $failures += Save-StatusArea 'plain'

    $errors = Get-LoggedErrors
    if ($errors) { $failures += "$errors errors logged without the variable" }

    # 2. Closed as its window's close does it, so it keeps the Dashboard, and started again with the variable.
    $playnite = Get-Process Playnite.FullscreenApp -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($playnite) {
        $playnite.CloseMainWindow() | Out-Null
        if (-not $playnite.WaitForExit(20000)) { Say 'Playnite did not close in 20 s; stopping it' }
    }
    Stop-Playnite
    Start-Sleep -Seconds 2
    Save-PlayniteFiles -Move 'plain'
    $env:XBOXSERIES_FAKE_BATTERY = $fake
    $env:XBOXSERIES_FAKE_PAD_BATTERY = $padFake
    Say "Playnite stopped; starting it again with XBOXSERIES_FAKE_BATTERY=$fake and XBOXSERIES_FAKE_PAD_BATTERY=$padFake"
    Enter-Dashboard
    Start-Sleep -Seconds 4
    $said = Get-LogLines '\[Status\] PC battery'
    $said | ForEach-Object { Say "logged: $_" }
    if (-not @($said | Where-Object { $_ -match "faked by XBOXSERIES_FAKE_BATTERY=$fake" }).Count) {
        $failures += 'With the variable, no "faked by" line: a Release build, which ignores it?'
    }
    if (-not @($said | Where-Object { $_ -match '\[Status\] PC battery: 57%, charging$' }).Count) {
        $failures += 'With the variable, no "[Status] PC battery: 57%, charging" line'
    }
    $pad = Get-LogLines '\[Status\] pad battery faked'
    $pad | ForEach-Object { Say "logged: $_" }
    if (-not @($pad | Where-Object { $_ -match "XBOXSERIES_FAKE_PAD_BATTERY=$padFake`: $padFake%$" }).Count) {
        $failures += 'With the pad variable, no "pad battery faked" line'
    }
    $failures += Save-StatusArea 'fake'

    if (-not (Get-Process Playnite.FullscreenApp -ErrorAction SilentlyContinue)) { $failures += 'Playnite exited' }
    $errors = Get-LoggedErrors
    if ($errors) { $failures += "$errors errors logged with the variable" }

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
    Remove-Item Env:XBOXSERIES_FAKE_BATTERY -ErrorAction SilentlyContinue
    Remove-Item Env:XBOXSERIES_FAKE_PAD_BATTERY -ErrorAction SilentlyContinue
}
exit $(if ($result -eq 'PASS') { 0 } else { 1 })
