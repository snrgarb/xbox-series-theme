# Achievements from PlayniteAchievements

How the Bundle gets achievements, what a user has to set up today for them to work, and what an onboarding flow will need to cover. Terms (**Toast**, **Score**, **Rare**, **Profile**, **Guide**) are as in `CONTEXT.md`.

## What the Bundle shows

- **The Achievement unlocked Toast.** It shows when the game running now, or the one that just exited, unlocks something. It comes in three forms:
  - one unlock gets the green pill;
  - one **Rare** unlock gets the blue pill with the diamond and its own sound;
  - several unlocks together get "<n> achievements unlocked".
- **The Guide's Profile & system** shows the **Score** total, the number of achievements unlocked, and the last three unlocks.
- Not built yet: the **Achievements page** in the Game hub (#34).

Without PlayniteAchievements, none of this appears and nothing fails.

## How it works

PlayniteAchievements ([justin-delano/PlayniteAchievements](https://github.com/justin-delano/PlayniteAchievements), a Playnite generic extension) does the real work. It signs in to each platform, fetches achievements, keeps them in its cache, and watches the running game for unlocks. The Bundle only reads what it records.

```
Steam / Xbox / PSN / GOG / RetroAchievements / …
        │  (its sign-ins, its refreshes, its in-game polling)
        ▼
PlayniteAchievements ── cache ──► PlayniteAchievementsSource (adapter, reflection)
        │                                │  IAchievements port
        └─ AchievementUnlocked event ───►│
                                         ▼
                              ExtensionCore.Achievements
                     (who to announce, Score, totals, Toast timing)
                                         ▼
                        AchievementToast / ToastWindow / Guide
```

### The adapter: `src/XboxSeries.Extension/PlayniteAchievementsSource*.cs`

- **No API to build against.** PlayniteAchievements offers no SDK or contract, so the adapter finds the plugin instance by its Id `e6aad2c9-6e06-4d8d-ac55-ac3b252b5f7b` and reaches its members by reflection. Those members were checked against its source at v4.1:
  - `AchievementDataService.GetAllGameAchievementData` and `GetGameAchievementData(Guid)`;
  - per achievement: `ApiName`, `DisplayName`, `Description` (optional), `UnlockedIconPath`, `GlobalPercentUnlocked`, `UnlockTimeUtc` and `Unlocked`.
- **Binding and failure.** It binds once Playnite has loaded every plugin. If the plugin isn't installed, or a member has been renamed or reshaped, the adapter logs one line and reports no achievements. It never throws into Playnite.
- **What it listens to:**
  - the static `PlayniteAchievementsPlugin.AchievementUnlocked` event, which is raised live while a game runs;
  - `CacheManager.CacheInvalidated` and `CacheDeltaUpdated`, and `RefreshRuntime.GameRefreshed`, which are raised when a refresh or an edit saves.
- **Coalescing.** Cache changes are gathered for 500 ms and then read off the UI thread. `Games()` only hands out the last read, so the UI thread never waits on PlayniteAchievements.
- **Ignored unlock events:** previews, test fires, friend unlocks, progress updates and "game completed" banners. PlayniteAchievements' own Test buttons therefore never show the Bundle's Toast; use the Debug menu instead (see Testing below).

### The rules: `src/XboxSeries.Core/ExtensionCore.Achievements.cs`

- **The first read is a baseline.** Everything PlayniteAchievements already had when Playnite started is never announced.
- **Only the current session counts.** An unlock is announced only if it belongs to the running game, or the game that most recently stopped (until another starts), and was unlocked since that game started.
- **Each unlock is announced once.** Dedupe is on (game, achievement key). The same unlock often arrives twice: live, then again from the cache.
- **Grouping.** Unlocks heard within 1 s of each other, or found in one refresh, become one "<n> achievements unlocked" Toast.
- **Rare** means a known rarity under 10%. An achievement with unknown rarity is never Rare.
- **Score** comes from rarity: ≤1% → 100, ≤5% → 50, ≤10% → 30, ≤25% → 20, ≤50% → 15, anything else (or unknown) → 10.
- **Totals are per PC, not per Profile.** PlayniteAchievements keeps no per-user data, so every Profile shows the same numbers.

### When a Toast actually appears

PlayniteAchievements notices an unlock in one of two ways:

| Path | When | Delay |
|---|---|---|
| **In-game monitor**, which raises `AchievementUnlocked` | While the game runs, if **Monitor Achievements While a Game Is Running** is on (the default) and the game's platform is signed in | Steam reads the client's local stats file, so it is near-instant. Other platforms are polled every **In-game poll interval**, 15 s by default and 10 s at minimum. |
| **Refresh after exit**, through a cache change | A refresh after the game closes, or a periodic or manual refresh | Seconds to minutes. Announced only if it is the game that just exited and the unlock is from its session. |

## What a user has to do today

All of this is in PlayniteAchievements' own UI. The Bundle cannot do any of it for them yet.

1. **Install PlayniteAchievements** (v4.1 or later), from Playnite's add-on browser under Generic, or from its `.pext`. Restart Playnite.
2. **Sign in to each platform** in PlayniteAchievements' settings, on the Platforms tab.
   - Steam, GOG, Epic, Xbox, PSN and others each use an embedded sign-in window, which needs a mouse and keyboard.
   - In Fullscreen mode, the settings open from the main menu → **Open Plugin Settings**, as a popout.
3. **Run a first refresh** from the landing page (Desktop sidebar) or main menu → **Installed Refresh** or **Full Refresh**.
   - That refresh records what is already unlocked and announces nothing; neither does the Bundle.
4. **Leave in-game monitoring on:** General → Sync & Updates → **Monitor Achievements While a Game Is Running**.
5. **Turn off PlayniteAchievements' own unlock pop-up and sound**, or every unlock shows twice with two sounds:
   - Notifications → Behavior → **Show my unlock notifications** off;
   - Notifications → Styles → Sounds → **Play a sound on unlock notifications** off.

   Keep **Enable notifications** on, so it can still warn when a platform needs signing in again. Its `AchievementUnlocked` event is raised whatever these switches say, which the Bundle relies on.
6. **Run games borderless or windowed where possible.** PlayniteAchievements says its own pop-ups don't show over exclusive-fullscreen DirectX 9 games. The Bundle's Toast is a topmost WPF window and is likely limited the same way. This has not been verified.

## Known gaps

- **No version contract.** A PlayniteAchievements update that renames a member silently turns achievements off; only the log says so. This needs an onboarding/status check (below) and a re-check of the adapter on each of its releases.
- **Silent failure states.** The user sees no achievements but can't tell why:
  - PlayniteAchievements isn't installed;
  - the installed version is one the adapter can't read;
  - no platform is signed in;
  - nothing has been refreshed;
  - in-game monitoring is off.
- **Unlocks with no time are never announced.** PlayniteAchievements records some platforms' unlocks without a timestamp; the Bundle counts them in the totals but treats them as long ago.
- **Rarity display.** 9.95% shows as "10%" while counting as Rare.
- **Sign-in needs a mouse.** PlayniteAchievements' sign-in windows are web pages, not controller-friendly.

## Onboarding: what it will need

An onboarding system (first run, and re-runnable from Settings) should take the user from nothing to working Toasts, driven by the controller wherever possible. Proposed steps, each a state the extension can detect:

| # | State | How to detect | What onboarding does |
|---|---|---|---|
| 1 | **Not installed** | No plugin with the Id in `api.Addons.Plugins` | Explain what it adds, then offer to install via the URI `playnite://playnite/installaddon/PlayniteAchievements`. Playnite's handler isn't gated on Desktop mode, but this is untested in Fullscreen. A restart is required afterwards. |
| 2 | **Incompatible version** | Found, but `Binding` failed | Say which version is installed and that the Bundle needs an update, or PlayniteAchievements does. |
| 3 | **No platform signed in / no data** | Bound, but `Games()` is empty after the first read | Tell the user to open PlayniteAchievements' settings (Fullscreen: main menu → Open Plugin Settings) and sign in. Say this step needs a mouse and keyboard. |
| 4 | **Never refreshed** | Same as 3; the two can't be told apart without reading more of its state | Offer to start an Installed refresh. That would mean reflecting into its refresh entry point (not bound today), or telling the user where the menu item is. |
| 5 | **In-game monitoring off** | `Settings.Persisted.EnableInGamePolling` (not bound today) | Ask them to turn it on: without it, Toasts only arrive after the game exits. |
| 6 | **Double notifications** | `Persisted.EnableUnlockToasts` / `EnableUnlockSounds` (not bound today) | Ask them to turn PlayniteAchievements' pop-up and sound off, or offer to do it. |
| 7 | **Ready** | Bound, has games, monitoring on, its toasts off | Show a test Toast, like the Debug menu's Achievement, so they see what to expect. |

### Changes this implies

- **The port reports a status, not a bool.** `IAchievements.IsAvailable` would become something like `NotInstalled | Incompatible(version) | NoData | Ready`, plus the two setting flags. The adapter already knows each case; today it only logs them.
- **Reading, and maybe writing, PlayniteAchievements' settings.** It keeps them in `PersistedSettings`, reachable from the plugin instance, so the adapter's reflection would grow. Writing another extension's settings is invasive and may not persist unless it saves them itself. Prefer telling the user, and only write after an explicit "do it for me".
- **A place for it in the UI.** A step in a first-run flow, plus a Settings entry ("Achievements: Ready / Sign in needed / …") that reopens it.
- **Persisting "seen".** Onboarding shouldn't nag after it is dismissed. Re-show it only when the state gets worse, for example when an update breaks the binding.
- **Re-checks.** Re-run detection after PlayniteAchievements' settings popout closes, or on its `SettingsSaved` event, so the status updates without a restart.

### Open questions

- Should installing PlayniteAchievements be part of the Bundle's own first run, or an optional extra?
- Do we write its settings (steps 5–6), or only instruct?
- Is a Desktop-mode hand-off ("finish sign-in on the desktop") acceptable for step 3, given its sign-in windows?

## Testing

- **Debug build** → Settings → Debug → Notifications: **Achievement**, **Rare achievement** and **Three achievements** fire made-up unlocks through the core. These need no PlayniteAchievements and are not counted in the totals.
- **A real unlock:** install and set up PlayniteAchievements as above, start a signed-in Steam game, and unlock something. The Toast should follow within a second or two.
- **Logs:** Playnite's `extensions.log` has one line on start, "Achievements are read from PlayniteAchievements", or the reason it isn't.
