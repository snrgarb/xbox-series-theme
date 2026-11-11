# Xbox Series Theme

A Playnite 10 Fullscreen-mode add-on that closely reproduces the look and feel of the Xbox Series X|S dashboard, including a system-wide Guide opened by the controller's Guide button.

## Language

**Bundle**:
The single product: the Dashboard theme plus the companion extension that provides the Guide. Shipped as two packages, each with a version of its own, the two sharing Major.Minor: the theme's .pthm, and the extension's .pext, which packs that same .pthm. Both are in every release, beside the **Boot shell**.
_Avoid_: package, mod

**Dashboard**:
The Playnite Fullscreen-mode theme that restyles Playnite's own screens to look like the Xbox Series X|S dashboard.
_Avoid_: skin, home screen (Home is one page of the Dashboard)

**Home**:
The Dashboard's landing page, modelled on the Xbox Home screen. Behind it is the art of the **Recent row** game that has focus, once focus has rested on it as long as **Settings**' Background delay says, and the green swirl with focus anywhere else. Under its **Recent row**, four wide tiles in the console's store and promo places go somewhere the **Bundle** has instead: Discord (the **Discord tab**, or before one, **Settings** on Theme settings' Discord, focus on its Client ID), Playnite settings (**Settings** on **Playnite settings**, focus on its Playnite settings box), Achievements (the **Guide**'s Profile & system, the **Score** total under it, or with no totals why: "Needs PlayniteAchievements" without it, "Nothing recorded yet" while it has no game's achievements) and Downloads (**Manage Queue**, what the **Queue** holds under it, B back to Home).

**Recent row**:
The row of most recently played games on **Home**, ending in the Play history tile.
_Avoid_: Jump back in, MRU

**My games & apps**:
The Dashboard's library page: a left nav (Games, Apps, Play history, Full library, Manage) beside what it selects. **Groups** aren't in the nav: Full library's tabs are the user's filter presets, and See all at the end of a Group on **Home** opens Full library on that Group's preset. It exists only when the extension is installed.
_Avoid_: library page, collection

**Apps**:
Playnite's Software tools, shown under **My games & apps**. They are not games. Each tile is the App's art, its name in white on a black band over the art's foot while focused: a Microsoft Store app's tile art fills the tile over the accent, any other App's icon sits centred on white with a soft shadow.
_Avoid_: tools, programs

**Queue**:
Every game being installed, as **My games & apps** shows it: those Playnite is installing, and those Steam is installing that Playnite doesn't know of (the Game hub's Install for a Steam game asks Steam itself, without its windows, or the user started one in Steam). It shows the current download first, then those next up. Steam's own queue order can't be read, so next up is the order they started installing. A queued game's tiles read *Queued*, then *Installing...* once its download starts. An install Steam drops (cancelled in Steam, or at its install dialog) leaves the Queue: at once if Steam had started on it, otherwise a minute on.
_Avoid_: downloads, download list

**Manage**:
The page under **My games & apps**' nav line. Its Queue box opens **Manage Queue**.
_Avoid_: Manage game (that is the **Tile menu**'s way to the **Game hub**)

**Manage Queue**:
The page listing the **Queue**, each download with its speed, amount and share, below a row of recently installed games. View opens the focused game's **Tile menu**. B goes back to **Manage**.
_Avoid_: download manager

**Group**:
A row on **Home** below the **Recent row**, showing the games of one Playnite filter preset. Users don't create Groups in the **Bundle**; they flag filter presets in Playnite.
_Avoid_: pin, collection, category

**Tile menu**:
The theme's one menu for a game, with the console's main rows. Menu opens it on a game tile on **Home** and **My games & apps**, and on Playnite's own grid there; so do the **Game hub**'s Manage and Menu, without the Manage game row, as the player is there already, and **Manage Queue**'s View, on its rows and Recently installed covers. Focus stays on what opened it, so closing it leaves focus there. Its rows: Play game (Install while the game isn't installed, through its client without its windows where it can, else Playnite's own; Quit while it runs), Add to / Remove from Favourites, Manage game, which opens the game's **Game hub**, and Uninstall, for an installed game a library brought in (not one added by hand), while it isn't running, which asks "Uninstall <game>?" first (Uninstall / Cancel), then uninstalls as Playnite's game menu's Uninstall does; then, after a gap as the console's, Customise Home (Customise games & apps on **My games & apps**' tiles), which opens **Settings** on Theme settings' Personalisation, and More options. It sits centred over the dimmed screen, drawn like the **Guide**'s menus and prompts. More options opens a second panel, drawn alike, over the menu and further right, as the **Guide**'s **Flyout** sits beside its tab, the menu dimmed behind it: a row for each of the game's actions that don't play it (as "Launch config tool"), Hide this game or Unhide this game, Enable HDR or Disable HDR on a display that can show HDR, Extensions, which opens Playnite's own Extensions picker for the game, and Remove, which asks "Remove <game>?" (Remove / Cancel) and takes the game out of the library, a library's game kept out of its next update; one installing, running or uninstalling is refused with Playnite's own error. Playnite's Edit fields isn't offered. B steps back one level at a time: from a prompt to the panel that asked it, focus on the row that asked, from the flyout to the menu on More options, then out.
_Avoid_: context menu, game menu (that is Playnite's own, which the theme no longer shows; without the extension, Menu and Manage still open it)

**Game hub**:
The Dashboard's page for one game, modelled on the Xbox game hub: a header, a row of cards about the game, and its actions. It replaces Playnite's Game details view.
_Avoid_: game page, game card, Manage game

**Achievements page**:
The **Game hub**'s view of one game's achievements, opened from the hub and shown in its place. It exists only when the extension is installed and achievement data is available.
_Avoid_: achievements window, trophies

**Search**:
The Dashboard's page for finding games and **Apps** by name, typed on the **Console keyboard**. Empty, it shows the games most recently added; typing shows a Games row and an Apps row, each only with something found, never hidden games. A on a game opens its **Game hub**; A on an App starts it. Home's top bar, the **Guide**'s Search **Utility**, Y on Home and Y on My games & apps' Games open it; Playnite's own keyboard and its filter and preset lists never come up over the **Dashboard**. B puts the keyboard away first, then leaves the page, back to Games on the game Y was pressed on when opened from there. It exists only when the extension is installed.
_Avoid_: filter (narrowing the list currently shown by name is Playnite's own filter, not Search)

**Console keyboard**:
The extension's on-screen keyboard, laid out as the console's: the number row, QWERTY, a symbols key, the cursor's left and right, Shift, Enter and a space bar, with the pad's shortcuts: X backspace, Y space, LB and RB the cursor, Start Enter. It is how every text the **Bundle** asks for is typed: on **Search**, and elsewhere (a **Profile**'s gamertag and real name, Discord's Client ID and Secret, Full library's name filter) over the dimmed page under a caption and its field, Start keeping the text and B dropping it.
_Avoid_: on-screen keyboard (Playnite's own, as `SelectString` shows it), OSK

**Settings**:
The Dashboard's page for the **Bundle**'s own options, modelled on the console's Settings: a left rail (Theme settings and **Playnite settings**, then under a divider More Playnite options, an action rather than a category, and in Debug builds a divider and the Debug menu) beside the selected category. Theme settings is boxes two wide, a line icon and a title each, that open their pages in its place: Personalisation, whose boxes are the boot video, the profiles and Who's playing today?, then Background delay, a checkbox A turns over in place (on, as at first, **Home**'s background waits for focus to rest 1.5 s on a **Recent row** game before showing its art; off, it follows focus after 150 ms), and under it Reduce motion, another (off at first; on, for a PC the animations cost frames on, the **Dashboard** stops moving: the swirl stands still, pages, rows, tiles, the **Guide** and menus show in place rather than sliding, flying or growing, a **Toast** only fades, and **Home**'s background crossfades in 150 ms rather than 400; the boot video, the loading spinners, the **Idle screen**, the **Launch screen** and an achievement's **Toast** move as ever); Idle, whose page has the **Idle screen**'s two dropdowns, Dim after and Show things after; Guide, whose page has Guide, a checkbox A turns over in place (on at first; off, the **Guide** never shows: the **Guide button** does nothing in the extension, a hold neither, so Steam or another app can have it; **Home**'s profile and Achievements tile do nothing and its Discord tile opens Settings on Discord; a **Toast** has no "Press ⓧ to view"; the **Idle screen** leaves out its tips about the Guide; and a prompt that would show over the Guide, as "Quit X and start Y?", shows on its own, closing as it is answered), then the **Now Playing source**; and Discord. B steps back one level at a time (Theme settings › Personalisation › Boot video), focus on the box it came from, then from the rail to where the page was opened from. It exists only when the extension is installed.
_Avoid_: Bundle settings, plugin settings, add-on settings

**Playnite settings**:
The category on **Settings**' rail, under Theme settings, for what the **Bundle** does with Playnite itself, boxes two wide as Personalisation's: Download metadata, which fills in the games missing metadata, blank fields only, under Playnite's own progress bar, B stopping it; Update after downloads, a box with a checkbox, on by default, which has a game finishing downloading followed by a library update across every integration and metadata for the games it brings, once no game is running, ending with the Library updated **Toast**; and Playnite settings, which opens Playnite's own Fullscreen Settings window, restyled by the **Dashboard**. That window can't hold the Bundle's options.
_Avoid_: Settings (that is the Dashboard's page)

**Play session**:
One continuous run of a game, from launch to exit, as recorded by the extension. Playnite itself keeps only totals.
_Avoid_: playthrough, visit

**Guide**:
The panel, modelled on the Xbox Guide, shown over any foreground window when the Guide button is pressed. **Settings**' Guide page can turn it off, when it never shows.
_Avoid_: overlay (the overlay is the window hosting the Guide, not the Guide itself), menu, sidebar

**Overlay**:
The topmost, full-screen, transparent window the extension draws the Guide into, above every other window.

**Status area**:
The PC's own battery on a laptop or handheld (its percent, a cell filled to it, and a bolt while it charges; nothing on a desktop), pad 1's battery and the clock, shown on **Home**'s top bar and at the **Guide**'s top right alike, and the count of Playnite's notifications, which the Notifications **Utility** wears. In a **Call** it shows a Discord icon too, badged muted or deafened, and lit while the user speaks.
_Avoid_: status bar, tray

**Utility**:
One of the buttons along the bottom of the **Guide**'s landing tab: Notifications (Playnite's own), Audio (Windows' default output and input device, each a dropdown, then master volume and mute, in a **Call** Discord's mute and deafen, and last **Now Playing**) and Search. Selecting Notifications or Audio opens it in a **Flyout**; Search brings Playnite to the front on **Search**.
_Avoid_: widget, quick setting

**Flyout**:
A second panel the **Guide** opens beside and over its own when an entry is selected, modelled on the console's profile flyout. The tab stays behind it, dimmed; the Flyout has the pad until B closes it, focus going back to the entry that opened it.
_Avoid_: submenu, popup, page

**Toast**:
A green banner, as the console's, shown at the bottom of the screen above every window without taking focus, so a running game keeps its input. It is separate from the **Overlay**. The first Toast is a new Playnite notification; an incoming Discord call is the next; the third, the Queued Toast, says "Queued" with the game's cover as each game joins the **Queue**, waiting its turn as a notification's does; the fourth, the Ready to play Toast, says "Ready to play" with the cover as a game the Queue held finishes installing; the fifth, the Uninstalled Toast, says "<Game> was uninstalled" with the cover once Steam has finished a game uninstalled from the **Tile menu** or Playnite's game menu; the sixth, the Achievement unlocked Toast, is the console's pill, in green, as PlayniteAchievements records an unlock in the game running now, or the one that just exited, since it started, waiting its turn as a notification's does: the achievement's icon (or a trophy) in its circle, "Achievement unlocked", its **Score** and name, then its description, two lines at a time and never cut short, then the prompt, for 8.5 s rather than 5 and 3 s more for each further page of a long description, the next Toast waiting for its pill to collapse. Several recorded together (heard within a second of each other, or found in one refresh) are one Toast, the same pill saying "<n> achievements unlocked". One **Rare** unlock told of alone gets the Achievement unlocked Toast's rare form, the console's: the same pill in blue with a spinning diamond, saying "Rare Achievement unlocked - <n>%", with a sound of its own, the pill collapsing as the sound's closing flourish starts, 8.45 s in, however long its description, whose pages are held for less; several together are the ordinary "<n> achievements unlocked" even with a Rare one among them. What PlayniteAchievements already had as Playnite started, and an older session's unlocks it finds on a resync, are never told of. The seventh, the Library updated Toast, says "Library updated" with a glyph, no cover, and under it "<n> games added" when the update added any, as the update after a download (**Playnite settings**' Update after downloads) finishes, waiting its turn as a notification's does; several downloads finishing together get one.
_Avoid_: popup, notification (Notifications are Playnite's own messages)

**Launch screen**:
The launching game's art, full screen above every window, as on the console: its background art, or its cover blurred and darkened, chosen as **Home**'s hero chooses it. It fades in as a game is launched from anywhere in the **Bundle**, hides Steam, the Xbox app, Epic's launcher and any splash or update windows behind it, and fades out once the game is running, or the launch is cancelled, fails or times out. It steps aside sooner for a window that needs the user, such as a EULA, a sign-in or an error: a launcher's or game's window that holds the foreground for 5 s, or, at once, one of Playnite's own dialogs, as its error on a launch that failed, or Steam saying its launch waits on the user, when Steam's own window, where it asks and which it keeps hidden while it runs in the tray, is put in front; and B, with the **Guide** closed, dismisses it, putting Steam's question in front too should there be one. It never takes focus, so the game gets the pad as soon as it starts. It is separate from the **Overlay**.
_Avoid_: splash, loading screen

**Boot shell**:
This TV PC's own sign-in shell, which Windows starts in explorer's place and which brings Playnite up to **Start-up**, then keeps Fullscreen up: it starts Playnite again after a crash, and falls back to the desktop when Playnite crashes three times in five minutes. It is this PC's alone, not part of the **Bundle**.
_Avoid_: the tool, launcher (a launcher is Steam's, Epic's or the Xbox app's)

**Start-up**:
The console's start-up sequence, shown full screen over Playnite as it starts in Fullscreen, before **Home**: the **Boot video**, **Who's playing today?**, Signing you in, then a **Toast** saying "Hi" and the **Profile**'s first name. It has the pad to itself, the **Guide button** too.
_Avoid_: splash, intro, login screen

**Boot video**:
One console generation's boot animation, from the original Xbox to the Series X, chosen on **Settings**, or Off. Any button skips it.
_Avoid_: intro video, splash

**Profile**:
One person who plays on this PC: a gamertag, a real name and a picture, kept in the extension's encrypted store. It is identity only; the **Bundle**'s options are shared by every Profile. The first is made from the Windows user; the one signed in shows on the **Status area**, with the **Score** total under its gamertag on **Home**'s top bar, and on the **Guide**'s Profile & system, with **Switch profile** under it and the achievement totals under that: the **Score** total, the achievements unlocked and the last three unlocks, newest first. Those come from this PC's library, as PlayniteAchievements keeps nothing per Profile, so every Profile shows the same totals.
_Avoid_: account, user (that is the Windows user), gamer

**Who's playing today?**:
**Start-up**'s profile picker: the **Profiles** in a row, focus on the one signed in last, and A signs the focused one in. It asks on every start and on waking from sleep, unless turned off on **Settings**, when the last Profile signs in by itself; **Switch profile** asks it whatever that setting says.
_Avoid_: profile select, login, user switcher

**Switch profile**:
The **Guide**'s entry under the profile card on Profile & system: the Guide closes and **Who's playing today?** asks, without the **Boot video**, even when it is turned off for start and waking.
_Avoid_: sign out, change user, user switcher

**Idle screen**:
What shows when nobody has touched the controller, keyboard or mouse for a while, anywhere in Windows, over Playnite, a running game or any other app in front alike (a controller counting outside Playnite as XInput sees it, sticks and triggers included, so not a wheel or a pad a game reads in its own way), in stages counted from the last input: Dimmed, after Dim after (2 min by default), a dim, 70% black, fading in over everything on screen, the **Guide**, the **Tile menu**, dialogs and the **Console keyboard** included, with only **Toasts** above it; then Showing things, after Show things after (5 min by default), the console's idle panel over the dim: a full-height panel taking the right half of the screen, sliding in from the right, showing one card at a time, each for 20 s in shuffled turn and reshuffled when they run out: the nearly-finished games (the most achievements unlocked without all: the game and the locked achievement most players have, its **Score**, the game's progress), the last unlocks ("Unlocked 2 days ago", the achievement's Score and icon), the last games played ("Played yesterday", the playtime) and tips (their buttons drawn as icons in the text and highlighted on a controller), the achievement ones only with PlayniteAchievements. Y on a game's card, "See game", wakes the screen and opens that game's **Game hub**, the **Guide** closing first if it was up, but while a game runs only wakes it, the game keeping the front; Y on a tip, "Got it", only wakes it. Both times are set on Theme settings' Idle page, each a dropdown of the same steps, 1, 2, 5, 10, 15 and 30 min, 1, 2, 3 and 4 h, and Never; Show things after is never shorter than Dim after, so its dropdown leaves out the steps below Dim after, and raising Dim after past it raises it with it. They set the timers at once, counting from then, are saved, and are shared by every **Profile**. Dim after on Never turns the Idle screen off, Show things after following it to Never, and Show things after on Never only dims; a Show things after equal to Dim after shows things and the dim together. The first input while dimmed is the wake press: it only wakes the screen, swallowed, so A doesn't launch, B doesn't go back and the **Guide button** doesn't open the **Guide**, focus staying where it was (but for Y on a game's card), and **Who's playing today?** isn't asked; outside Playnite, over a game or another app, the wake press reaches that game or app too, nothing there being Playnite's to swallow. Input outside Playnite's windows, a pad's sticks and triggers and a pad held off its rest included, is looked for every second, so a held accelerator keeps the screen from dimming and a dimmed screen there wakes within a second. It waits only during **Start-up** and a **Launch screen**, counting from zero again once that ends, so a game launched counts from its Launch screen going, and a game stopping changes nothing; Toasts neither wake it nor count as input.
_Avoid_: screensaver, sleep (that is Windows'), attract mode

**Spinner**:
The console's loading spinner: a white arc with a green tail that grows clockwise from the top, its tail chasing its head round. In the **Dashboard**'s top bar it rests as a dot at the top before each turn; round the **Profile**'s picture and in the **Guide**'s **Flyouts** it turns on without a rest, its ends cut square. It turns round the **Profile**'s picture on Signing you in, and in the **Dashboard**'s top bar while Playnite updates the library, and in a **Flyout** while its entries load.
_Avoid_: loading ring, progress ring, light

**Discord tab**:
The Guide tab, in the console's Parties & chats slot, that shows the user's Discord voice activity. It appears once the user has connected their own Discord application and stays until they disconnect it: while it can't show Discord it says only why: "Connecting to Discord" while a connect is under way, and "Discord isn't running" while Discord can't be reached and the extension keeps trying it, backing off, until it can sign in again by itself; once nothing is trying Discord, as when it no longer can sign in by itself, it says "Reconnect Discord in settings".
_Avoid_: party, chats

**Call**:
The Discord voice channel, DM or group DM the user is in, as Discord reports it: its name and the people in it. Mute, Deafen and Leave are asked of Discord and shown done only once Discord says so.
_Avoid_: party, voice chat, session (a **Play session** is a game's)

**Known channel**:
A DM, group DM or server voice channel the user has been in a **Call** in, on this PC or another device, remembered most recently joined first in the extension's encrypted store. Known DMs are kept until removed; a server's voice channel only while among the six most recent.
_Avoid_: favourite, recent, contact

**Shortcut**:
A tile on the **Discord tab** for one of the six most recently joined **Known channels**, in rows of three: A rejoins it in one press, and Remove shortcut, from its menu, forgets it.
_Avoid_: pin, favourite, quick join

**Servers**:
The **Discord tab**'s last entry, a **Flyout** of the user's Discord servers, those the user has joined a **Call** in first, most recently joined first (every one remembered, however far its **Known channels** have fallen), then the rest by name; a server opens onto its voice channels, each with the avatars of the people in it or Empty, and A on one joins it.
_Avoid_: guilds (Discord's API name), browse

**Now Playing**:
The last entry of the Guide's Audio **Flyout**, under the mute switches after a divider, showing the media Windows' flyout shows: the track's title and artist, its album art, the app playing it, by the name people know it by, whether it plays or is paused, and a thin bar of how far into the track it is (hidden for media with no length, as a live stream), following its session live while the flyout shows. The session is the **Now Playing source**'s while that app has one, else Windows' current. Under the card sit Previous, Play/Pause and Next, which ask the app and show nothing changed until it says so; one the app has turned off is dimmed. It is always there; with no media playing it says "No music app is running", with no controls. There is no Now Playing tab.
_Avoid_: media player, music

**Now Playing source**:
The option on **Settings**' Guide page, under Theme settings, naming the app **Now Playing** follows: Any (Windows' current media session), Spotify (the default), or any other app that has had a media session since Playnite started. Shared by every **Profile**.
_Avoid_: media source, player setting

**Rare**:
An achievement fewer than 10% of players have unlocked, as the console's rule; one whose rarity isn't known never is. Told of alone, its unlock gets the Achievement unlocked **Toast**'s rare form.
_Avoid_: ultra rare, legendary

**Score**:
What an achievement is worth: its platform's own points where PlayniteAchievements records them (Xbox's Gamerscore, RetroAchievements' points, Epic's XP), else from its rarity, the share of players who have unlocked it, as Steam, GOG and PSN have no points: 1% or less is 100, 5% or less 50, 10% or less 30, 25% or less 20, 50% or less 15, and anything commoner, or of unknown rarity, 10. The Achievement unlocked **Toast** shows it, and the **Profile**'s totals add it up over every platform's games.
_Avoid_: Gamerscore (that is Xbox's own, only part of the total), points (that is a platform's own, which a Score may or may not be)

**Guide button**:
The Xbox logo button in the centre of an Xbox controller.
_Avoid_: home button, Xbox button

## Relationships

- A **Bundle** contains one **Dashboard** and one extension, which owns the **Overlay**
- The **Overlay** hosts the **Guide**, and nothing else
- Tapping the **Guide button** opens the **Guide** 0.3 s after the press, or closes it on release; holding it for 0.7 s opens Power instead, without showing the **Guide** first
- Selecting a game anywhere on the **Dashboard** opens its **Game hub**
- A game exiting, however it was quit, returns to **Home** from wherever it was launched, the Recent row focused on that game; a game quit from the **Guide** closes the **Guide** as it goes
- The extension records **Play sessions**; the **Game hub** shows a game's most recent ones
- A **Toast** never takes focus; pressing the **Guide button** while one is showing opens the **Guide** at the Toast's subject; during a Queued Toast it goes to **Manage Queue** instead, as the Queue isn't in the Guide, during a Ready to play Toast, whose prompt reads "Press ⓧ to start", it starts the game, during an Uninstalled Toast it opens the Guide, and during an Achievement unlocked Toast, **Rare** or not, it opens the Guide on Profile & system, where the **Profile**'s totals and last unlocks are
- A **Launch screen** never takes focus; the **Guide** and **Toasts** show over it
- The **Guide** has three tabs: landing, the **Discord tab** (once connected, until disconnected), and Profile & system
- Every **Call** makes its channel a **Known channel**, at the front; the first six are the **Discord tab**'s **Shortcuts**
- Someone else joining a known DM the user isn't in rings it: a **Toast** for up to 30 s and Answer atop the **Discord tab** until they hang up or the user joins; a server's voice channel never rings
- Selecting the profile on **Home**'s top bar opens the **Guide** on Profile & system
- The **Boot shell** owns everything from sign-in to **Start-up**'s first frame; the **Bundle** owns everything from it, but for the Boot shell starting explorer, then Steam, Discord and Spotify, as the **Boot video** ends, every other startup app following them
- On this PC, Exit Playnite leaves the desktop, and holding View+Menu for 5 s closes Playnite to it from anywhere; while no Fullscreen runs, Playnite closed or in Desktop mode, the **Guide button** brings Fullscreen back through the **Boot shell**
- The extension runs only while Playnite's Fullscreen shows the **Dashboard**; in Desktop mode, or on another Fullscreen theme, it stays dormant, and no **Start-up**, **Guide**, **Toast**, **Launch screen**, **Idle screen** or Bundle sound shows over Playnite, every launch left to Playnite
- **Start-up** plays the **Boot video**, then **Who's playing today?** signs a **Profile** in; its "Hi" **Toast** shows first, Playnite's notifications and a ringing **Call**'s Toast after it, and pressing the **Guide button** during it opens the **Guide** on Profile & system
- Waking from sleep asks **Who's playing today?** again, without the **Boot video**; waking the **Idle screen** never does
- **Switch profile** on the **Guide** asks **Who's playing today?** too, also without the **Boot video**, whether or not it asks on start
- **Home**'s top bar, the **Guide**'s Settings entry and the **Tile menu**'s Customise entries all open **Settings**; B returns to where it was opened from

## Flagged ambiguities

- "Full screen overlay" in the original idea meant the **Overlay** window hosting the **Guide** flyout, not a full-screen copy of **Home**.
- "The desktop" is Windows' own, explorer's, which the **Boot shell** falls back to; Playnite's Desktop mode is Playnite in its other mode, not the desktop.
- "**Start-up**'s first frame" is the first frame that shows something: the **Boot video**'s first frame, or **Who's playing today?**'s with the Boot video Off. Start-up's window may draw black before it, and that isn't it.
- "Cold boot", the boot the **Boot shell** is timed on, is a full boot: a Restart, or a power-on with Fast Startup off. Fast Startup's hybrid boot isn't one, nor is waking from sleep.
