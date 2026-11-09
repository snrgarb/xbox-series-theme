# Achievements with PlayniteAchievements installed (addons.txt pins its release), on the seeded library: what Home's
# Achievements tile says and opens before PlayniteAchievements has recorded anything and once it has three games'
# achievements, then every other way to achievements a user can press: a recent unlock on the Guide's Profile & system,
# PlayniteAchievements' own entry under the Tile menu's More options > Extensions, and the Game hub's stats card.
#
# Passes when, in both states, the extension reads PlayniteAchievements and the tile opens the Guide; with nothing
# recorded the tile says "Nothing recorded yet" (#151); with achievements recorded there are totals; and Playnite is
# still up. The rest is for looking at: each step leaves <step>.jpg, <step>.txt (every text Playnite's windows show, as
# UI Automation reads them) and a line in steps.txt with the window in front, the element with focus and Playnite's
# windows. The achievements are given to PlayniteAchievements as its legacy per-game JSON cache, which it imports as it
# starts; no store account is signed in.
Import-Module (Join-Path $PSScriptRoot 'lib\E2E.psm1') -Force
$result = 'FAIL'
$failures = @()

$paId = 'e6aad2c9-6e06-4d8d-ac55-ac3b252b5f7b'
$paData = Join-Path $Data "ExtensionsData\$paId"
$steam = 'cb91dfc9-b977-43bf-8e70-55f46e410fab'

# A game of Steam's library plugin beside the seed's, which belong to no plugin: if PlayniteAchievements takes
# achievements only for a game of the provider's own library, this one still has them.
$zenith = @{ Name = 'Zenith Rally'; GameId = '480'; PluginId = $steam; IsInstalled = $true; Source = 'Steam' }

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

# Every text Playnite's windows show and the names of its top-level windows, as UI Automation reads them; nothing when
# it can't.
function Get-Shown {
    $texts = @(); $windows = @()
    try {
        $playnite = Get-Process Playnite.FullscreenApp -ErrorAction Stop | Select-Object -First 1
        $ofPlaynite = New-Object Windows.Automation.PropertyCondition ([Windows.Automation.AutomationElement]::ProcessIdProperty), $playnite.Id
        foreach ($window in [Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children, $ofPlaynite)) {
            $windows += "'$($window.Current.Name)'"
            foreach ($e in $window.FindAll([Windows.Automation.TreeScope]::Subtree, [Windows.Automation.Condition]::TrueCondition)) {
                try { if ($e.Current.Name) { $texts += $e.Current.Name } } catch { }
            }
        }
    } catch { $windows += "UI Automation failed: $_" }
    [pscustomobject]@{ Texts = $texts; Windows = $windows }
}

function Get-Focus {
    try {
        $f = [Windows.Automation.AutomationElement]::FocusedElement.Current
        "$($f.ClassName) '$($f.Name)' $($f.AutomationId)".Trim()
    } catch { "unknown ($_)" }
}

# A step's screenshot, texts and line in steps.txt. The texts are left in $script:Shown rather than returned: Say and
# Push-Button write their lines to the output too, which a caller would get mixed in with them.
$script:Shown = @()
function Snap([string] $Name) {
    Save-Screen $Name
    $shown = Get-Shown
    $shown.Texts | Set-Content (Join-Path $Out "$Name.txt") -Encoding UTF8
    $front = Get-Foreground
    Say "$Name`: front $($front.Process) '$($front.Title)'; focus $(Get-Focus); windows $($shown.Windows -join ', '); $($shown.Texts.Count) texts"
    $script:Shown = @($shown.Texts)
}

function Press([string[]] $Buttons, [int] $WaitMs = 500) {
    foreach ($b in $Buttons) { Push-Button $b; Start-Sleep -Milliseconds $WaitMs }
}

function Fail([string] $What) { $script:failures += $What; Say "CHECK FAILED: $What" }

# Says which of $Wanted a step's texts hold, for reading steps.txt without the screenshots.
function Note([string] $Step, [string[]] $Texts, [string[]] $Wanted) {
    $found = @($Wanted | Where-Object { $w = $_; @($Texts | Where-Object { $_ -like "*$w*" }).Count })
    $missing = @($Wanted | Where-Object { $found -notcontains $_ })
    Say "  $Step shows: $(if ($found) { $found -join ' | ' } else { '(none of them)' }); not: $(if ($missing) { $missing -join ' | ' } else { '(nothing missing)' })"
}

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

# The extension's own line on PlayniteAchievements at start: read, not there, or not readable.
function Test-Adapter([string] $Phase) {
    $line = Wait-PlayniteLog 'Achievements are read from PlayniteAchievements|PlayniteAchievements[^\r\n]*no achievements' 30
    Say "$Phase`: adapter: $(if ($line) { $line.Trim() } else { 'said nothing in 30 s' })"
    if ($line -notmatch 'Achievements are read from PlayniteAchievements') { Fail "$Phase`: the extension does not read PlayniteAchievements" }
}

# Home's Achievements tile, the third of the four under the Recent row, from the Recent row's first tile: focused,
# and opened with A. The texts with the tile focused are left in $script:Tile, those with it open in $script:Guide.
function Open-AchievementsTile([string] $Phase) {
    Press Down
    Snap "$Phase-promo-row"
    Press Left, Left, Left, Right, Right
    Snap "$Phase-tile-focused"
    $script:Tile = $script:Shown
    Note "$Phase-tile-focused" $script:Tile 'Achievements', 'Nothing recorded yet', 'Needs PlayniteAchievements', ' Score'
    Press A -WaitMs 2000
    Snap "$Phase-tile-opened"
    $script:Guide = $script:Shown
    $front = Get-Foreground
    if ($front.Title -ne 'Xbox Series Guide') { Fail "$Phase`: A on the Achievements tile did not put the Guide in front: $($front.Process) '$($front.Title)'" }
    Note "$Phase-tile-opened" $script:Guide 'Switch profile', ' G', 'achievement', 'Clean Sweep', 'Let There Be Light', 'Podium', 'Settings', 'Power'
}

function Close-Guide([string] $Phase) {
    Press B -WaitMs 1500
    Snap "$Phase-guide-closed"
    $front = Get-Foreground
    if ($front.Title -eq 'Xbox Series Guide') { Fail "$Phase`: B did not close the Guide" }
    Press Up
}

# Moves down a menu of the Bundle's to the row named $Row, by where the texts shown put it among $Rows, the rows such
# a menu can have, in order; $Otherwise presses of Down when UI Automation doesn't show them.
function Move-ToRow([string[]] $Texts, [string[]] $Rows, [string] $Row, [int] $Otherwise) {
    $shown = @($Rows | Where-Object { $Texts -contains $_ })
    $at = [array]::IndexOf($shown, $Row)
    if ($at -lt 0) { $at = $Otherwise; Say "  $Row not among the texts ($($shown -join ', ')); pressing Down $Otherwise times" }
    else { Say "  rows $($shown -join ', '); $Row is $at down" }
    for ($i = 0; $i -lt $at; $i++) { Press Down -WaitMs 350 }
}

try {
    Connect-Pad
    Initialize-Playnite $PSScriptRoot
    Initialize-Library -Games $zenith

    # 1. PlayniteAchievements installed, nothing recorded: as a user has it right after installing it.
    Enter-Dashboard
    Start-Sleep -Seconds 6  # the "Hi <user>!" toast has gone
    Test-Adapter 'nodata'
    $said = Wait-PlayniteLog 'XboxSeriesPlugin:Achievements: [^\r\n]*' 20
    Say "nodata: extension: $(if ($said) { $said.Trim() } else { 'no Achievements: line (a build before #151 logs none)' })"
    Write-PlayniteAchievementsState 'nodata'
    Snap 'nodata-home'
    Open-AchievementsTile 'nodata'
    if ($said -notmatch 'none \(Nothing recorded yet\)' -and $script:Tile -notcontains 'Nothing recorded yet') {
        Fail 'nodata: the Achievements tile does not say "Nothing recorded yet" (no log line, no such text on Home)'
    }
    if (@($script:Guide | Where-Object { $_ -match '^\d[\d,]* achievements?$' }).Count) { Fail 'nodata: the Guide shows achievement totals with nothing recorded' }
    Close-Guide 'nodata'

    # 2. The same Playnite once PlayniteAchievements has three games' achievements.
    Close-Playnite
    Save-PlayniteFiles -Move 'nodata'
    Set-SeedAchievements
    Enter-Dashboard
    Start-Sleep -Seconds 8
    Test-Adapter 'data'
    $said = Wait-PlayniteLog 'XboxSeriesPlugin:Achievements: \d[^\r\n]*' 30
    Say "data: extension: $(if ($said) { $said.Trim() } else { 'no Achievements: <n> Score line (a build before #151 logs none)' })"
    Write-PlayniteAchievementsState 'data'
    Snap 'data-home'
    Open-AchievementsTile 'data'
    $score = @($script:Tile | Where-Object { $_ -match '^\d[\d,]* Score$' }) | Select-Object -First 1
    $count = @($script:Guide | Where-Object { $_ -match '^\d[\d,]* achievements?$' }) | Select-Object -First 1
    Say "data: the tile says '$score', the Guide '$count' (seeded: 95 Score by rarity, 5 achievements)"
    if (-not $said -and -not $score -and -not $count) {
        Fail 'data: no achievement totals: not logged, not on the tile, not on the Guide (was the seed imported? see data-achievements-log.txt and the data lines above)'
    } elseif ($said -and $said -notmatch ', 5 unlocked') { Say "  NOT AS SEEDED: $($said.Trim())" }
    if ($script:Tile -contains 'Nothing recorded yet' -or $script:Tile -contains 'Needs PlayniteAchievements') { Fail 'data: the tile still says it has nothing' }

    # A recent unlock on Profile & system: under the profile card and Switch profile. A does nothing on it, by design.
    Press Down, Down
    Snap 'data-guide-recent'
    Press A -WaitMs 1200
    Snap 'data-guide-recent-a'
    if ((Get-Foreground).Title -ne 'Xbox Series Guide') { Say '  A on a recent unlock took the Guide from the front' }
    Close-Guide 'data'

    # PlayniteAchievements' own entry for a game: the Tile menu (Start) > More options > Extensions, Playnite's picker
    # of every add-on's game menu items, then A on what it offers, twice.
    Press Start -WaitMs 1500
    Snap 'data-tilemenu'
    Move-ToRow $script:Shown ('Play game', 'Install', 'Quit', 'Add to Favourites', 'Remove from Favourites', 'Manage game', 'Uninstall', 'Customise Home', 'More options') 'More options' 4
    Press A -WaitMs 1500
    Snap 'data-more-options'
    Move-ToRow $script:Shown ('Hide this game', 'Unhide this game', 'Enable HDR', 'Disable HDR', 'Extensions', 'Remove') 'Extensions' 1
    Press A -WaitMs 2500
    Snap 'data-extensions'
    Note 'data-extensions' $script:Shown 'Playnite Achievements', 'View', 'Refresh', 'Manage'
    Press A -WaitMs 2500
    Snap 'data-extensions-a'
    Note 'data-extensions-a' $script:Shown 'View', 'Refresh', 'Manage', 'Clean Sweep', 'First Lap', 'Night Owl'
    Press A -WaitMs 4000
    Snap 'data-extensions-a-a'
    Note 'data-extensions-a-a' $script:Shown 'Clean Sweep', 'First Lap', 'Podium', 'Night Owl', 'Ghost in the Machine', 'Amber Circuit'
    foreach ($n in 1..3) {
        Press B -WaitMs 1500
        Snap "data-extensions-b$n"
    }

    # 3. The Game hub of Amber Circuit, from a new start so focus is known to be on the Recent row's first tile: its
    # "Your game stats" card has the Achievements row. Down from the hub's actions to its cards, and A there.
    Close-Playnite
    Save-PlayniteFiles -Move 'data'
    Enter-Dashboard
    Start-Sleep -Seconds 8
    Press A -WaitMs 3000
    Snap 'hub'
    Note 'hub' $script:Shown 'Amber Circuit', 'Your game stats', 'Time played', 'Achievements', '3 / 5', '60%'
    Press Down -WaitMs 1200
    Snap 'hub-down'
    $cards = $script:Shown
    Press A -WaitMs 2000
    Snap 'hub-down-a'
    Say "  A on the hub's card: $(if (($script:Shown -join '|') -eq ($cards -join '|')) { 'the same texts as before it' } else { 'the texts changed' })"
    Note 'hub-down-a' $script:Shown 'Clean Sweep', 'First Lap', 'Night Owl', 'Your game stats'
    Press B -WaitMs 1500
    Snap 'hub-b'

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
