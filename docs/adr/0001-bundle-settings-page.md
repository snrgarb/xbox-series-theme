# The Bundle's settings live on a Settings page on the Dashboard

Playnite's Fullscreen Settings window takes no extension sections (`OpenPluginSettings` throws in Fullscreen, #15), so the Bundle's own options (hero mode, tile shape, accent colour, Guide behaviour, Connect Discord) needed a home reachable by controller (#42).

They live on **Settings**, an extension-drawn Dashboard page like **My games & apps** and **Search**, in the console's Settings look: a left rail of categories with the selected one to its right.

- **Rail**: Personalisation (hero, tile shape, accent colour), Guide (its behaviour options), Discord (the Connect checklist, status line and Disconnect; shown before connecting, since connecting starts there), then a divider and Playnite's own entries: **Playnite settings**, which opens Playnite's restyled Fullscreen Settings window, and More Playnite options, which opens Playnite's restyled main menu (Switch to Desktop mode, Update library, Minimise, Exit) as the top bar's Settings button did before.
- **Entry points**: the Home top bar's Settings button and the Guide's Settings entry open Settings (the Guide's brings Playnite to the front first, as Home and My games & apps do). The tile menu's Customise Home and Customise games & apps open it on Personalisation.
- **B** returns to where Settings was opened from: the tile whose Menu opened it, or the top bar's Settings button.
- **Changes apply and save instantly**, as on the console; there is no Save. Discord's Connect is an action, not a setting.
- **Text entry** (Discord's Client ID and Secret) uses the extension's console keyboard from Search (#39), plus a Paste action that fills the field from the Windows clipboard. A QR code links to Discord's developer portal.
- The Dashboard reads the options it needs (hero, tile shape, accent) through `{PluginSettings}`. The Client Secret and tokens stay under DPAPI, as already decided.
- **Without the extension** there is no Settings page: the top bar's Settings button opens Playnite's main menu, as before.

This replaces user story 44 in #19 ("a Settings entry that brings Playnite to the front on Fullscreen settings"): the Guide's Settings entry now lands on Settings, one step from Playnite settings.

## Considered options

- **A Flyout in the Guide**: reachable over a running game, but narrow, and Home's hero and tiles can't be seen changing behind the dimmed tab.
- **Desktop mode's add-on settings, alone or for Discord only**: easy text entry, but no controller access, and Customise Home would have nowhere to go in Fullscreen.
- **Playnite's own keyboard window for the Discord fields**: available now, but its 5×11 layout and lack of paste make a 32-character Secret painful.
- **Rebuilding Playnite's main-menu items as rows**: looks native, but re-implements Playnite commands and goes stale if Playnite adds items.
