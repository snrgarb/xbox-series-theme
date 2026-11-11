# Community UX feedback, November 2026: what holds up

UX suggestions posted in a Discord thread on 2026-11-11, checked against the code on `main` (ce59583) and on the
win11-e2e VM. The screenshots are in [`ux-feedback-2026-11/`](ux-feedback-2026-11/): `feedback-*` are the
commenter's annotated screenshots of the Dashboard, `console-*` their console references, and the rest come from the
throwaway e2e payload `e2e/payloads/ux-audit` on this branch. That payload seeds three games whose covers have a red
border and labelled edges: a 600x900 portrait cover, a 920x430 Steam header and a 512x512 square.

**Gist:** cover art is cropped on every surface whenever its shape differs from the tile's, and on Home's Recent row
and in the Game hub even a standard 2:3 cover is cropped. The Groups screen keeps the top bar and never shows a third
row. Text is set smaller than the console's 26 px minimum. The clock is in Segoe UI. The Game hub's header is split
over two lines. The profile picture claim is already met. The Store/Game Pass slots and the news tiles are ideas
rather than gaps, and need a decision before they become tickets.

## Findings

| # | Claim | Verdict | Evidence |
|---|---|---|---|
| 1 | Clock should be Bahnschrift SemiBold | **Holds.** Today it is Segoe UI Regular 22, and the theme-only clock is Segoe UI Semibold 22 | `StatusArea.xaml:107`, `Main.xaml:614`; `home-1.jpg`; console in `console-home.jpg` |
| 2 | Text on tiles and in the hub should be Segoe UI Regular 24–26 | **Holds in substance.** The guideline is real: [Xbox Accessibility Guideline 101](https://learn.microsoft.com/xbox/accessibility/xbox-accessibility-guidelines/101#implementation-guidelines) sets console text at ≥ 26 px at 1080p. The Dashboard uses 18 for the focused tile's title, 19–22 in the hub and Segoe UI Semibold 22 on the hub's buttons | `HomeTiles.xaml:80`, `GameDetails.xaml:39-58,104-117` |
| 3 | Group headers: "Bahnschrift 36 Semilight, but actually 32 Regular" | **The reading of ours is wrong:** it is Bahnschrift SemiBold 24. The 36 SemiLight target comes from the commenter's own guideline source, which isn't public | `GroupsControl.xaml:57-58`; `groups-1.jpg` |
| 4 | Vertical cards are cropped | **Holds, and goes wider than reported.** Every tile is `UniformToFill`. On the Recent row (square) a 2:3 cover loses its top and bottom, and a Steam header shows about a third of its width. In Groups (194x296) a 2:3 cover fits, but a square loses its sides and a header shows only its left edge. In the hub (375x500) a header shows only its left edge, and a 2:3 cover loses its top and bottom. My games & apps and Search are square tiles too | `home-1.jpg`, `groups-1.jpg`, `groups-3.jpg`, `hub-header.jpg`, `feedback-groups.jpg`, `feedback-hub.jpg` |
| 5 | Focusing the wide tiles should bring the Groups into view (console: they peek in under the wide row) | **Holds.** Home is two screens since 7a9d444, and Groups never peek in. That was on purpose, and the console does it the commenter's way | `home-wide.jpg` vs `console-home-scrolled.jpg`; `HomeRows.cs`, `GroupsControl.xaml.cs:205-224` |
| 6 | Top bar should hide while browsing Groups, with 2.5 rows visible | **Holds.** The top bar stays, two Groups show and the third is faded out | `groups-1.jpg`, `groups-3.jpg`; `Main.xaml:300-312` |
| 7 | Hub: the metadata line should sit under the title, beside features and the rating | **Holds.** Title and metadata share a line, and the features and rating are on a second line | `GameDetails.xaml:547-713`; `feedback-hub.jpg` |
| 8 | Hub: buttons in Bahnschrift Bold 24 | **Holds as a gap:** they are Segoe UI Semibold 22. The console's hub buttons are set in its header face | `GameDetails.xaml:104-117` |
| 9 | Hub: blurred game art inside the panel, the previous screen behind it | **Holds as a difference.** Ours is the game's art full screen, with a near-opaque `#E61F1F1F` band over it | `GameDetails.xaml:372-456`; `hub-header.jpg` |
| 10 | Hub: Achievements and download queue buttons, as on the console | **Holds as a gap.** The action row is Play/Install, Manage and the page toggle. Achievements page is #34 | `GameDetails.xaml:490-544` |
| 11 | Profile name and picture could come from the Windows user | **Already done.** The first Profile takes the Windows user's name and the account picture (`Image192`). The VM's account has no picture, so it shows the generic one, as the commenter's does | `WindowsUserAccount.cs:93-117`; `home-1.jpg` |
| 12 | Battery useful for handheld users; notifications in place of the mic | **Holds for the PC's battery.** With the extension only the pad's battery shows, and the PC's (`PowerStatus`) shows only with the theme alone. Notifications already has its own button, and there is no mic icon outside a Discord call | `GamingInputController.cs:83-129`, `Main.xaml:584-616` |
| 13 | Play history tile could give way to another recent game | **Holds as an option.** The console lets the tile be hidden (`xbox-dashboard-reference.md`, Recent tiles). Ours is always there, after 7 games | `ExtensionCore.cs:21`, `RecentRowControl.xaml:54-57` |
| 14 | Discord wide tile duplicates the top bar; wide tiles could be Steam News | **An idea, not a gap.** It needs a decision on network use and content | `PromoRow.xaml` |
| 15 | Store / Game Pass gaps in the top bar: custom links | **An idea, not a gap.** The Bundle has no store of its own | `Main.xaml:501-562` |

Not validated: the commenter's console reference for the Game hub, which was sent as a duplicate of their hub
screenshot.

## Also seen

- After Start-up, Home's focus is on the profile picture, not the Recent row's first game (`home-1.jpg`). Not raised
  in the thread.
- The hint bar covers the bottom-right Group tile (`groups-3.jpg`), as the console's does in `console-home-scrolled.jpg`.
