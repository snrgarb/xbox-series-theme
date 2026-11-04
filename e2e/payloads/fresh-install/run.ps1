# A fresh install of the Bundle: Playnite as a new user has it, the theme and extension installed as Playnite installs
# them, then Fullscreen started. Passes when the extension says it started on the Dashboard and Playnite is still up and
# in front a while later. Run on the test VM's desktop by `vm.sh desktop`, with the .pext beside it; writes Playnite's
# logs, the result and screenshots to $env:E2E_OUT.
$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue'
$out = $env:E2E_OUT
$here = $PSScriptRoot
$data = Join-Path $env:APPDATA 'Playnite'
$fullscreenExe = Join-Path $env:LOCALAPPDATA 'Playnite\Playnite.FullscreenApp.exe'

function Say($text) { "[$(Get-Date -Format HH:mm:ss)] $text" | Tee-Object -FilePath "$out\steps.txt" -Append }

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Desk {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    public static uint ForegroundPid() { uint pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid); return pid; }
}
'@
[Desk]::SetProcessDPIAware() | Out-Null

function Shot($name) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    # JPEG: a 1080p screenshot is ~6 MB as PNG, a few hundred KB as this.
    $bmp.Save("$out\$name.jpg", [System.Drawing.Imaging.ImageFormat]::Jpeg)
    $g.Dispose(); $bmp.Dispose()
}

function Logs { (Get-Content "$data\playnite.log", "$data\extensions.log" -ErrorAction SilentlyContinue) -join "`n" }

$result = 'FAIL'
try {
    # 1. Playnite as a new user has it: never run, no add-ons.
    Get-Process Playnite.* -ErrorAction SilentlyContinue | Stop-Process -Force
    Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory "$data\library" -Force | Out-Null
    # Desktop's first-time wizard finished with no libraries picked: without it Fullscreen refuses to start and sends
    # the user to Desktop. Every real user has been through it before they can install an add-on.
    @{ FirstTimeWizardComplete = $true; DatabasePath = "$data\library" } | ConvertTo-Json | Set-Content "$data\config.json"
    Say "fresh Playnite data at $data"

    # 2. The Bundle installed as Playnite installs an add-on the user agreed to: queued in extinstalls.json, installed
    #    as Playnite next starts, which also selects a newly installed Fullscreen theme.
    $pext = Get-ChildItem $here -Filter *.pext | Select-Object -First 1
    if (-not $pext) { throw "No .pext in $here" }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($pext.FullName)
    try {
        $entry = $zip.Entries | Where-Object { $_.Name -like '*.pthm' } | Select-Object -First 1
        if (-not $entry) { throw "$($pext.Name) has no .pthm inside" }
        $pthm = Join-Path $here $entry.Name
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $pthm, $true)
    } finally { $zip.Dispose() }
    ConvertTo-Json @(
        @{ InstallType = 0; Path = $pthm },
        @{ InstallType = 0; Path = $pext.FullName }
    ) | Set-Content "$data\extinstalls.json"
    Say "queued $($entry.Name) and $($pext.Name)"

    # 3. Fullscreen started, as the user would.
    $playnite = Start-Process $fullscreenExe -PassThru
    Say "started Fullscreen, pid $($playnite.Id)"

    # 4. The extension reports which way it went.
    $deadline = (Get-Date).AddSeconds(120)
    $verdict = $null
    while (-not $verdict -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        $logs = Logs
        if ($logs -match 'Xbox Series extension started on the Dashboard') { $verdict = 'dashboard' }
        elseif ($logs -match 'Xbox Series extension stays dormant[^\r\n]*') { $verdict = "dormant: $($Matches[0])" }
        elseif ($playnite.HasExited) { $verdict = "Playnite exited with $($playnite.ExitCode)" }
    }
    Say "verdict: $(if ($verdict) { $verdict } else { 'nothing within 120 s' })"
    Shot 'started'

    # 5. Still up, and in front, once the Start-up has had time to finish.
    Start-Sleep -Seconds 20
    Shot 'settled'
    $alive = -not $playnite.HasExited
    $front = [Desk]::ForegroundPid() -eq $playnite.Id
    Say "after 20 s: running $alive, in front $front"
    $logs = Logs
    $errors = @(([regex]'(?m)^.*\|ERROR\|.*$').Matches($logs) | ForEach-Object Value)
    Say "errors logged: $($errors.Count)"
    $errors | ForEach-Object { Say "  $_" }

    if ($verdict -eq 'dashboard' -and $alive -and $front) { $result = 'PASS' }
} catch {
    Say "error: $_"
} finally {
    Say "result: $result"
    Copy-Item "$data\playnite.log", "$data\extensions.log", "$data\fullscreenConfig.json", "$data\config.json" $out -ErrorAction SilentlyContinue
    Get-Process Playnite.* -ErrorAction SilentlyContinue | Stop-Process -Force
}
exit $(if ($result -eq 'PASS') { 0 } else { 1 })
