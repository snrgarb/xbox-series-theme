using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// Search: games, never hidden ones, and Apps found by name as the user types on the console keyboard. With nothing
    /// typed it shows the games most recently added; typing shows a Games row and an Apps row, each only with something
    /// found. It takes the place of My games &amp; apps and Settings, as they take each other's.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>How many games the recently added row holds.</summary>
        private const int RecentlyAddedCount = 12;

        /// <summary>How names are matched: whatever the case, and accents ignored, so "pokemon" finds "Pokémon".</summary>
        private const CompareOptions SearchMatch = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

        public SearchState Search { get; } = new SearchState();

        /// <summary>Opens Search from Home's top bar, empty, the keyboard up; B goes back to the top bar.</summary>
        public void OpenSearch() => ShowSearch(SearchOrigin.TopBar);

        /// <summary>
        /// Puts Search up empty, every time it opens: nothing typed, Shift on, the letters showing, the keyboard up. It
        /// takes the place of My games &amp; apps, which gives the user's own filter and sort back, and of Settings.
        /// </summary>
        private void ShowSearch(SearchOrigin from)
        {
            CloseMyGames();
            CloseSettings();
            Search.Keyboard.Reset();
            Search.OpenedFrom = from;
            Search.IsKeyboardShown = true;
            Search.IsShown = true;
            RefreshSearch();
        }

        /// <summary>A on the page's search box: the keyboard comes up again, the text as it was.</summary>
        public void ShowSearchKeyboard()
        {
            if (SearchHasThePad)
            {
                Search.IsKeyboardShown = true;
            }
        }

        /// <summary>
        /// A on a key of the console keyboard: a character goes in at the cursor, Shift going off after it; Enter puts
        /// the keyboard away, focus going to the results. The rows follow the text at once.
        /// </summary>
        public void PressKey(KeyboardKey key)
        {
            var keyboard = Search.Keyboard;
            if (!SearchHasThePad || !Search.IsKeyboardShown || !keyboard.Keys.Contains(key))
            {
                return;
            }

            switch (key.Kind)
            {
                case KeyboardKeyKind.Character:
                    keyboard.Type(key);
                    break;
                case KeyboardKeyKind.Space:
                    keyboard.Insert(" ");
                    break;
                case KeyboardKeyKind.Shift:
                    keyboard.SetShifted(!keyboard.IsShifted);
                    break;
                case KeyboardKeyKind.Symbols:
                    keyboard.ToggleSymbols();
                    break;
                case KeyboardKeyKind.Backspace:
                    keyboard.Backspace();
                    break;
                case KeyboardKeyKind.Left:
                    keyboard.MoveCursor(-1);
                    break;
                case KeyboardKeyKind.Right:
                    keyboard.MoveCursor(1);
                    break;
                case KeyboardKeyKind.Enter:
                    Search.IsKeyboardShown = false;
                    break;
            }

            RefreshSearch();
        }

        /// <summary>
        /// A pad shortcut with the keyboard up: X backspace, Y space, LB and RB the cursor left and right, the left stick's
        /// click Shift, and Start submits. Returns whether the button was the keyboard's, so the view marks it handled.
        /// </summary>
        public bool PressKeyboardShortcut(ControllerButton button)
        {
            if (!SearchHasThePad || !Search.IsKeyboardShown)
            {
                return false;
            }

            var kind = ShortcutKey(button);
            if (kind == null)
            {
                return false;
            }

            PressKey(Search.Keyboard.Keys.First(k => k.Kind == kind));
            return true;
        }

        /// <summary>The key a pad button stands for on the keyboard; null for a button that isn't the keyboard's.</summary>
        private static KeyboardKeyKind? ShortcutKey(ControllerButton button)
        {
            switch (button)
            {
                case ControllerButton.X: return KeyboardKeyKind.Backspace;
                case ControllerButton.Y: return KeyboardKeyKind.Space;
                case ControllerButton.LeftShoulder: return KeyboardKeyKind.Left;
                case ControllerButton.RightShoulder: return KeyboardKeyKind.Right;
                case ControllerButton.LeftStick: return KeyboardKeyKind.Shift;
                case ControllerButton.Start: return KeyboardKeyKind.Enter;
                default: return null;
            }
        }

        /// <summary>
        /// Text typed on a physical keyboard: in at the cursor as typed, whatever the console keyboard's Shift says, which
        /// goes off as after any character; with the console keyboard up or away.
        /// </summary>
        public void TypeSearchText(string text)
        {
            // Backspace and Enter, say, reach a view as text too; only what prints goes in.
            var printable = new string((text ?? string.Empty).Where(c => !char.IsControl(c)).ToArray());
            if (SearchHasThePad && printable.Length > 0)
            {
                Search.Keyboard.Insert(printable);
                Search.Keyboard.SetShifted(false);
                RefreshSearch();
            }
        }

        /// <summary>
        /// A on a game the page found, or a recently added one: its Game hub, at once, as there is no double-tap to play
        /// here. The hub is for the main list's selected game, so the main list takes every game first, which the user's
        /// own filter, kept to give back as the page goes, could leave it out of. The page stays behind the hub, which B
        /// comes back to.
        /// </summary>
        public void ChooseSearchGame(Guid gameId)
        {
            if (SearchHasThePad && Search.RecentlyAdded.Concat(Search.Games).Any(t => t.GameId == gameId))
            {
                ShowInMainList(GameListFilter.LastPlayed);
                playnite.OpenGameHub(gameId);
            }
        }

        /// <summary>
        /// A on an App the page found: starts it with its arguments, as My games &amp; apps' Apps does, whatever game is
        /// running, without the Launch screen.
        /// </summary>
        public void StartSearchApp(Guid appId)
        {
            var app = SearchHasThePad ? Search.Apps.FirstOrDefault(a => a.Id == appId) : null;
            if (app != null)
            {
                playnite.StartApp(app);
            }
        }

        /// <summary>
        /// B on the page: puts the keyboard away first, focus going to the results, then goes back to where the page was
        /// opened from (<see cref="SearchState.OpenedFrom"/>): Home, focus on the top bar's Search from there. A menu,
        /// prompt or the Guide still showing over the page answers the B instead.
        /// </summary>
        public void LeaveSearch()
        {
            if (!SearchHasThePad)
            {
                return;
            }

            if (Search.IsKeyboardShown)
            {
                Search.IsKeyboardShown = false;
            }
            else
            {
                CloseSearch();
            }
        }

        /// <summary>
        /// The page's rows for the text as it is now, and the library as it is now: with nothing typed, the games most
        /// recently added; else the games and Apps found. A row is only replaced when what it shows changed, since a new
        /// row rebuilds every tile and reads every cover again.
        /// </summary>
        private void RefreshSearch()
        {
            if (!Search.IsShown)
            {
                return;
            }

            var text = Search.Keyboard.Text.Trim();
            Search.SetHasText(text.Length > 0);
            IReadOnlyList<HomeTile> recentlyAdded = text.Length > 0
                ? new HomeTile[0]
                : library.Games
                    .Where(g => !g.Hidden)
                    .OrderBy(g => g.Added == null)
                    .ThenByDescending(g => g.Added)
                    .Take(RecentlyAddedCount)
                    .Select(TileOf)
                    .ToList();
            IReadOnlyList<HomeTile> games = text.Length == 0
                ? new HomeTile[0]
                : Found(library.Games.Where(g => !g.Hidden), g => g.Name, g => g.SortingName, text).Select(TileOf).ToList();
            IReadOnlyList<App> apps = text.Length == 0 || !MyGames.HasApps
                ? new App[0]
                : Found(MyGames.Apps, a => a.Name, a => a.Name, text).ToList();

            if (!SameTiles(recentlyAdded, Search.RecentlyAdded))
            {
                Search.RecentlyAdded = recentlyAdded;
            }

            if (!SameTiles(games, Search.Games))
            {
                Search.Games = games;
            }

            if (apps.Count != Search.Apps.Count || apps.Zip(Search.Apps, App.Same).Contains(false))
            {
                Search.Apps = apps;
            }
        }

        /// <summary>
        /// What of <paramref name="items"/> has <paramref name="text"/> in its name: first those whose name starts with
        /// it, then those with a word starting with it, then the rest, each in order of <paramref name="sortingName"/>.
        /// </summary>
        private static IEnumerable<T> Found<T>(IEnumerable<T> items, Func<T, string> name, Func<T, string> sortingName, string text)
        {
            var compare = CultureInfo.CurrentCulture.CompareInfo;
            return items
                .Select(item => new { Item = item, Rank = Rank(compare, name(item) ?? string.Empty, text) })
                .Where(found => found.Rank >= 0)
                .OrderBy(found => found.Rank)
                .ThenBy(found => sortingName(found.Item), StringComparer.CurrentCultureIgnoreCase)
                .Select(found => found.Item);
        }

        /// <summary>0 for a name starting with the text, 1 for a word in it starting with it, 2 for the text elsewhere in it, -1 for neither.</summary>
        private static int Rank(CompareInfo compare, string name, string text)
        {
            if (compare.IsPrefix(name, text, SearchMatch))
            {
                return 0;
            }

            if (compare.IndexOf(name, text, SearchMatch) < 0)
            {
                return -1;
            }

            for (var i = 1; i < name.Length; i++)
            {
                if (!char.IsLetterOrDigit(name[i - 1]) && char.IsLetterOrDigit(name[i]) && compare.IsPrefix(name.Substring(i), text, SearchMatch))
                {
                    return 1;
                }
            }

            return 2;
        }

        /// <summary>Whether two rows of tiles would show the same: the same games, as each reads now.</summary>
        private static bool SameTiles(IReadOnlyList<HomeTile> a, IReadOnlyList<HomeTile> b) =>
            a.Count == b.Count
            && a.Zip(b, (x, y) => x.GameId == y.GameId && x.Title == y.Title && x.CoverImage == y.CoverImage
                && x.Source == y.Source && x.IsRunning == y.IsRunning && x.Status == y.Status).All(same => same);

        /// <summary>
        /// Whether the page is up with nothing over it. Playnite's own buttons reach the page before the pad's reach the
        /// core, so a menu or prompt still showing, or the Guide, answers them instead.
        /// </summary>
        private bool SearchHasThePad => Search.IsShown && ActiveDialog == null && !Guide.IsOverlayShown;

        /// <summary>Takes the page away, giving back the user's own filter and sort if a game it opened took the main list over.</summary>
        private void CloseSearch()
        {
            if (Search.IsShown)
            {
                Search.IsShown = false;
                GiveBackMainList();
                playnite.ReleaseUserFilter();
            }
        }
    }
}
