# Xbox Series Theme

A Playnite 10 Fullscreen-mode add-on that closely reproduces the look and feel of the Xbox Series X|S dashboard, including a system-wide Guide opened by the controller's Guide button.

## Language

**Bundle**:
The single product: the Dashboard theme plus the companion extension that provides the Guide. Shipped as two packages, released together under one version.
_Avoid_: package, mod

**Dashboard**:
The Playnite Fullscreen-mode theme that restyles Playnite's own screens to look like the Xbox Series X|S dashboard.
_Avoid_: skin, home screen (Home is one page of the Dashboard)

**Home**:
The Dashboard's landing page, modelled on the Xbox Home screen.

**Recent row**:
The row of most recently played games on **Home**, ending in the Play history tile.
_Avoid_: Jump back in, MRU

**My games & apps**:
The Dashboard's library page: a left nav (Games, Apps, Groups, Play history, Full library) beside the games it selects. It exists only when the extension is installed.
_Avoid_: library page, collection

**Apps**:
Playnite's Software tools, shown under **My games & apps**. They are not games. Each tile is the App's art, its name in white on a black band over the art's foot while focused: a Microsoft Store app's tile art fills the tile over the accent, any other App's icon sits centred on white with a soft shadow.
_Avoid_: tools, programs

**Queue**:
Every game being installed, as **My games & apps** shows it: those Playnite is installing, and those Steam is installing that Playnite doesn't know of (the Game hub's Install for a Steam game asks Steam itself, without its windows, or the user started one in Steam). It shows the current download first, then those next up. Steam's own queue order can't be read, so next up is the order they started installing. A queued game's tiles read *Queued*, then *Installing...* once its download starts. An install Steam drops (cancelled in Steam, or at its install dialog) leaves the Queue: at once if Steam had started on it, otherwise a minute on.
_Avoid_: downloads, download list

**Manage**:
The page under **My games & apps**' nav line. Its Queue box opens **Manage Queue**.
_Avoid_: Manage game (that is Playnite's own game menu)

**Manage Queue**:
The page listing the **Queue**, each download with its speed, amount and share, below a row of recently installed games. B goes back to **Manage**.
_Avoid_: download manager

**Group**:
A row on **Home** below the **Recent row**, showing the games of one Playnite filter preset. Users don't create Groups in the **Bundle**; they flag filter presets in Playnite.
_Avoid_: pin, collection, category

**Tile menu**:
The menu Menu opens on a game tile on **Home**: Play game (Quit while it runs), Add to / Remove from Favourites, Manage game, which opens Playnite's own game menu, and Customise Home (Customise games & apps on **My games & apps**' tiles), which opens **Settings** on Personalisation. It sits centred over the dimmed screen, drawn like the **Guide**'s menus and prompts.
_Avoid_: context menu, game menu (that is Playnite's own, behind Manage game)

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
The Dashboard's page for the **Bundle**'s own options, modelled on the console's Settings: a left rail (Personalisation, whose Start up box holds the boot video, the profiles and Who's playing today?; Guide; Discord; then **Playnite settings** and More Playnite options) beside the selected category. It exists only when the extension is installed.
_Avoid_: Bundle settings, plugin settings, add-on settings

**Playnite settings**:
Playnite's own Fullscreen Settings window, restyled by the **Dashboard** and reached from **Settings**' rail. It can't hold the Bundle's options.
_Avoid_: Settings (that is the Dashboard's page)

**Play session**:
One continuous run of a game, from launch to exit, as recorded by the extension. Playnite itself keeps only totals.
_Avoid_: playthrough, visit

**Guide**:
The panel, modelled on the Xbox Guide, shown over any foreground window when the Guide button is pressed.
_Avoid_: overlay (the overlay is the window hosting the Guide, not the Guide itself), menu, sidebar

**Overlay**:
The topmost, full-screen, transparent window the extension draws the Guide into, above every other window.

**Status area**:
Pad 1's battery and the clock, shown on **Home**'s top bar and at the **Guide**'s top right alike, and the count of Playnite's notifications, which the Notifications **Utility** wears. In a **Call** it shows a Discord icon too, badged muted or deafened, and lit while the user speaks.
_Avoid_: status bar, tray

**Utility**:
One of the buttons along the bottom of the **Guide**'s landing tab: Notifications (Playnite's own), Audio (Windows' default output and input device, each a dropdown, then master volume and mute, in a **Call** Discord's mute and deafen, and last **Now Playing**) and Search. Selecting Notifications or Audio opens it in a **Flyout**; Search brings Playnite to the front on **Search**.
_Avoid_: widget, quick setting

**Flyout**:
A second panel the **Guide** opens beside and over its own when an entry is selected, modelled on the console's profile flyout. The tab stays behind it, dimmed; the Flyout has the pad until B closes it, focus going back to the entry that opened it.
_Avoid_: submenu, popup, page

**Toast**:
A green banner, as the console's, shown at the bottom of the screen above every window without taking focus, so a running game keeps its input. It is separate from the **Overlay**. The first Toast is a new Playnite notification; an incoming Discord call is the next; the third, the Queued Toast, says "Queued" with the game's cover as each game joins the **Queue**, waiting its turn as a notification's does; the fourth, the Ready to play Toast, says "Ready to play" with the cover as a game the Queue held finishes installing.
_Avoid_: popup, notification (Notifications are Playnite's own messages)

**Launch screen**:
The launching game's art, full screen above every window, as on the console: its background art, or its cover blurred and darkened, chosen as **Home**'s hero chooses it. It fades in as a game is launched from anywhere in the **Bundle**, hides Steam, the Xbox app, Epic's launcher and any splash or update windows behind it, and fades out once the game is running, or the launch is cancelled, fails or times out. It steps aside sooner for a window that needs the user, such as a EULA, a sign-in or an error: a launcher's or game's window that holds the foreground for 5 s, or, at once, Steam saying its launch waits on the user, when Steam's own window, where it asks and which it keeps hidden while it runs in the tray, is put in front; and B, with the **Guide** closed, dismisses it, putting Steam's question in front too should there be one. It never takes focus, so the game gets the pad as soon as it starts. It is separate from the **Overlay**.
_Avoid_: splash, loading screen

**Start-up**:
The console's start-up sequence, shown full screen over Playnite as it starts in Fullscreen, before **Home**: the **Boot video**, **Who's playing today?**, Signing you in, then a **Toast** saying "Hi" and the **Profile**'s first name. It has the pad to itself, the **Guide button** too.
_Avoid_: splash, intro, login screen

**Boot video**:
One console generation's boot animation, from the original Xbox to the Series X, chosen on **Settings**, or Off. Any button skips it.
_Avoid_: intro video, splash

**Profile**:
One person who plays on this PC: a gamertag, a real name and a picture, kept in the extension's encrypted store. It is identity only; the **Bundle**'s options are shared by every Profile. The first is made from the Windows user; the one signed in shows on the **Status area** and the **Guide**'s Profile & system.
_Avoid_: account, user (that is the Windows user), gamer

**Who's playing today?**:
**Start-up**'s profile picker: the **Profiles** in a row, focus on the one signed in last, and A signs the focused one in. It asks on every start and on waking from sleep, unless turned off on **Settings**, when the last Profile signs in by itself.
_Avoid_: profile select, login, user switcher

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
The option on **Settings**' Guide category naming the app **Now Playing** follows: Any (Windows' current media session), Spotify (the default), or any other app that has had a media session since Playnite started. Shared by every **Profile**.
_Avoid_: media source, player setting

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
- A **Toast** never takes focus; pressing the **Guide button** while one is showing opens the **Guide** at the Toast's subject; during a Queued Toast it goes to **Manage Queue** instead, as the Queue isn't in the Guide, and during a Ready to play Toast, whose prompt reads "Press ⓧ to start", it starts the game
- A **Launch screen** never takes focus; the **Guide** and **Toasts** show over it
- The **Guide** has three tabs: landing, the **Discord tab** (once connected, until disconnected), and Profile & system
- Every **Call** makes its channel a **Known channel**, at the front; the first six are the **Discord tab**'s **Shortcuts**
- Someone else joining a known DM the user isn't in rings it: a **Toast** for up to 30 s and Answer atop the **Discord tab** until they hang up or the user joins; a server's voice channel never rings
- Selecting the profile on **Home**'s top bar opens the **Guide** on Profile & system
- **Start-up** plays the **Boot video**, then **Who's playing today?** signs a **Profile** in; its "Hi" **Toast** shows first, Playnite's notifications and a ringing **Call**'s Toast after it, and pressing the **Guide button** during it opens the **Guide** on Profile & system
- Waking from sleep asks **Who's playing today?** again, without the **Boot video**
- **Home**'s top bar, the **Guide**'s Settings entry and the **Tile menu**'s Customise entries all open **Settings**; B returns to where it was opened from

## Flagged ambiguities

- "Full screen overlay" in the original idea meant the **Overlay** window hosting the **Guide** flyout, not a full-screen copy of **Home**.
