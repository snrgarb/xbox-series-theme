# The Bundle's settings live on a Settings page on the Dashboard

Playnite's Fullscreen Settings window takes no extension sections (`OpenPluginSettings` throws in Fullscreen, #15), so the Bundle's own options (hero mode, tile shape, accent colour, Guide behaviour, Connect Discord) needed a home reachable by controller (#42).

They live on **Settings**, an extension-drawn Dashboard page like **My games & apps** and **Search**, in the console's Settings look: a left rail of categories with the selected one to its right.

- **Rail**: Theme settings, a grid of boxes as the console's, each opening its page in the pane, B stepping back a page at a time (#86): Personalisation (boxes too: the boot video, the **Profile**s and whether Who's playing today? asks, each a box opening its own page, Background delay, a checkbox for whether Home's hero waits on a game before showing its art, #147, and Reduce motion, a checkbox that turns the Dashboard's animations off, #156; later hero, tile shape, accent colour), Idle (the **Idle screen**'s Dim after and Show things after, two dropdowns of the same steps from 1 min to 4 h and Never, 2 min and 5 min by default, Show things after never shorter than Dim after, shared by every **Profile** and saved, #90), Guide (its behaviour options) and Discord (the Connect checklist, status line and Disconnect; shown before connecting, since connecting starts there). Then Playnite's own entries, with no divider between them and Theme settings since #86: **Playnite settings**, a page of boxes as Personalisation's (Download metadata, Update after downloads, and a box opening Playnite's restyled Fullscreen Settings window, #82), and under a divider, as an action rather than a category, More Playnite options, which opens Playnite's restyled main menu (Switch to Desktop mode, Update library, Minimise, Exit) as the top bar's Settings button did before.
- **Entry points**: the Home top bar's Settings button and the Guide's Settings entry open Settings (the Guide's brings Playnite to the front first, as Home and My games & apps do). The tile menu's Customise Home and Customise games & apps open it on Theme settings' Personalisation, focus on its boxes; Home's Discord tile, before Discord is connected, on Theme settings' Discord, focus on its Client ID.
- **B** returns to where Settings was opened from: the tile whose Menu opened it, or the top bar's Settings button.
- **Changes apply and save instantly**, as on the console; there is no Save. Discord's Connect is an action, not a setting.
- **Text entry** (Discord's Client ID and Secret) uses the extension's console keyboard from Search (#39), plus a Paste action that fills the field from the Windows clipboard. A QR code links to Discord's developer portal. A **Profile**'s gamertag and real name use it too, as does Full library's name filter: every text the Bundle asks for is typed on the console keyboard, over the dimmed page, in place of Playnite's own keyboards.
- The Dashboard reads the options it needs (hero, tile shape, accent) through `{PluginSettings}`. The Client Secret and tokens stay under DPAPI, as already decided.
- **Without the extension** there is no Settings page: the top bar's Settings button opens Playnite's main menu, as before.

This replaces user story 44 in #19 ("a Settings entry that brings Playnite to the front on Fullscreen settings"): the Guide's Settings entry now lands on Settings, two steps from Playnite's Fullscreen Settings window, by Playnite settings' box.

## Considered options

- **A Flyout in the Guide**: reachable over a running game, but narrow, and Home's hero and tiles can't be seen changing behind the dimmed tab.
- **Desktop mode's add-on settings, alone or for Discord only**: easy text entry, but no controller access, and Customise Home would have nowhere to go in Fullscreen.
- **Playnite's own keyboard window for the Discord fields**: available now, but its 5×11 layout and lack of paste make a 32-character Secret painful.
- **Rebuilding Playnite's main-menu items as rows**: looks native, but re-implements Playnite commands and goes stale if Playnite adds items.
