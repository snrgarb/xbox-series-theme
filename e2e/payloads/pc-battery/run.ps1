# The PC's own battery in the Status area (#209). The VM has no battery, so a Debug build fakes one when Playnite starts
# with XBOXSERIES_FAKE_BATTERY set; a Release build ignores it. What the log says:
#   [Status] PC battery: none|57%, charging                        as the extension starts, and as it changes
#   [Status] PC battery faked by XBOXSERIES_FAKE_BATTERY=...: ...  a Debug build taking the variable
#   Guide opened over <process>; ...                               the Guide took the foreground
#
# 1. No variable, as the VM is: "PC battery: none", and no "faked" line. Home's top bar and the Guide show the Status
#    area as today, with no PC battery: compare home-plain-topbar.jpg and guide-plain-status.jpg with guide-over-game's.
# 2. Playnite closed, and started again with XBOXSERIES_FAKE_BATTERY=57,charging: the "faked" line, and
#    "PC battery: 57%, charging". Home's top bar and the Guide show 57%, a cell a little over half full and a bolt,
#    before the pad's battery (home-fake-topbar.jpg, guide-fake-status.jpg).
# Screenshots are whole screens, with the top bar and the Guide's top right cut out of them beside.
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()
$fake = '57,charging'

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

# Home's top bar, then the Guide opened over Home with its Status area at the top right, and closed again; what is
# wrong, if anything.
function Save-StatusArea([string] $Phase) {
    $bad = @()
    Save-Screen "home-$Phase" | Out-Host
    Save-ScreenPart "home-$Phase-topbar" 0.5 0 0.5 0.12 | Out-Host
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
    Say "Playnite stopped; starting it again with XBOXSERIES_FAKE_BATTERY=$fake"
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
}
exit $(if ($result -eq 'PASS') { 0 } else { 1 })
