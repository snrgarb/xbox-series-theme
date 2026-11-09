# Achievements with PlayniteAchievements installed (addons.txt pins its release), on the seeded library: what Home's
# Achievements tile says and opens before PlayniteAchievements has recorded anything and once it has three games'
# achievements, then the other ways to achievements a user can press: a recent unlock on the Guide's Profile & system,
# the Game hub's stats card, and PlayniteAchievements' own entry under the Tile menu's More options > Extensions.
#
# Passes when, in both states, the extension reads PlayniteAchievements, focus reaches the Achievements tile and A on it
# puts the Guide in front; with nothing recorded the extension logs "Achievements: none (Nothing recorded yet)" (#151)
# and the Guide has no totals; with the seed it logs "Achievements: 95 Score, 5 unlocked" and the Guide shows them; the
# Game hub and Playnite's Extensions picker open for Amber Circuit; and Playnite is still up, at Home.
#
# No press is counted blind: each move waits for the extension's log to say where it went ("Home focus on the
# Achievements tile", "Game hub opened for ...", "Tile menu <game>: <rows>"), and the test stops, saying where focus was,
# when it didn't. Waits are by a stopwatch and log lines by their order: the VM's clock can jump mid-run. Playnite's
# main window gives UI Automation nothing, so what Home, the hub and Playnite's own windows show is for looking at:
# tile-focused.jpg has the tile's line ("Nothing recorded yet", "95 Score"), data-hub.jpg the card's "Achievements 3 / 5",
# data-extensions*.jpg what PlayniteAchievements offers. The Guide's texts are read, into <step>.txt.
#
# The achievements are given to PlayniteAchievements as its legacy per-game JSON cache, which it imports as it starts;
# no store account is signed in.
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()

$paId = 'e6aad2c9-6e06-4d8d-ac55-ac3b252b5f7b'
$paData = Join-Path $Data "ExtensionsData\$paId"
$steam = 'cb91dfc9-b977-43bf-8e70-55f46e410fab'
$guideTitle = 'Xbox Series Guide'
$told = 'Home focus on|Game hub opened|Tile menu|Extensions picker|XboxSeriesPlugin:Achievements:'

# A game of Steam's library plugin beside the seed's, which belong to no plugin.
$zenith = @{ Name = 'Zenith Rally'; GameId = '480'; PluginId = $steam; IsInstalled = $true; Source = 'Steam' }

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

# The extension's log, by line count: a mark in it stays where it was whatever the clock does.
function Get-ExtensionLog { @(Get-Content (Join-Path $Data 'extensions.log') -ErrorAction SilentlyContinue) }
function Get-Mark { (Get-ExtensionLog).Count }
function Get-LogLines([int] $After, [string] $Pattern) {
    @(Get-ExtensionLog | Select-Object -Skip $After | Where-Object { $_ -match $Pattern } | ForEach-Object { $_.Trim() })
}

# Waits up to $Seconds, by a stopwatch, for a line matching $Pattern after the mark $After; the last such line, or $null.
function Wait-Line([string] $Pattern, [double] $Seconds, [int] $After) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    do {
        $hit = Get-LogLines $After $Pattern | Select-Object -Last 1
        if ($hit) { return $hit }
        Start-Sleep -Milliseconds 200
    } while ($watch.Elapsed.TotalSeconds -lt $Seconds)
    $null
}

# Waits up to $Seconds for the window in front to have, or with -Not to stop having, the title $Title.
function Wait-Front([string] $Title, [double] $Seconds, [switch] $Not) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    do {
        if (((Get-Foreground).Title -eq $Title) -ne [bool] $Not) { return $true }
        Start-Sleep -Milliseconds 200
    } while ($watch.Elapsed.TotalSeconds -lt $Seconds)
    $false
}

# The Guide's texts, as UI Automation reads its window; none when it isn't there or can't be read.
function Get-GuideTexts {
    $texts = @()
    try {
        $named = New-Object Windows.Automation.PropertyCondition ([Windows.Automation.AutomationElement]::NameProperty), $guideTitle
        $window = [Windows.Automation.AutomationElement]::RootElement.FindFirst([Windows.Automation.TreeScope]::Children, $named)
        if ($window) {
            foreach ($e in $window.FindAll([Windows.Automation.TreeScope]::Subtree, [Windows.Automation.Condition]::TrueCondition)) {
                try { if ($e.Current.Name) { $texts += $e.Current.Name } } catch { }
            }
        }
    } catch { }
    $texts
}

# A step's screenshot and its line in steps.txt: the window in front and the log's last word on focus. With the Guide
# in front its texts go to <step>.txt and $script:Shown; otherwise $script:Shown is empty.
$script:Shown = @()
function Snap([string] $Name) {
    Save-Screen $Name
    $front = Get-Foreground
    $script:Shown = @()
    if ($front.Title -eq $guideTitle) {
        $script:Shown = @(Get-GuideTexts)
        $script:Shown | Set-Content (Join-Path $Out "$Name.txt") -Encoding UTF8
    }
    $focus = Get-LogLines 0 'Home focus on' | Select-Object -Last 1
    $where = if ($focus) { $focus -replace '^.*Home focus on ', '' } else { 'none' }
    $read = if ($script:Shown.Count) { "; $($script:Shown.Count) Guide texts" } else { '' }
    Say "$Name`: front $($front.Process) '$($front.Title)'; last focus line: $where$read"
}

function Fail([string] $What) { $script:failures += $What; Say "CHECK FAILED: $What" }

# Stops the test: the screen and what the log told since $After are kept, and the catch below fails it with $What.
function Stop-Test([string] $What, [int] $After = 0) {
    Save-Screen 'stopped'
    Get-LogLines $After $told | Select-Object -Last 12 | ForEach-Object { Say "  log: $_" }
    $front = Get-Foreground
    throw "$What (front: $($front.Process) '$($front.Title)'; see stopped.jpg)"
}

# One press that must move focus: waits for a log line matching $Pattern, pressing once more if none came in 3 s, and
# stops the test if still none. The line is left in $script:Line. (Say and Push-Button write to the output, so the
# functions here that press or say leave their answer in a $script: variable rather than return it.)
$script:Line = $null
function Move-Focus([string] $Button, [string] $Pattern, [string] $Toward) {
    foreach ($try in 1..2) {
        $mark = Get-Mark
        Push-Button $Button
        $script:Line = Wait-Line $Pattern 3 $mark
        if ($script:Line) { Start-Sleep -Milliseconds 400; return }
        Say "  $Button moved focus nowhere the log tells of (try $try)"
    }
    Stop-Test "$Button twice did not move focus to $Toward"
}

# The name in the log's last focus line of the kind $Kind ('Recent tile (.+)' or '(.+) tile').
function Get-Focused([string] $Line, [string] $Kind) { if ($Line -match "Home focus on the $Kind`$") { $Matches[1].Trim() } }

# Playnite closed as its window's close does it, so it saves what it has; killed only if it won't go.
function Close-Playnite {
    $p = Get-Process Playnite.FullscreenApp -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) {
        $p.CloseMainWindow() | Out-Null
        if (-not $p.WaitForExit(20000)) { Say 'Playnite did not close in 20 s; stopping it' }
    }
    Stop-Playnite
    Start-Sleep -Seconds 2
}

# Three games' achievements, as PlayniteAchievements' legacy cache held a game's: a file a game, named by its id. Five
# unlocked of nine. By the Bundle's Score rule (no platform points: by rarity) they come to 10 + 15 + 50 for Amber
# Circuit, 10 for Brass Lantern and 10 for Zenith Rally: 95.
function Set-SeedAchievements {
    $dir = Join-Path $paData 'achievement_cache'
    New-Item -ItemType Directory $dir -Force | Out-Null
    $now = (Get-Date).ToUniversalTime()
    $games = @(
        @{ Name = 'Amber Circuit'; AppId = 481; Achievements = @(
            @{ ApiName = 'ACH_FIRST_LAP'; DisplayName = 'First Lap'; Description = 'Finish a lap of any circuit'; Hidden = $false; GlobalPercentUnlocked = 82.5; UnlockTimeUtc = $now.AddDays(-9).ToString('o') }
            @{ ApiName = 'ACH_PODIUM'; DisplayName = 'Podium'; Description = 'Finish a race in the first three'; Hidden = $false; GlobalPercentUnlocked = 30.0; UnlockTimeUtc = $now.AddDays(-3).ToString('o') }
            @{ ApiName = 'ACH_CLEAN_SWEEP'; DisplayName = 'Clean Sweep'; Description = 'Win every race of a season'; Hidden = $false; GlobalPercentUnlocked = 4.0; UnlockTimeUtc = $now.AddHours(-5).ToString('o') }
            @{ ApiName = 'ACH_NIGHT_OWL'; DisplayName = 'Night Owl'; Description = 'Win a night race in the rain'; Hidden = $false; GlobalPercentUnlocked = 12.0 }
            @{ ApiName = 'ACH_GHOST'; DisplayName = 'Ghost in the Machine'; Description = 'Beat your own ghost by ten seconds'; Hidden = $true; GlobalPercentUnlocked = 1.5 }) }
        @{ Name = 'Brass Lantern'; AppId = 482; Achievements = @(
            @{ ApiName = 'ACH_LIGHT'; DisplayName = 'Let There Be Light'; Description = 'Light the first lantern'; Hidden = $false; UnlockTimeUtc = $now.AddDays(-1).ToString('o') }
            @{ ApiName = 'ACH_DARK'; DisplayName = 'Lights Out'; Description = 'Finish a chapter without a lantern'; Hidden = $false; GlobalPercentUnlocked = 20.0 }) }
        @{ Name = 'Zenith Rally'; AppId = 480; Achievements = @(
            @{ ApiName = 'ACH_START'; DisplayName = 'Green Flag'; Description = 'Start a rally'; Hidden = $false; GlobalPercentUnlocked = 60.0; UnlockTimeUtc = $now.AddDays(-20).ToString('o') }
            @{ ApiName = 'ACH_FINISH'; DisplayName = 'Chequered Flag'; Description = 'Finish a rally'; Hidden = $false; GlobalPercentUnlocked = 35.0 }) })
    foreach ($game in $games) {
        $id = Get-SeedId $game.Name
        @{ LastUpdatedUtc = $now.ToString('o'); ProviderKey = 'Steam'; LibrarySourceName = 'Steam'; HasAchievements = $true
            GameName = $game.Name; AppId = $game.AppId; PlayniteGameId = "$id"; Achievements = $game.Achievements } |
            ConvertTo-Json -Depth 5 | Set-Content (Join-Path $dir "$id.json") -Encoding UTF8
        Say "  achievements for $($game.Name) $id`: $(@($game.Achievements | Where-Object { $_.UnlockTimeUtc }).Count) of $($game.Achievements.Count) unlocked"
    }
    Say "PlayniteAchievements' legacy cache seeded: $($games.Count) games in $dir"
}

# What PlayniteAchievements has on disk, and what it and Playnite logged about it: why a seed wasn't taken, if it wasn't.
function Write-PlayniteAchievementsState([string] $Phase) {
    $manifest = Get-ChildItem (Join-Path $Data 'Extensions') -Recurse -Filter extension.yaml -ErrorAction SilentlyContinue |
        Where-Object { (Get-Content $_.FullName -Raw) -match 'PlayniteAchievements' } | Select-Object -First 1
    $version = if ($manifest) { (Get-Content $manifest.FullName | Where-Object { $_ -like 'Version:*' }) -join '' } else { 'not installed' }
    Say "$Phase`: PlayniteAchievements $version"
    Get-ChildItem $paData -Recurse -File -ErrorAction SilentlyContinue |
        ForEach-Object { Say "  its data: $($_.FullName.Substring($Data.Length)) $($_.Length) bytes" }
    $lines = @((Get-PlayniteLog) -split "`n" | Where-Object { $_ -match 'PlayniteAchievements|PlayAch|Legacy cache import|\|ERROR\|' })
    $lines | Set-Content (Join-Path $Out "$Phase-achievements-log.txt") -Encoding UTF8
    $lines | Where-Object { $_ -match 'Legacy cache import|\|ERROR\||Xbox|Achievements are read|no achievements' } | Select-Object -First 25 |
        ForEach-Object { Say "  logged: $($_.Trim())" }
}

# From Home as it starts, focus on the top bar's profile: Down to the Recent row's first tile, Down to the wide tiles
# under it, then along them to Achievements. Each step is the log's; the test stops where one doesn't come.
function Move-ToAchievementsTile([string] $Phase) {
    $wide = 'Home focus on the (Discord|Playnite settings|Achievements|Downloads) tile'
    Move-Focus Down 'Home focus on the Recent tile .+' 'the Recent row'
    Say "$Phase`: Down from the top bar: the Recent tile $(Get-Focused $script:Line 'Recent tile (.+)')"
    Move-Focus Down $wide 'the wide tiles'
    $tile = Get-Focused $script:Line '(.+) tile'
    Say "$Phase`: Down again: the $tile tile"
    foreach ($n in 1..4) {
        if ($tile -eq 'Achievements') { break }
        $button = if ($tile -eq 'Downloads') { 'Left' } else { 'Right' }
        Move-Focus $button $wide 'the next wide tile'
        $tile = Get-Focused $script:Line '(.+) tile'
        Say "$Phase`: $button`: the $tile tile"
    }
    if ($tile -ne 'Achievements') { Stop-Test "$Phase`: focus never reached the Achievements tile; it is on the $tile tile" }
    Start-Sleep -Milliseconds 800
    Snap "$Phase-tile-focused"
}

# A on the Achievements tile, which has focus: the Guide must come to the front. Its texts are left in $script:Guide.
function Open-AchievementsTile([string] $Phase) {
    Push-Button A
    if (-not (Wait-Front $guideTitle 6)) { Stop-Test "$Phase`: A on the Achievements tile did not put the Guide in front" }
    Start-Sleep -Milliseconds 1200
    Snap "$Phase-tile-opened"
    $script:Guide = $script:Shown
    $reads = @($script:Guide | Where-Object { $_ -match '^\d[\d,]* (G|achievements?)$|^Switch profile$|^Profile & system$' }) -join ' | '
    Say "  the Guide reads: $reads"
    if ($script:Guide -notcontains 'Profile & system') { Fail "$Phase`: the Guide did not open on Profile & system (or its texts can't be read: $($script:Guide.Count))" }
}

# B on the Guide, then Up from the Achievements tile to the Recent tile Down left; the game it is, in $script:Game.
$script:Game = $null
function Close-Guide([string] $Phase) {
    Push-Button B
    if (-not (Wait-Front $guideTitle 5 -Not)) { Stop-Test "$Phase`: B did not close the Guide" }
    Start-Sleep -Milliseconds 1200
    Snap "$Phase-guide-closed"
    Move-Focus Up 'Home focus on the Recent tile .+' 'the Recent row, from the Achievements tile'
    $script:Game = Get-Focused $script:Line 'Recent tile (.+)'
}

# Back to Home with focus on a Recent tile, from wherever the presses before left things: a window of Playnite's own
# or an add-on's is closed with B, or A when B leaves it (a message box's OK); on the main window Right, B and Down are
# tried until the log tells of focus on Home. $script:AtHome says whether it did.
$script:AtHome = $false
function Enter-Home([string] $Step) {
    $script:AtHome = $false
    foreach ($round in 1..10) {
        $front = Get-Foreground
        if ($front.Process -ne 'Playnite.FullscreenApp') { Say "  $Step`: $($front.Process) '$($front.Title)' is in front, not Playnite"; Start-Sleep -Seconds 1; continue }
        if ($front.Title -eq 'Xbox Series Start-up') {
            Say "  $Step`: Start-up is in front; picking the profile"
            Push-Button A; Start-Sleep -Seconds 5
        } elseif ($front.Title -ne 'Playnite') {
            Say "  $Step`: closing '$($front.Title)'"
            Push-Button B
            if (-not (Wait-Front $front.Title 2 -Not)) { Push-Button A; Wait-Front $front.Title 2 -Not | Out-Null }
            Start-Sleep -Milliseconds 800
        } else {
            $mark = Get-Mark
            $line = $null
            foreach ($button in 'Right', 'B', 'Down') {
                Push-Button $button
                $line = Wait-Line 'Home focus on' 2 $mark
                if ($line -or (Get-Foreground).Title -ne 'Playnite') { break }
            }
            if ($line) {
                Start-Sleep -Milliseconds 800
                $where = $line -replace '^.*Home focus on ', ''
                Say "  $Step`: back at Home: $where"
                $script:AtHome = $true
                return
            }
        }
    }
    Save-Screen "$Step-not-home"
    Fail "$Step`: could not get back to Home (see $Step-not-home.jpg)"
}

# Along the Recent row to Amber Circuit's tile, the row's first, from any of its tiles; the game focus ends on is in
# $script:Game.
function Move-ToAmberCircuit {
    $line = Get-LogLines 0 'Home focus on' | Select-Object -Last 1
    $game = Get-Focused $line 'Recent tile (.+)'
    if (-not $game) {
        Move-Focus Up 'Home focus on the Recent tile .+' 'the Recent row'
        $game = Get-Focused $script:Line 'Recent tile (.+)'
    }
    foreach ($n in 1..8) {
        if ($game -eq 'Amber Circuit') { break }
        $mark = Get-Mark
        Push-Button Left
        $line = Wait-Line 'Home focus on the (Recent tile .+|Play history tile)' 2 $mark
        if (-not $line) { break }
        $game = Get-Focused $line 'Recent tile (.+)'
        Start-Sleep -Milliseconds 300
    }
    $script:Game = $game
}

# Down a menu of the Bundle's from its first row to the row $Row, by the rows the log line $Line gave ("...: a, b, c").
function Move-ToRow([string] $Line, [string] $Row) {
    $rows = @((($Line -split ': ', 2)[1]) -split ', ')
    $at = [array]::IndexOf($rows, $Row)
    if ($at -lt 0) { Stop-Test "the menu has no row $Row`: $($rows -join ', ')" }
    Say "  rows: $($rows -join ', '); $Row is $at down"
    for ($i = 0; $i -lt $at; $i++) { Push-Button Down; Start-Sleep -Milliseconds 400 }
}

# The extension's own line on PlayniteAchievements at start, and the totals it then logged, which must match $Totals.
function Test-Start([string] $Phase, [string] $Totals) {
    $line = Wait-PlayniteLog 'Achievements are read from PlayniteAchievements|PlayniteAchievements[^\r\n]*no achievements' 30
    Say "$Phase`: adapter: $(if ($line) { $line.Trim() } else { 'said nothing in 30 s' })"
    if ($line -notmatch 'Achievements are read from PlayniteAchievements') { Fail "$Phase`: the extension does not read PlayniteAchievements" }
    $said = Wait-Line "XboxSeriesPlugin:Achievements: $Totals`$" 30 0
    $last = Get-LogLines 0 'XboxSeriesPlugin:Achievements: ' | Select-Object -Last 1
    $logged = if ($last) { $last -replace '^.*XboxSeriesPlugin:', '' } else { 'no Achievements: line (a build before #151 logs none)' }
    Say "$Phase`: extension: $logged"
    if (-not $said) { Fail "$Phase`: the extension did not log the totals expected (pattern: $Totals)" }
}

try {
    Connect-Pad
    Initialize-Playnite $PSScriptRoot
    Initialize-Library -Games $zenith

    # 1. PlayniteAchievements installed, nothing recorded: as a user has it right after installing it.
    Enter-Dashboard
    Start-Sleep -Seconds 6  # the "Hi <user>!" toast has gone
    Test-Start 'nodata' 'none \(Nothing recorded yet\)'
    Write-PlayniteAchievementsState 'nodata'
    Snap 'nodata-home'
    Move-ToAchievementsTile 'nodata'
    Open-AchievementsTile 'nodata'
    if (@($script:Guide | Where-Object { $_ -match '^\d[\d,]* (G|achievements?)$' }).Count) { Fail 'nodata: the Guide shows achievement totals with nothing recorded' }
    Close-Guide 'nodata'

    # 2. The same Playnite once PlayniteAchievements has three games' achievements.
    Close-Playnite
    Save-PlayniteFiles -Move 'nodata'
    Set-SeedAchievements
    Enter-Dashboard
    Start-Sleep -Seconds 6
    Test-Start 'data' '95 Score, 5 unlocked'
    Write-PlayniteAchievementsState 'data'
    Snap 'data-home'
    Move-ToAchievementsTile 'data'
    Open-AchievementsTile 'data'
    foreach ($text in '95 G', '5 achievements', 'Clean Sweep', 'Let There Be Light', 'Podium') {
        if ($script:Guide -notcontains $text) { Fail "data: the Guide does not show '$text'" }
    }

    # A recent unlock on Profile & system, two down from the profile card: A does nothing on it, by design.
    Push-Button Down; Start-Sleep -Milliseconds 600
    Push-Button Down; Start-Sleep -Milliseconds 600
    Snap 'data-guide-recent'
    Push-Button A; Start-Sleep -Milliseconds 1500
    Snap 'data-guide-recent-a'
    $front = (Get-Foreground).Title
    Say "  A on a recent unlock: $(if ($front -eq $guideTitle) { 'the Guide stays, as built' } else { "'$front' came to the front" })"
    if ($front -ne $guideTitle) {
        Fail "data: A two down from the profile card took the Guide away, to '$front' (see data-guide-recent.jpg for where focus was)"
        Enter-Home 'data-guide'
        Move-ToAmberCircuit
    } else { Close-Guide 'data' }
    if ($script:Game -ne 'Amber Circuit') { Say "  focus is on $($script:Game); moving to Amber Circuit"; Move-ToAmberCircuit }
    if ($script:Game -ne 'Amber Circuit') { Stop-Test "data: focus is on the Recent tile '$($script:Game)', not Amber Circuit" }

    # The Game hub of Amber Circuit: its "Your game stats" card has the Achievements row (data-hub.jpg). Up from Play
    # to the card, and A there, which has nothing to do (#34 is not built).
    $mark = Get-Mark
    Push-Button A
    if (-not (Wait-Line 'Game hub opened for Amber Circuit' 8 $mark)) { Stop-Test 'data: A on Amber Circuit did not open its Game hub' $mark }
    Start-Sleep -Seconds 3
    Snap 'data-hub'
    Push-Button Up; Start-Sleep -Milliseconds 1200
    Snap 'data-hub-up'
    Push-Button A; Start-Sleep -Seconds 2
    Snap 'data-hub-up-a'
    $front = (Get-Foreground).Title
    Say "  A after Up in the hub: $(if ($front -eq 'Playnite') { 'no window opened (compare data-hub-up.jpg and data-hub-up-a.jpg)' } else { "'$front' came to the front (GameError: focus was still on Play, and the seed's games have nothing to start)" })"
    Enter-Home 'data-hub'
    if (-not $script:AtHome) { Stop-Test 'data: not back at Home after the Game hub' }
    Move-ToAmberCircuit
    if ($script:Game -ne 'Amber Circuit') { Stop-Test "data: after the hub focus is on the Recent tile '$($script:Game)', not Amber Circuit" }
    Snap 'data-hub-closed'

    # PlayniteAchievements' own entry for the game: the Tile menu (Start) > More options > Extensions, Playnite's
    # picker of every add-on's game menu items, then A on its first item. For looking at.
    $mark = Get-Mark
    Push-Button Start
    $menu = Wait-Line 'Tile menu Amber Circuit: .+' 5 $mark
    if (-not $menu) { Stop-Test 'data: Start on Amber Circuit did not open its Tile menu' $mark }
    Start-Sleep -Milliseconds 800
    Snap 'data-tilemenu'
    Move-ToRow $menu 'More options'
    $mark = Get-Mark
    Push-Button A
    $more = Wait-Line "Tile menu's flyout More options: .+" 5 $mark
    if (-not $more) { Stop-Test 'data: A on More options did not open its flyout' $mark }
    Start-Sleep -Milliseconds 800
    Snap 'data-more-options'
    Move-ToRow $more 'Extensions'
    $mark = Get-Mark
    Push-Button A
    if (-not (Wait-Line "Extensions picker opens for Amber Circuit" 5 $mark)) { Stop-Test 'data: A on Extensions did not open the picker' $mark }
    Start-Sleep -Seconds 3
    Snap 'data-extensions'
    Push-Button A; Start-Sleep -Seconds 4
    Snap 'data-extensions-a'
    Enter-Home 'data-extensions'
    Snap 'data-end'

    if (-not (Get-Process Playnite.FullscreenApp -ErrorAction SilentlyContinue)) { Fail 'Playnite exited' }
    $errors = @(([regex]'(?m)^.*\|ERROR\|.*$').Matches((Get-PlayniteLog)) | ForEach-Object Value)
    Say "$($errors.Count) errors in the last start's logs (not a failure: PlayniteAchievements, signed in nowhere, logs its own)"
    $errors | Select-Object -First 10 | ForEach-Object { Say "  logged: $($_.Trim())" }

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
}
exit $(if ($result -eq 'PASS') { 0 } else { 1 })
