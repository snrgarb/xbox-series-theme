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

**Play session**:
One continuous run of a game, from launch to exit, as recorded by the extension. Playnite itself keeps only totals.
_Avoid_: playthrough, visit

**Guide**:
The flyout panel, modelled on the Xbox Guide, shown over any foreground window when the Guide button is pressed.
_Avoid_: overlay (the overlay is the window hosting the Guide, not the Guide itself), menu, sidebar

**Overlay**:
The topmost, full-screen, transparent window the extension draws the Guide into, above every other window.

**Status area**:
The controller's battery and the clock, shown on **Home**'s top bar and at the **Guide**'s top right alike, and the count of Playnite's notifications, which the Notifications **Utility** wears.
_Avoid_: status bar, tray

**Utility**:
One of the buttons along the bottom of the **Guide**'s landing tab: Notifications (Playnite's own) and Audio (Windows master volume and mute). Selecting one shows it in the panel in place of the tab; B goes back.
_Avoid_: widget, quick setting

**Toast**:
A small notice shown above every window without taking focus, so a running game keeps its input. It is separate from the **Overlay**. The first Toast is an incoming Discord call.
_Avoid_: popup, notification (Notifications are Playnite's own messages)

**Discord tab**:
The Guide tab, in the console's Parties & chats slot, that shows the user's Discord voice activity. It appears only once the user has connected their own Discord application.
_Avoid_: party, chats

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
- The **Guide** has three tabs: landing, the **Discord tab** (when connected), and Profile & system
- Selecting the profile on **Home**'s top bar opens the **Guide** on Profile & system

## Flagged ambiguities

- "Full screen overlay" in the original idea meant the **Overlay** window hosting the **Guide** flyout, not a full-screen copy of **Home**.
