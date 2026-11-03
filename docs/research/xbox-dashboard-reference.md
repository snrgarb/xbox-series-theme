# Current Xbox Series X|S dashboard: online reference (2025–2026)

Research for #5 (part of #1). Researched 2026-10-13.

**Gist:** As of October 2026 the Series X|S Dashboard is still the 2023 "new Home" layout: a full-bleed background, a quick-access top bar, and a "Jump back in" row of recent tiles near the bottom, with pinnable groups below it. It has moved toward heavy personalisation since then. Up to 10 Home groups, custom hue/shade accent colours (also tinting the Guide), up to 3 pinned recent tiles, a Play History tile and tab, game hubs on tile select, a new green glass Xbox logo with a new boot animation and sound, and an Insider-only "immersive background" mode. Public sources describe features and settings paths well. They say almost nothing quantitative about focus visuals, motion timing, type sizes, colour values or sounds, and those need a real-console capture (#7).

## Sources and confidence

- **Primary:** Xbox Wire (news.xbox.com) feature and Insider posts. These are first-party and most claims below trace to them. Xbox Wire images are linked, not embedded.
- **Primary but unreadable by tooling:** support.xbox.com articles are a JS-rendered SPA (the raw HTML is an empty `#root`), so I could not read them directly. Where a claim relies on support-article wording seen only through search snippets or secondary coverage, it is marked **(secondary)**.
- **Secondary:** press coverage (TechRadar, PureXbox, VGC, Windows Central, etc.), used only where Xbox Wire is silent. These claims are marked.
- Claims marked **unverified** have no source I could confirm.

## Timeline of relevant dashboard changes

| Date | Change | Source |
|---|---|---|
| 2020-08-25 | Series X|S visual refresh: new tile shape, fonts and focus indicator. Guide gets a landing page, reorderable tabs, a utilities row at the bottom and a unified notifications inbox | [Xbox Wire](https://news.xbox.com/en-us/2020/08/25/xbox-one-and-xbox-series-x-new-digital-experience/) |
| 2020-04 | Guide tabs restructured: "Parties & chats" merged; settings/audio/power moved into "Profile & system" (gamerpic tab) | [Xbox Wire](https://news.xbox.com/en-us/2020/04/06/xbox-insiders-new-features-april-update/) |
| 2022-04 | "Mute navigation sounds" / "Mute notification sounds" options added | [PureXbox (secondary)](https://www.purexbox.com/news/2022/04/new-xbox-feature-allows-you-to-mute-system-sound-effects) |
| 2023-05-01 → 2023-07-26 | New Home: quick-access top menu, more background visible, smaller tiles moved to the bottom, background follows the focused game | [Insider post](https://news.xbox.com/en-us/2023/05/01/xbox-insiders-your-feedback-shapes-the-new-home-experience/), [launch post](https://news.xbox.com/en-us/2023/07/26/welcome-to-your-new-xbox-home/) |
| 2025-05-16 / 2025-06-23 | Recent list: hide system apps, pin up to 3, reduce tile count. Game hubs open on tile select | [Insider](https://news.xbox.com/en-us/2025/05/16/xbox-insiders-get-new-ways-to-personalize-home/), [June update](https://news.xbox.com/en-us/2025/06/23/xbox-june-update-copilot-for-gaming-aggregated-gaming-library/) |
| 2025-07-21 | Cross-device Play History tile at the end of "Jump back in" | [Xbox Wire](https://news.xbox.com/en-us/2025/07/21/now-available-for-xbox-insiders-your-cross-device-play-history-and-easier-access-to-your-cloud-playable-console-games-on-pc/) |
| 2026-03-18 → 2026-04-30 | Up to 10 Home groups; custom colour (hue/shade sliders) with a Guide accent; per-game Quick Resume; profile badges in Guide; Play History tab in My games & apps | [Insider](https://news.xbox.com/en-us/2026/03/18/xbox-insiders-april-2026-console-features/), [March update](https://news.xbox.com/en-us/2026/03/25/march-2026-xbox-update/), [April update](https://news.xbox.com/en-us/2026/04/30/april-xbox-update-2026/) |
| 2026-04-23 / 2026-05-13 | New green glass-orb Xbox logo; new boot animation and sound | [VGC (secondary)](https://www.videogameschronicle.com/news/asha-sharmas-xbox-overhaul-continues-as-revamped-series-xs-bootup-revealed/), [Xbox Wire Insider](https://news.xbox.com/en-us/2026/05/13/xbox-insiders-june-2026-console-features/) |
| 2026-05-29 | Hex colour entry and "match my gamerpic" (announced as "soon"); "What's New" in Home top nav; service status indicator top-right | [Xbox Wire](https://news.xbox.com/en-us/2026/05/29/xbox-insiders-july-2026-console-features/) |
| 2026-06-10 | Separate Home and My games & apps personalisation sections; poster-style library art; selectable status icons | [Xbox Wire](https://news.xbox.com/en-us/2026/06/10/xbox-insiders-july-2026-console-features-2/) |
| 2026-06-24 | Game hubs for Xbox 360 games; wishlist from the game card; cloud-play while updating | [Xbox Wire](https://news.xbox.com/en-us/2026/06/24/xbox-insiders-august-2026-console-features/) |
| 2026-07-15 | General release (OS 10.0.26100.8866) shipping the May–June Insider items | [ElevenForum (secondary)](https://www.elevenforum.com/t/xbox-console-july-15-2026-system-update-os-version-10-0-26100-8866-available.48163/), [Neowin (secondary)](https://www.neowin.net/news/new-xbox-console-update-brings-longer-gamertags-library-customization-and-more/) |
| 2026-08-19 | Remove items from the recent list via the Menu button; **immersive background mode** (right-stick click) | [Xbox Wire](https://news.xbox.com/en-us/2026/08/19/xbox-insiders-october-2026-console-features-2/) |
| 2026-09-09 | Choice of startup animation (Settings > System > Startup); local text search in My games & apps and achievements; 5-badge profile showcase | [Xbox Wire](https://news.xbox.com/en-us/2026/09/09/xbox-insiders-october-2026-console-features-3/) |

Insider items from August and September 2026 may still be Insider-only on retail consoles. I found no Xbox Wire general-release post confirming them.

## Home

### Background / hero
- Home gives most of the screen to the user's background. The 2023 redesign reduced tile sizes and moved tiles toward the bottom "to make more space for you to see your background". [Xbox Wire 2023-05](https://news.xbox.com/en-us/2023/05/01/xbox-insiders-your-feedback-shapes-the-new-home-experience/). Hero reference image: [Top-of-Home hero](https://xboxwire.thesourcemediaassets.com/sites/2/2023/07/01_Top-of-Home_Hero-d0abc3e44819903010b2.jpg), [2023 experiment image](https://xboxwire.thesourcemediaassets.com/sites/2/2023/04/0501-New-Dashboard-experiment_Top-of-Home-FINAL-72653034f196251c18d6.png).
- **Responsive game art:** an option makes the background change to the art of the focused tile in the recent list. [Xbox Wire 2023-07](https://news.xbox.com/en-us/2023/07/26/welcome-to-your-new-xbox-home/). Neither post describes the crossfade timing.
- Background types: solid colour, custom image, and **dynamic (animated) backgrounds** on Series X|S. New game-themed dynamic backgrounds ship most months, e.g. [June 2025](https://news.xbox.com/en-us/2025/06/23/xbox-june-update-copilot-for-gaming-aggregated-gaming-library/) and [April 2026](https://news.xbox.com/en-us/2026/04/30/april-xbox-update-2026/). The new green Xbox logo shipped as a dynamic background around 2026-05-01 [(secondary)](https://www.videogameschronicle.com/news/asha-sharmas-xbox-overhaul-continues-as-revamped-series-xs-bootup-revealed/). Built-in dynamic set (secondary, dated): [Windows Central list](https://windowscentral.com/list-xbox-series-x-series-s-dynamic-animated-themes).
- Since July 2026, dark/light theme, custom backgrounds and some dynamic backgrounds work offline [(secondary)](https://www.digitalcitizen.life/xbox-july-2026-update-adds-15-character-gamertags-library-customisation-and-service-alerts/).
- **Immersive background mode (Insider, Aug 2026):** pressing the right thumbstick anywhere on Home fades the UI out so the background fills the screen; "press any button to return to Home". [Xbox Wire](https://news.xbox.com/en-us/2026/08/19/xbox-insiders-october-2026-console-features-2/)

### Top bar
- A quick-access menu at the very top of Home links to **Library (My games & apps), Microsoft Store, Xbox Game Pass, Search, Settings**. [Xbox Wire 2023-05](https://news.xbox.com/en-us/2023/05/01/xbox-insiders-your-feedback-shapes-the-new-home-experience/), [2023-07](https://news.xbox.com/en-us/2023/07/26/welcome-to-your-new-xbox-home/)
- **"What's New"** entry in Home's top navigation after a system update opens the release notes (2026). [Xbox Wire 2026-05-29](https://news.xbox.com/en-us/2026/05/29/xbox-insiders-july-2026-console-features/)
- **Service status indicator** in the upper-right corner appears only during a confirmed outage. Selecting it opens a status page. [same post](https://news.xbox.com/en-us/2026/05/29/xbox-insiders-july-2026-console-features/)
- Profile/gamerpic, clock and notification placement in the top bar: **not described** by sources (gap).

### Recent tiles ("Jump back in")
- Row of recently used games and apps sits low on the screen. The large tiles are recent items on this device. [Xbox Wire 2025-07](https://news.xbox.com/en-us/2025/07/21/now-available-for-xbox-insiders-your-cross-device-play-history-and-easier-access-to-your-cloud-playable-console-games-on-pc/), console image: [Roaming MRU console](https://xboxwire.thesourcemediaassets.com/sites/2/2025/07/Roaming-MRU-Console-131f550ac5b88e38137f.png)
- A **Play History** tile at the end of the row covers games played on any Xbox device. [same](https://news.xbox.com/en-us/2025/07/21/now-available-for-xbox-insiders-your-cross-device-play-history-and-easier-access-to-your-cloud-playable-console-games-on-pc/). It can be hidden via Settings [(secondary)](https://mp1st.com/news/xbox-system-update-october-22-new-quality-of-life-features).
- Customisation at **Settings > General > Personalization > Games & apps** or **My games & apps > Manage > Games & apps**:
  - hide system apps
  - pin up to **3** items (Menu button on a tile → Pin/Unpin); pinned items stay at the front
  - reduce the visible tile count
  - Source: [May 2025](https://news.xbox.com/en-us/2025/05/16/xbox-insiders-get-new-ways-to-personalize-home/), [June 2025](https://news.xbox.com/en-us/2025/06/23/xbox-june-update-copilot-for-gaming-aggregated-gaming-library/). A settings screenshot's alt text also mentions a **tile size** option: [Settings image](https://xboxwire.thesourcemediaassets.com/sites/2/2025/05/Customize-Your-Home-Settings-2d606ec3975e4cd96a05.jpeg), [Home image](https://xboxwire.thesourcemediaassets.com/sites/2/2025/05/Customize-Your-Home-87c9aab695674979a2f1.jpeg), [MRU image](https://xboxwire.thesourcemediaassets.com/sites/2/2025/06/MRU_Home-ad76ab7481fab4b93e0b-1900x1080.jpg)
  - Since 2026-07, these live in a separate **Home** personalisation section. [Xbox Wire 2026-06-10](https://news.xbox.com/en-us/2026/06/10/xbox-insiders-july-2026-console-features-2/)
- Insider (Aug 2026): any item, including the Play History tile, can be removed from the list via the Menu button. [Xbox Wire](https://news.xbox.com/en-us/2026/08/19/xbox-insiders-october-2026-console-features-2/)
- The **My games & apps** tile shows a badge when items are new or need attention. [Xbox Wire 2023-05](https://news.xbox.com/en-us/2023/05/01/xbox-insiders-your-feedback-shapes-the-new-home-experience/)

### Pins and groups
- Rows below the recent list can include:
  - pinned favourites
  - curated groups
  - system groups (e.g. **Quick Resume**)
  - Friends & community updates
  - Watch & Listen (spotlight plus entertainment apps)
  - curated/personalised game lists (Game Pass, Store)
- Source: [Xbox Wire 2023-07](https://news.xbox.com/en-us/2023/07/26/welcome-to-your-new-xbox-home/). Images: [My favorites](https://news.xbox.com/en-us/wp-content/uploads/sites/2/2023/07/02_My-favorites-f9e0eacb5576c0f13294.jpg), [Friends & community](https://news.xbox.com/en-us/wp-content/uploads/sites/2/2023/07/03_Friends-Community-Updates-5edf483b7a31fb312e55.jpg), [Watch & Listen spotlight](https://xboxwire.thesourcemediaassets.com/sites/2/2023/07/04_Watch-Listen-spotlight-43c2bd493f78984b2c18.jpg), [Entertainment apps](https://xboxwire.thesourcemediaassets.com/sites/2/2023/07/05_Entertainment-apps-468b77a3467062a2ee7f.jpg).
- **Up to 10 groups** on Home (was 2). They are reordered the same way as games on Home and pinned from My games & apps, which now shows clearer "already pinned" indicators. [April 2026](https://news.xbox.com/en-us/2026/04/30/april-xbox-update-2026/), image: [More groups on Home](https://xboxwire.thesourcemediaassets.com/sites/2/2026/04/More-groups-on-Home-e4ec65657104850977c1.jpg)
- Quick Resume group tiles have **More options → Manage Quick Resume** (per-game disable). [April 2026](https://news.xbox.com/en-us/2026/04/30/april-xbox-update-2026/), image: [Quick Resume settings](https://xboxwire.thesourcemediaassets.com/sites/2/2026/04/Quick-Resume-Settings-e041c79397655928430e.jpg)

## Guide (Overlay opened by the Guide button)

- Opens as a flyout/overlay with **horizontal tabs across the top**, reorderable via **Profile & system > Customize guide tabs**. Tabs can be restored to default. [Xbox Wire 2020-04](https://news.xbox.com/en-us/2020/04/06/xbox-insiders-new-features-april-update/), [2020-08](https://news.xbox.com/en-us/2020/08/25/xbox-one-and-xbox-series-x-new-digital-experience/), [support.xbox.com (JS-only, seen via snippet)](https://support.xbox.com/en-US/help/hardware-network/getting-started-set-up/customize-guide-tabs)
- **Tabs (secondary, via the support-page snippet):**
  - Guide (home/landing)
  - People
  - Parties & chats
  - Game activity
  - Capture & share
  - Profile & system (shown as the gamerpic, on the left; holds Settings, audio, power, sign-out)
- **Landing tab:** recent items, quick links to Home (Dashboard), My games & apps and the Store. A **utilities row at the bottom** holds notifications, search, audio settings and similar. Notifications are one unified inbox (alerts, invites, messages). [Xbox Wire 2020-08](https://news.xbox.com/en-us/2020/08/25/xbox-one-and-xbox-series-x-new-digital-experience/). Images (2020, may be dated): [Guide](https://news.xbox.com/en-us/wp-content/uploads/sites/2/2020/08/Xbox-Series-X-Guide.jpg?resize=940,528), [Notifications](https://news.xbox.com/en-us/wp-content/uploads/sites/2/2020/08/Xbox-Series-X-Notifications.jpg?resize=825,528), [Volume slider](https://news.xbox.com/en-us/wp-content/uploads/sites/2/2020/08/Xbox-Series-X-Volume-Slider.jpg?resize=940,528), [new-user GIF](https://news.xbox.com/en-us/wp-content/uploads/sites/2/2020/08/Console_Guide_NewUser_EmptyState_ThreeScreens.gif?resize=940,528)
- **2026 additions:**
  - A **custom accent colour** shows as "a subtle splash" in the Guide. Turn it off at **Settings > General > Personalization > Customize the guide**.
  - Profile view in the Guide shows the **5 most recent badges** and tiered Gamerscore badges.
  - Sources: [April 2026](https://news.xbox.com/en-us/2026/04/30/april-xbox-update-2026/), [March Insider](https://news.xbox.com/en-us/2026/03/18/xbox-insiders-april-2026-console-features/), [May 13 Insider](https://news.xbox.com/en-us/2026/05/13/xbox-insiders-june-2026-console-features/)
- No 2024–2026 source describes a structural Guide redesign. The 2020 structure plus accent and badges is the best public picture. Its current pixel layout is a gap.

## My games & apps

- Reached from Home's top bar (Library) or the Guide.
- Sections:
  - Games
  - Apps
  - Full library (segmented section)
  - **Manage** (quick access to options formerly buried in Settings)
  - Groups
  - Sources: [Windows Central (secondary, undated)](https://windowscentral.com/microsoft-tinkering-games-apps-section-xbox), [June 2025](https://news.xbox.com/en-us/2025/06/23/xbox-june-update-copilot-for-gaming-aggregated-gaming-library/)
- **Play History tab** (April 2026): horizontal layout grouped by recency. [Xbox Wire](https://news.xbox.com/en-us/2026/04/30/april-xbox-update-2026/), image: [PlayHistory](https://xboxwire.thesourcemediaassets.com/sites/2/2026/04/PlayHistory-ab92655a1d19acab6888.jpg)
- 2026:
  - filters for owned, shared, and installed-but-no-access games [May 13](https://news.xbox.com/en-us/2026/05/13/xbox-insiders-june-2026-console-features/)
  - optional **poster-style (portrait) artwork** and selectable status icons on tiles [June 10](https://news.xbox.com/en-us/2026/06/10/xbox-insiders-july-2026-console-features-2/), image: [Library tiles](https://xboxwire.thesourcemediaassets.com/sites/2/2026/06/2.-Library-Tiles-Updates-643b19811396e3270b30-1900x1080.png)
  - local text search alongside filters and sort (Insider, Sep 2026) [Sep 9](https://news.xbox.com/en-us/2026/09/09/xbox-insiders-october-2026-console-features-3/), image: [Local text filter](https://xboxwire.thesourcemediaassets.com/sites/2/2026/09/2.-Local-text-filter-de857c8012a1ec2306e7-1024x576.png)

## Game hub / Manage game

- **Game hubs** (June 2025) open when a game is selected from the recent list, groups or installed games.
  - Content: Play button, player stats, achievements, friends playing, recent captures, add-ons, events.
  - "The default focus in game hubs is always on the Play button". Double-tapping A on a tile launches directly.
  - Toggle where hubs appear at Settings > General > Personalization > Games & apps.
  - Source: [Xbox Wire](https://news.xbox.com/en-us/2025/06/23/xbox-june-update-copilot-for-gaming-aggregated-gaming-library/), image: [Metaphor game hub](https://xboxwire.thesourcemediaassets.com/sites/2/2025/06/Metaphor_ReFantazio_Xbox-Wire-060225-a1886e6c04b1cd9a61f6-1900x1080.jpg)
- Hubs and the game card also cover Xbox 360 titles. The game card offers wishlist and cloud-play-while-updating. [June 24 2026](https://news.xbox.com/en-us/2026/06/24/xbox-insiders-august-2026-console-features/)
- **Manage game and add-ons** (reached via More options/Menu on a tile) includes:
  - Quick Resume settings
  - Saved data > Restore saved data (cloud save versions)
  - Sources: [April 2026](https://news.xbox.com/en-us/2026/04/30/april-xbox-update-2026/), [Aug 5 2026](https://news.xbox.com/en-us/2026/08/05/xbox-insiders-october-2026-console-features/), image: [Cloud saves](https://xboxwire.thesourcemediaassets.com/sites/2/2026/08/3.-Cloud-saves-f6729d28b24fe4d92dcf-1024x576.png)

## Settings

- Reached from Home's top bar or Guide > Profile & system. Layout is a left category list with a right pane (secondary/unverified for 2026).
- Paths confirmed by Xbox Wire:
  - General > Personalization > Games & apps
  - General > Personalization > Customize the guide
  - General > Volume & audio output > Additional options
  - General > TV & display options
  - System > Storage > Manage local game saves
  - System > Startup
  - Cloud gaming > Streaming preferences
  - Sources: [June 2025](https://news.xbox.com/en-us/2025/06/23/xbox-june-update-copilot-for-gaming-aggregated-gaming-library/), [April 2026](https://news.xbox.com/en-us/2026/04/30/april-xbox-update-2026/), [Aug 19](https://news.xbox.com/en-us/2026/08/19/xbox-insiders-october-2026-console-features-2/), [Sep 9](https://news.xbox.com/en-us/2026/09/09/xbox-insiders-october-2026-console-features-3/)
- Personalisation is split into **Home** and **My games & apps** sections (July 2026), with shortcuts into it "throughout menus". [June 10](https://news.xbox.com/en-us/2026/06/10/xbox-insiders-july-2026-console-features-2/)
- Accessibility includes Narrator, Magnifier, Captioning, High contrast, Night mode and colour filters. [xbox.com accessibility](https://www.xbox.com/en-ZA/community/for-everyone/accessibility)
- Exact top-level category list and order for 2026: **not confirmed** (gap).

## Search

- Global Search is one of the top-bar entries on Home and also sits in the Guide utilities row. [2023-05](https://news.xbox.com/en-us/2023/05/01/xbox-insiders-your-feedback-shapes-the-new-home-experience/), [2020-08](https://news.xbox.com/en-us/2020/08/25/xbox-one-and-xbox-series-x-new-digital-experience/)
- Local in-list search (My games & apps, achievements) is in Insider testing, Sep 2026. [Sep 9](https://news.xbox.com/en-us/2026/09/09/xbox-insiders-october-2026-console-features-3/)
- The Search page layout (keyboard, suggestions, result categories) is **not described** publicly (gap).

## Visual language

### Focus / selection
- The 2020 refresh changed "tile shape, fonts, and focus indicator across the experience". [Xbox Wire 2020-08](https://news.xbox.com/en-us/2020/08/25/xbox-one-and-xbox-series-x-new-digital-experience/), plus the [visual refresh style guide image](https://news.xbox.com/en-us/wp-content/uploads/sites/2/2020/08/Xbox-Visual-Refesh-Style-Guide.jpg?resize=940,528), which is the best first-party visual reference for tile shape and focus.
- No public source describes the focus ring's colour, thickness or corner radius, or any scale/lift on focus. All are **gaps**.

### Motion
- Known behaviours:
  - background crossfades to the focused game's art (2023)
  - the UI fades out in immersive mode (2026)
  - activity-feed video clips auto-play (2020)
  - the new boot animation shows the green glass logo (2026), and the startup animation is selectable from Xbox One-era to current (Insider)
  - Sources: [2023](https://news.xbox.com/en-us/2023/07/26/welcome-to-your-new-xbox-home/), [2026-08-19](https://news.xbox.com/en-us/2026/08/19/xbox-insiders-october-2026-console-features-2/), [2026-05-13](https://news.xbox.com/en-us/2026/05/13/xbox-insiders-june-2026-console-features/), [2026-09-09](https://news.xbox.com/en-us/2026/09/09/xbox-insiders-october-2026-console-features-3/), [startup image](https://xboxwire.thesourcemediaassets.com/sites/2/2026/09/1.-Startup-animation-865665f17a0c65c4ac0a-1024x576.png)
- No durations or easing are published (gap).

### Typography
- The 2020 refresh changed fonts but did not name them. [Xbox Wire 2020-08](https://news.xbox.com/en-us/2020/08/25/xbox-one-and-xbox-series-x-new-digital-experience/)
- Secondary sources say the dashboard uses **Segoe (Segoe UI family)**, which Microsoft has not confirmed. [madegooddesigns (secondary)](https://madegooddesigns.com/?p=7135). Segoe UI is available on Windows, so it is a practical choice for Playnite either way.
- Sizes and weights per surface: gap.

### Colour and accent
- The user picks an accent colour from system colours or, since April 2026, a **custom hue/shade**. The custom picker has readability "guardrails" and keeps the last custom colour. The accent appears "in subtle accents across the console" and in the Guide. [April 2026](https://news.xbox.com/en-us/2026/04/30/april-xbox-update-2026/), image: [Custom user colour](https://xboxwire.thesourcemediaassets.com/sites/2/2026/04/Custom-User-Color-1a96376daf06d699ca2c.jpg)
- Hex entry and "match my gamerpic", with a preview, were announced as coming soon. [May 29 2026](https://news.xbox.com/en-us/2026/05/29/xbox-insiders-july-2026-console-features/)
- System theme is Dark, Light or Scheduled. [Boss Rush (secondary, 2021)](https://bossrush.net/2021/08/01/change-the-background-and-theme-on-your-xbox-one-and-x-s/)
- Brand green: Xbox web properties use `#107C10` (theme-color meta on support.xbox.com). The 2026 rebrand returns to a glossy green glass orb logo. [VGC (secondary)](https://www.videogameschronicle.com/news/asha-sharmas-xbox-overhaul-continues-as-revamped-series-xs-bootup-revealed/)
- Gaps: the system colour swatch list and values, where exactly the accent is applied (focus? buttons? progress?), and the dark/light surface and text colours.

### UI sounds
- Navigation sounds (described as "page whooshes, focus blips, keyboard noises") and notification sounds can be muted at Settings > General > Volume & audio output > Additional options, or Settings > Accessibility > Audio. [TechRadar (secondary)](https://www.techradar.com/news/you-can-make-the-xbox-series-x-a-lot-less-noisy-now), [PureXbox (secondary)](https://www.purexbox.com/news/2022/04/new-xbox-feature-allows-you-to-mute-system-sound-effects)
- New boot sound (May 2026). Sound on/off is chosen alongside the startup animation. [Xbox Wire](https://news.xbox.com/en-us/2026/05/13/xbox-insiders-june-2026-console-features/), [Asha Sharma post](https://twitter.com/asha_shar/status/2052046475472085437), [original Series boot video](https://www.youtube.com/watch?v=oFcDfznnvSM)
- No public catalogue of the navigation sound set exists (gap).

## Video references

- [Major Nelson 2020 UX walkthrough](https://youtu.be/k8O5rg9AwnU) (2020, pre-2023-Home; good for Guide motion).
- [Original Series X|S boot](https://www.youtube.com/watch?v=oFcDfznnvSM).
- I found no first-party 2025–2026 video walkthrough of the current Home or Guide. Fresh video is the main capture need.

## Gaps that need a real-console capture (feeds #7)

Capture on a retail Series X|S on the current public OS, at 1080p and 4K, in dark theme with a static background and default accent. Then repeat the key shots with a custom accent and in light theme.

1. **Home at rest:** a full-frame screenshot showing top-bar contents and order (gamerpic, clock, notifications, What's New, the service indicator if it can be triggered), and recent-row tile sizes, spacing, and safe-area margins.
2. **Home focus states:** a focused vs unfocused recent tile (focus ring colour, thickness, radius, scale/lift, shadow, title label), a focused top-bar item, a focused group tile, and the Play History tile.
3. **Home scroll:** video of scrolling from the recent row down through groups, showing how the background dims or blurs and whether the top bar collapses.
4. **Responsive game art:** video of moving across recent tiles with the background following focus (crossfade duration, easing).
5. **Pinned tile marker** and the per-tile Menu (context menu) appearance.
6. **Guide:** video of opening and closing via the Guide button (slide direction, duration, backdrop dim/blur), plus a screenshot of each tab: current tab names and order, landing-page layout, utilities row, accent splash, profile badges.
7. **My games & apps:** each section (Games, Apps, Play History, Full library, Manage, Groups), the left nav focus state, landscape vs poster art, status icons, and the filter/sort UI.
8. **Game hub:** full layout with Play focused by default; the Manage game and add-ons page.
9. **Settings:** top-level category list and order, a sub-page, toggle/checkbox/dropdown controls and their focus states, and the Personalization > Home / My games & apps sections.
10. **Search:** empty state, on-screen keyboard, and results layout.
11. **Accent and colour:** the system colour swatch grid, the custom hue/shade picker, and sampled hex values for background panels, text (primary/secondary) and accent usage in dark and light themes.
12. **Typography:** high-res crops of headers, tile labels, body and button-hint text to confirm the typeface and measure sizes and weights.
13. **Motion timings:** page transitions (Home → My games & apps, Home → Settings), focus-move animation, and the immersive-mode fade.
14. **UI sounds:** audio recordings of focus move, select, back, Guide open, Guide close, notification pop and error/boundary, ideally isolated with game audio off.
15. **Notification toast:** position, size, animation and sound.
16. **Boot and startup:** the new boot animation and sound (reference only; it is outside the theme's scope unless Playnite startup is themed).
