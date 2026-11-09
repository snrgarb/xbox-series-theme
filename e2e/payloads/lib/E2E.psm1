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

# The seed library's games, A to N: made-up names, each with a cover and a background of a colour of its own with its
# name on it. Six are favourites, eight installed, six tagged "E2E Picks"; the first six were played, Amber Circuit
# last (an hour ago), so the Recent row starts Amber Circuit, Brass Lantern, Cobalt Run, Dune Signal, Ember Vale, Frost
# Harbor, then the never played ones from Glass Orchard on.
$script:SeedGames = @(
    @{ Name = 'Amber Circuit'; IsInstalled = $true;  Favorite = $true;  Tags = 'E2E Picks'; Source = 'Steam'; PlayedHoursAgo = 1;   Playtime = 36000 }
    @{ Name = 'Brass Lantern'; IsInstalled = $true;  Favorite = $false;                     Source = 'Steam'; PlayedHoursAgo = 24;  Playtime = 7200 }
    @{ Name = 'Cobalt Run';    IsInstalled = $true;  Favorite = $true;                      Source = 'GOG';   PlayedHoursAgo = 48;  Playtime = 144000 }
    @{ Name = 'Dune Signal';   IsInstalled = $false; Favorite = $false; Tags = 'E2E Picks'; Source = 'Steam'; PlayedHoursAgo = 72;  Playtime = 2700 }
    @{ Name = 'Ember Vale';    IsInstalled = $true;  Favorite = $false; Tags = 'E2E Picks'; Source = 'GOG';   PlayedHoursAgo = 120; Playtime = 18000 }
    @{ Name = 'Frost Harbor';  IsInstalled = $false; Favorite = $true;                      Source = 'Steam'; PlayedHoursAgo = 192; Playtime = 54000 }
    @{ Name = 'Glass Orchard'; IsInstalled = $true;  Favorite = $false;                     Source = 'Steam' }
    @{ Name = 'Hollow Peak';   IsInstalled = $false; Favorite = $false; Tags = 'E2E Picks'; Source = 'GOG' }
    @{ Name = 'Iron Tide';     IsInstalled = $true;  Favorite = $true;  Tags = 'E2E Picks'; Source = 'Steam' }
    @{ Name = 'Jade Meridian'; IsInstalled = $false; Favorite = $false;                     Source = 'GOG' }
    @{ Name = 'Kite Protocol'; IsInstalled = $true;  Favorite = $false;                     Source = 'Steam' }
    @{ Name = 'Lumen Drift';   IsInstalled = $false; Favorite = $true;  Tags = 'E2E Picks'; Source = 'GOG' }
    @{ Name = 'Mossgate';      IsInstalled = $true;  Favorite = $true }
    @{ Name = 'Night Ferry';   IsInstalled = $false; Favorite = $false }
)

# The id the seed gives a game by its name: the same on every run, so a test can name a game in Playnite's log, or in
# a file the extension keeps by game id, without reading the library.
function Get-SeedId([string] $Name) {
    $md5 = [Security.Cryptography.MD5]::Create()
    try { [guid] $md5.ComputeHash([Text.Encoding]::UTF8.GetBytes("xbox-e2e:$Name")) } finally { $md5.Dispose() }
}

# Runs lib\Library.ps1, which writes and reads Playnite's library with Playnite's own code.
function Invoke-Library([string[]] $Arguments) {
    $powershell = Join-Path $env:windir 'SysWOW64\WindowsPowerShell\v1.0\powershell.exe'
    $said = & $powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Library.ps1') `
        -Program (Split-Path $script:FullscreenExe) -Library (Join-Path $script:Data 'library') @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Playnite's library: $($said -join "`n")" }
}

# Fills the fresh Playnite's library, after Initialize-Playnite and before Playnite starts, so Home's first paint has
# it. Start-Dashboard -Library calls this; a test that starts Fullscreen itself calls it between Initialize-Playnite
# and Start-Fullscreen.
#
# Written: the seed's fourteen games ($script:SeedGames above; -NoSeed leaves them out), then $Games, and a filter
# preset "My Picks", shown in Fullscreen's quick selection, so a Group on Home, the first one: the games tagged
# "E2E Picks" (Amber Circuit, Dune Signal, Ember Vale, Hollow Peak, Iron Tide, Lumen Drift; installed and not), by
# name, Z to A. A new library's own four presets stay, Groups too, after it: All, Favorites, Most Played, Recently
# Played (installed only).
#
# A game is a hashtable: Name, and any property of Playnite's Game by its name (IsInstalled, Favorite, Hidden,
# Playtime in seconds, GameId, PluginId, InstallDirectory, Description...; strings, numbers and booleans, as they go
# through JSON), plus Tags, Categories, Platforms (names, one or several), Source (a name), PlayedHoursAgo (sets
# LastActivity) and Hue (0 to 360, its pictures' colour):
#
#   Start-Dashboard $PSScriptRoot -Library -Games @(
#       @{ Name = 'Half-Life 2'; GameId = '220'; PluginId = 'cb91dfc9-b977-43bf-8e70-55f46e410fab'; IsInstalled = $true; Tags = 'E2E Picks' })
#
# Each game's id is Get-SeedId of its name, unless it gives an Id, and its pictures are library\files\<id>\cover.png
# (600x900) and background.jpg (1920x1080, a darker shade of the same colour, reading "<name> background"). Games are
# added in order, a minute apart, the first the most recently added. What was written is in $env:E2E_OUT\library.json,
# as Get-Library reads it.
function Initialize-Library([hashtable[]] $Games = @(), [switch] $NoSeed) {
    $specs = @(@(if (-not $NoSeed) { $script:SeedGames }) + $Games | ForEach-Object {
        $spec = $_.Clone()
        if (-not $spec.Id) { $spec.Id = Get-SeedId $spec.Name }
        $spec
    })
    $file = Join-Path $env:TEMP 'e2e-library-games.json'
    ConvertTo-Json $specs -Depth 4 | Set-Content $file -Encoding UTF8
    $written = Join-Path $script:Out 'library.json'
    Invoke-Library '-Write', $file, '-Read', $written
    $library = Get-Content $written -Raw -Encoding UTF8 | ConvertFrom-Json
    Say "library seeded: $($library.Games.Count) games, presets $(($library.Presets | ForEach-Object Name) -join ', ')"
    Write-LibrarySteps $library
}

# The library, a line a game and a line a preset, in steps.txt: enough to tell from one run what Playnite was given.
function Write-LibrarySteps($Library) {
    foreach ($g in $Library.Games) {
        Say "  game $($g.Name) $($g.Id): installed $($g.IsInstalled), favourite $($g.Favorite), last played $($g.LastActivity), source $($g.Source), tags $($g.Tags -join '/'), cover $([IO.File]::Exists($g.CoverImage)), background $([IO.File]::Exists($g.BackgroundImage))"
    }
    foreach ($p in $Library.Presets) {
        Say "  preset $($p.Name) $($p.Id): in Fullscreen $($p.ShowInFullscreen), selects $($p.Games.Count): $($p.Games -join ', ')"
    }
}

# What Playnite's library holds, read once Playnite has stopped (Stop-Playnite first): Games (Name, Id, IsInstalled,
# Favorite, Hidden, Playtime, LastActivity, Source, Tags, CoverImage and BackgroundImage as full paths), by name, and
# Presets (Name, Id, ShowInFullscreen, Games: the names its filter selects), in Playnite's order.
function Get-Library {
    $file = Join-Path $env:TEMP 'e2e-library-read.json'
    Invoke-Library '-Read', $file
    Get-Content $file -Raw -Encoding UTF8 | ConvertFrom-Json
}

# Starts Fullscreen on the new install and takes Start-up through to Home: skips the Boot video, picks the one profile
# at Who's playing today?, and waits for Start-up to finish. With -Library, Playnite's library is the seed's rather
# than empty, and with -Games those games too: see Initialize-Library.
function Start-Dashboard([string] $PayloadDir, [switch] $Library, [hashtable[]] $Games = @()) {
    Initialize-Playnite $PayloadDir
    if ($Library -or $Games.Count) { Initialize-Library -Games $Games }
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
