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
Playnite's Software tools, shown under **My games & apps**. They are not games.
_Avoid_: tools, programs

**Group**:
A row on **Home** below the **Recent row**, showing the games of one Playnite filter preset. Users don't create Groups in the **Bundle**; they flag filter presets in Playnite.
_Avoid_: pin, collection, category

**Tile menu**:
The menu Menu opens on a game tile on **Home**: Play game (Quit while it runs), Add to / Remove from Favourites, and Manage game, which opens Playnite's own game menu. It sits centred over the dimmed screen, drawn like the **Guide**'s menus and prompts.
_Avoid_: context menu, game menu (that is Playnite's own, behind Manage game)

**Game hub**:
The Dashboard's page for one game, modelled on the Xbox game hub: a header, a row of cards about the game, and its actions. It replaces Playnite's Game details view.
_Avoid_: game page, game card, Manage game

**Achievements page**:
The **Game hub**'s view of one game's achievements, opened from the hub and shown in its place. It exists only when the extension is installed and achievement data is available.
_Avoid_: achievements window, trophies

**Search**:
The Dashboard's page for finding games and **Apps** by name. It exists only when the extension is installed.
_Avoid_: filter (narrowing the list currently shown by name is Playnite's own filter, not Search)

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
One of the buttons along the bottom of the **Guide**'s landing tab: Notifications (Playnite's own) and Audio (Windows master volume and mute, and in a **Call** Discord's mute and deafen). Selecting one opens it in a **Flyout**.
_Avoid_: widget, quick setting

**Flyout**:
A second panel the **Guide** opens beside and over its own when an entry is selected, modelled on the console's profile flyout. The tab stays behind it, dimmed; the Flyout has the pad until B closes it, focus going back to the entry that opened it.
_Avoid_: submenu, popup, page

**Toast**:
A green banner, as the console's, shown at the bottom of the screen above every window without taking focus, so a running game keeps its input. It is separate from the **Overlay**. The first Toast is a new Playnite notification; an incoming Discord call is the next.
_Avoid_: popup, notification (Notifications are Playnite's own messages)

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

**Guide button**:
The Xbox logo button in the centre of an Xbox controller.
_Avoid_: home button, Xbox button

## Relationships

- A **Bundle** contains one **Dashboard** and one extension, which owns the **Overlay**
- The **Overlay** hosts the **Guide**, and nothing else
- Tapping the **Guide button** opens the **Guide** 0.3 s after the press, or closes it on release; holding it for 0.7 s opens Power instead, without showing the **Guide** first
- Selecting a game anywhere on the **Dashboard** opens its **Game hub**
- The extension records **Play sessions**; the **Game hub** shows a game's most recent ones
- A **Toast** never takes focus; pressing the **Guide button** while one is showing opens the **Guide** at the Toast's subject
- The **Guide** has three tabs: landing, the **Discord tab** (once connected, until disconnected), and Profile & system
- Every **Call** makes its channel a **Known channel**, at the front; the first six are the **Discord tab**'s **Shortcuts**
- Someone else joining a known DM the user isn't in rings it: a **Toast** for up to 30 s and Answer atop the **Discord tab** until they hang up or the user joins; a server's voice channel never rings
- Selecting the profile on **Home**'s top bar opens the **Guide** on Profile & system
- **Start-up** plays the **Boot video**, then **Who's playing today?** signs a **Profile** in; its "Hi" **Toast** shows first, Playnite's notifications and a ringing **Call**'s Toast after it, and pressing the **Guide button** during it opens the **Guide** on Profile & system
- Waking from sleep asks **Who's playing today?** again, without the **Boot video**
- **Home**'s top bar, the **Guide**'s Settings entry and the **Tile menu**'s Customise entries all open **Settings**; B returns to where it was opened from

## Flagged ambiguities

- "Full screen overlay" in the original idea meant the **Overlay** window hosting the **Guide** flyout, not a full-screen copy of **Home**.
