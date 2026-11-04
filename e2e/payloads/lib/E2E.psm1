# What every end-to-end test does on the test VM's desktop: a new user's Playnite with the Bundle installed, Fullscreen
# started, a virtual Xbox pad pressed, Playnite's logs waited on, the screen and foreground looked at. Imported by each
# test's run.ps1, which `e2e/run.sh` puts beside this folder as lib\. Everything a test writes goes to $env:E2E_OUT.
$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue'

$script:Out = $env:E2E_OUT
$script:Data = Join-Path $env:APPDATA 'Playnite'
$script:FullscreenExe = Join-Path $env:LOCALAPPDATA 'Playnite\Playnite.FullscreenApp.exe'
$script:Playnite = $null
$script:Pad = $null

Add-Type -AssemblyName System.Windows.Forms, System.Drawing, System.IO.Compression.FileSystem
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class E2EDesk {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    public static uint WindowPid(IntPtr h) { uint pid; GetWindowThreadProcessId(h, out pid); return pid; }
    // Windows gives the foreground only to a process that sent the last input: a tap of Alt from this process is that
    // input, as Playnite has it when the user has just pressed A to play.
    public static void TakeForeground(IntPtr h) {
        keybd_event(0x12, 0, 0, UIntPtr.Zero);
        keybd_event(0x12, 0, 2, UIntPtr.Zero);
        SetForegroundWindow(h);
    }
    public static uint ForegroundPid() { uint pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid); return pid; }
    public static string ForegroundTitle() { var s = new StringBuilder(256); GetWindowText(GetForegroundWindow(), s, 256); return s.ToString(); }
}
'@
[E2EDesk]::SetProcessDPIAware() | Out-Null

function Say([string] $Text) {
    $line = "[$(Get-Date -Format HH:mm:ss.fff)] $Text"
    Add-Content (Join-Path $script:Out 'steps.txt') $line -Encoding UTF8
    Write-Output $line
}

function Save-Screen([string] $Name) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    $bmp.Save((Join-Path $script:Out "$Name.jpg"), [System.Drawing.Imaging.ImageFormat]::Jpeg)
    $g.Dispose(); $bmp.Dispose()
}

function Get-PlayniteLog {
    (Get-Content (Join-Path $script:Data 'playnite.log'), (Join-Path $script:Data 'extensions.log') -ErrorAction SilentlyContinue) -join "`n"
}

# Waits for a line matching $Pattern in Playnite's logs, written after $After lines were there; returns the line, or
# $null after $Seconds. Throws if Playnite exits meanwhile.
function Wait-PlayniteLog([string] $Pattern, [int] $Seconds = 30, [int] $After = 0) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        $lines = @((Get-PlayniteLog) -split "`n")
        $hit = $lines | Select-Object -Skip $After | Where-Object { $_ -match $Pattern } | Select-Object -First 1
        if ($hit) { return $hit }
        if ($script:Playnite -and $script:Playnite.HasExited) { throw "Playnite exited with $($script:Playnite.ExitCode)" }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    return $null
}

function Get-PlayniteLogLength { @((Get-PlayniteLog) -split "`n").Count }

function Get-Foreground {
    $pid_ = [E2EDesk]::ForegroundPid()
    $name = (Get-Process -Id $pid_ -ErrorAction SilentlyContinue).ProcessName
    [pscustomobject]@{ Pid = $pid_; Process = $name; Title = [E2EDesk]::ForegroundTitle() }
}

# Playnite as a new user has it, never run and with no add-on, but past Desktop's first-time wizard (no libraries
# picked): without it Fullscreen refuses to start and sends the user to Desktop, and every real user has been through
# it before they can install an add-on. Then the Bundle queued as Playnite installs an add-on the user agreed to:
# installed as Playnite next starts, which also selects a newly installed Fullscreen theme.
function Initialize-Playnite([string] $PayloadDir) {
    Get-Process Playnite.* -ErrorAction SilentlyContinue | Stop-Process -Force
    Remove-Item $script:Data -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory (Join-Path $script:Data 'library') -Force | Out-Null
    @{ FirstTimeWizardComplete = $true; DatabasePath = (Join-Path $script:Data 'library') } |
        ConvertTo-Json | Set-Content (Join-Path $script:Data 'config.json')

    $pext = Get-ChildItem $PayloadDir -Filter *.pext | Select-Object -First 1
    if (-not $pext) { throw "No .pext in $PayloadDir" }
    $zip = [IO.Compression.ZipFile]::OpenRead($pext.FullName)
    try {
        $entry = $zip.Entries | Where-Object { $_.Name -like '*.pthm' } | Select-Object -First 1
        if (-not $entry) { throw "$($pext.Name) has no .pthm inside" }
        $pthm = Join-Path $PayloadDir $entry.Name
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $pthm, $true)
    } finally { $zip.Dispose() }
    ConvertTo-Json @(@{ InstallType = 0; Path = $pthm }, @{ InstallType = 0; Path = $pext.FullName }) |
        Set-Content (Join-Path $script:Data 'extinstalls.json')
    Say "fresh Playnite, $($entry.Name) and $($pext.Name) queued"
}

function Start-Fullscreen {
    # From Playnite's folder, as its shortcut starts it: SDL reads gamecontrollerdb.txt from the working directory.
    $script:Playnite = Start-Process $script:FullscreenExe -WorkingDirectory (Split-Path $script:FullscreenExe) -PassThru
    Say "started Fullscreen, pid $($script:Playnite.Id)"
    $script:Playnite
}

function Stop-Playnite {
    Get-Process Playnite.* -ErrorAction SilentlyContinue | Stop-Process -Force
}

function Save-PlayniteFiles {
    foreach ($f in 'playnite.log', 'extensions.log', 'fullscreenConfig.json', 'config.json') {
        Copy-Item (Join-Path $script:Data $f) $script:Out -ErrorAction SilentlyContinue
    }
}

# A virtual wired Xbox 360 pad on ViGEmBus, as Windows and XInput see a real one, the Guide button included.
function Connect-Pad {
    Add-Type -Path (Join-Path $PSScriptRoot 'Nefarius.ViGEm.Client.dll')
    $client = New-Object Nefarius.ViGEm.Client.ViGEmClient
    $script:Pad = $client.CreateXbox360Controller()
    $script:Pad.Connect()
    Say "virtual pad connected"
}

# Presses and releases a button: A, B, X, Y, Start, Back, Guide, Up, Down, Left, Right, LeftShoulder, RightShoulder.
function Push-Button([string] $Button, [int] $HoldMs = 120) {
    $b = [Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button]::$Button
    if (-not $b) { throw "No button $Button" }
    $script:Pad.SetButtonState($b, $true)
    Start-Sleep -Milliseconds $HoldMs
    $script:Pad.SetButtonState($b, $false)
    Say "pressed $Button for $HoldMs ms"
}

function Disconnect-Pad { if ($script:Pad) { try { $script:Pad.Disconnect() } catch {} } }

# Starts Fullscreen on the new install and takes Start-up through to Home: skips the Boot video, picks the one profile
# at Who's playing today?, and waits for Start-up to finish.
function Start-Dashboard([string] $PayloadDir) {
    Initialize-Playnite $PayloadDir
    Start-Fullscreen | Out-Null
    if (-not (Wait-PlayniteLog 'Xbox Series extension started on the Dashboard' 120)) {
        $dormant = Wait-PlayniteLog 'Xbox Series extension stays dormant[^\r\n]*' 1
        throw "The extension did not start on the Dashboard$(if ($dormant) { ": $dormant" })"
    }
    Say "extension started on the Dashboard"
    if (-not (Wait-PlayniteLog 'Start-up phase WhosPlaying' 30)) {
        # The Boot video plays first; a press skips it.
        Push-Button A
        if (-not (Wait-PlayniteLog 'Start-up phase WhosPlaying' 30)) { throw "Start-up never reached Who's playing today?" }
    }
    Start-Sleep -Milliseconds 800
    Save-Screen 'whos-playing'
    Push-Button A
    if (-not (Wait-PlayniteLog 'Start-up phase None' 30)) { throw "Start-up did not finish after the profile was picked" }
    Say "Start-up finished"
}

# A stand-in for a game: a full-screen, borderless window of its own process, put in front as a game launched from
# Playnite would be. Started with no console window, as the payload itself is. Returns the window's process.
function Start-StandInGame {
    $script = Join-Path $PSScriptRoot 'StandInGame.ps1'
    $started = Get-Date
    Start-Process conhost.exe -ArgumentList '--headless', 'powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass',
        '-STA', '-File', "`"$script`""
    $deadline = $started.AddSeconds(60)
    while ((Get-Date) -lt $deadline) {
        $window = [E2EDesk]::FindWindow([NullString]::Value, 'Stand-in game')  # $null would pass "", a class no window has
        if ($window -ne [IntPtr]::Zero -and [E2EDesk]::IsWindowVisible($window)) {
            [E2EDesk]::TakeForeground($window)
            Start-Sleep -Milliseconds 300
            if ((Get-Foreground).Title -eq 'Stand-in game') {
                $p = Get-Process -Id ([E2EDesk]::WindowPid($window))
                Say "stand-in game in front after $([int]((Get-Date) - $started).TotalSeconds) s, pid $($p.Id)"
                return $p
            }
        }
        Start-Sleep -Milliseconds 250
    }
    $front = Get-Foreground
    $running = @(Get-CimInstance Win32_Process -Filter "Name = 'powershell.exe'" | Where-Object CommandLine -like '*StandInGame.ps1*').Count
    Say "stand-in: $running process(es) running, window $(if ($window -ne [IntPtr]::Zero) { 'found' } else { 'not found' })"
    throw "The stand-in game never came to the front (foreground: $($front.Process) '$($front.Title)')"
}

Export-ModuleMember -Function * -Variable Out, Data
