using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK;
using Playnite.SDK.Events;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Theme settings' Personalisation page: its boxes, and the page each opens in their place (Boot video, Profiles, a
    /// profile, its Picture, Who's playing today?). Every box shows its name
    /// alone; what it's set to shows on the page it opens, but for Background delay, a checkbox A turns over in place. The pad moves between a page's boxes; A opens or
    /// sets, and B steps back a page at a time, focus going back to the box that opened it, then from Personalisation's
    /// boxes to Theme settings' own. Every choice goes straight to the core, which saves it; the pane holds which page
    /// shows and nothing else.
    /// </summary>
    public partial class PersonalisationPane : UserControl
    {
        /// <summary>Each generation's friendly name, in the Boot video page's order.</summary>
        internal static readonly IReadOnlyList<KeyValuePair<BootVideo, string>> BootVideoNames = new[]
        {
            new KeyValuePair<BootVideo, string>(BootVideo.Off, "Off"),
            new KeyValuePair<BootVideo, string>(BootVideo.Xbox, "Xbox (2001)"),
            new KeyValuePair<BootVideo, string>(BootVideo.Xbox360, "Xbox 360 (2005)"),
            new KeyValuePair<BootVideo, string>(BootVideo.Xbox360Slim, "Xbox 360 Slim (2010)"),
            new KeyValuePair<BootVideo, string>(BootVideo.Xbox360SpecialEdition, "Xbox 360 Special Edition (2012)"),
            new KeyValuePair<BootVideo, string>(BootVideo.XboxOne, "Xbox One (2013)"),
            new KeyValuePair<BootVideo, string>(BootVideo.XboxOneX, "Xbox One X (2017)"),
            new KeyValuePair<BootVideo, string>(BootVideo.XboxSeriesS, "Xbox Series S (2020)"),
            new KeyValuePair<BootVideo, string>(BootVideo.XboxSeriesX, "Xbox Series X (2020)"),
        };

        private readonly Stack<Opened> opened = new Stack<Opened>();
        private ExtensionCore core;
        private Page page = Page.Personalisation;

        /// <summary>The profile the Profile and Picture pages are of, by Id: the core replaces a profile it changes.</summary>
        private string profileId;

        /// <summary>
        /// The profiles there were as Add profile opened text entry, by Id, until it goes: the one not among them is the
        /// one added. Null with no add pending.
        /// </summary>
        private HashSet<string> profilesBeforeAdd;

        public PersonalisationPane()
        {
            InitializeComponent();
            Loaded += (s, e) => { if (core != null) { Follow(true); } };
            Unloaded += (s, e) => { if (core != null) { Follow(false); } };
        }

        /// <summary>B, or Left from the first column, on Personalisation's boxes: back to Theme settings' boxes.</summary>
        public event EventHandler BackToThemeSettings;

        /// <summary>The page showing changed, and with it <see cref="Title"/>.</summary>
        public event EventHandler TitleChanged;

        private enum Page
        {
            Personalisation,
            BootVideo,
            WhosPlaying,
            Profiles,
            Profile,
            Pictures,
        }

        /// <summary>The header over the page showing: the category's, the page's, or the profile's gamertag.</summary>
        public string Title
        {
            get
            {
                switch (page)
                {
                    case Page.BootVideo: return "Boot video";
                    case Page.WhosPlaying: return "Who's playing today?";
                    case Page.Profiles: return "Profiles";
                    case Page.Profile: return Profile?.Gamertag ?? "";
                    case Page.Pictures: return "Picture";
                    default: return "Personalisation";
                }
            }
        }

        private StartUpSettingsState State => core.Settings.StartUp;

        /// <summary>Personalisation's own boxes as the pad moves between them: a row each, Background delay alone on the last.</summary>
        private Button[][] Rows => new[]
        {
            new[] { BootVideoBox, ProfilesBox },
            new[] { AskBox },
            new[] { BackgroundDelayBox },
        };

        private Profile Profile => State.Profiles.FirstOrDefault(p => p.Id == profileId);

        internal void Attach(ExtensionCore extensionCore)
        {
            core = extensionCore;
            DataContext = State;
            if (IsLoaded)
            {
                Follow(true);
            }
        }

        private void Follow(bool on)
        {
            State.PropertyChanged -= OnStateChanged;
            core.TextEntry.PropertyChanged -= OnTextEntryChanged;
            if (on)
            {
                State.PropertyChanged += OnStateChanged;
                core.TextEntry.PropertyChanged += OnTextEntryChanged;
            }
        }

        /// <summary>
        /// Text entry gone: the overlay gives focus back to the box that opened it. From Add profile, a profile kept is
        /// added, and focus goes on to its box, as Add profile's own box, made again with the page, is no longer the one
        /// the overlay remembers; cancelled, it stays on Add profile.
        /// </summary>
        private void OnTextEntryChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(TextEntryState.IsShown) || core.TextEntry.IsShown || profilesBeforeAdd == null)
            {
                return;
            }

            var before = profilesBeforeAdd;
            profilesBeforeAdd = null;
            if (page == Page.Profiles)
            {
                FocusSoon(() => ProfileButton(State.Profiles.FirstOrDefault(p => !before.Contains(p.Id))?.Id)
                    ?? Buttons().LastOrDefault(button => button.DataContext is AddProfileItem));
            }
        }

        /// <summary>A on Theme settings' Personalisation, or Customise opening it: the first box.</summary>
        public void FocusFirst()
        {
            Reset();
            BootVideoBox.Focus();
        }

        /// <summary>The page left, or Settings closed: back to Personalisation's boxes, for next time.</summary>
        public void Reset()
        {
            opened.Clear();
            if (page != Page.Personalisation)
            {
                Show(Page.Personalisation);
            }
        }

        private void Show(Page next)
        {
            page = next;
            PersonalisationPage.Visibility = next == Page.Personalisation ? Visibility.Visible : Visibility.Collapsed;
            BootVideoPage.Visibility = next == Page.BootVideo ? Visibility.Visible : Visibility.Collapsed;
            WhosPlayingPage.Visibility = next == Page.WhosPlaying ? Visibility.Visible : Visibility.Collapsed;
            ProfilesPage.Visibility = next == Page.Profiles ? Visibility.Visible : Visibility.Collapsed;
            ProfilePage.Visibility = next == Page.Profile ? Visibility.Visible : Visibility.Collapsed;
            PicturesPage.Visibility = next == Page.Pictures ? Visibility.Visible : Visibility.Collapsed;
            Fill(next);
            TitleChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>A page's choices, from the core's state now.</summary>
        private void Fill(Page which)
        {
            switch (which)
            {
                case Page.BootVideo:
                    BootVideoRows.ItemsSource = BootVideoNames
                        .Select(name => new ChoiceRow(name.Key, name.Value, name.Key == State.BootVideo)).ToList();
                    break;
                case Page.WhosPlaying:
                    WhosPlayingRows.ItemsSource = new[]
                    {
                        new ChoiceRow(true, "Ask every time", State.AskWhosPlaying),
                        new ChoiceRow(false, "Sign in the last profile", !State.AskWhosPlaying),
                    };
                    break;
                case Page.Profiles:
                    ProfileBoxes.ItemsSource = State.Profiles.Cast<object>().Concat(new[] { new AddProfileItem() }).ToList();
                    break;
                case Page.Profile:
                    var profile = Profile;
                    ProfilePage.DataContext = profile;
                    RemoveBox.Visibility = State.Profiles.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case Page.Pictures:
                    var worn = Profile?.Picture;
                    PictureButtons.ItemsSource = State.Gamerpics.Select(path => new PictureItem(path, path == worn)).ToList();
                    PicturesHint.Visibility = State.Gamerpics.Count > 1 ? Visibility.Collapsed : Visibility.Visible;
                    break;
            }
        }

        /// <summary>The core changed what a page shows: it is filled again, focus kept on the same choice where it can be.</summary>
        private void OnStateChanged(object sender, PropertyChangedEventArgs e)
        {
            var refill = (page == Page.BootVideo && e.PropertyName == nameof(StartUpSettingsState.BootVideo))
                || (page == Page.WhosPlaying && e.PropertyName == nameof(StartUpSettingsState.AskWhosPlaying))
                || ((page == Page.Profiles || page == Page.Profile || page == Page.Pictures) && e.PropertyName == nameof(StartUpSettingsState.Profiles))
                || ((page == Page.Profile || page == Page.Pictures) && e.PropertyName == nameof(StartUpSettingsState.Gamerpics));
            if (!refill)
            {
                return;
            }

            var focused = Buttons().ToList();
            var here = focused.IndexOf(Keyboard.FocusedElement as Button);
            var focusedId = page == Page.Profiles && (Keyboard.FocusedElement as FrameworkElement)?.DataContext is Profile p ? p.Id : null;

            // The profile these pages are of was removed elsewhere: back to Profiles.
            if ((page == Page.Profile || page == Page.Pictures) && Profile == null)
            {
                opened.Clear();
                Show(Page.Profiles);
                FocusSoon(() => Buttons().FirstOrDefault());
                return;
            }

            Fill(page);
            TitleChanged?.Invoke(this, EventArgs.Empty);
            if (here >= 0)
            {
                FocusSoon(() => ProfileButton(focusedId) ?? AtIndex(here));
            }
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnPaneKeyDown.
        private void OnBoxClick(object sender, RoutedEventArgs e) => Press((Button)sender);

        private void OnRowClick(object sender, RoutedEventArgs e) => Press((Button)sender);

        private void OnProfileBoxClick(object sender, RoutedEventArgs e) => Press((Button)sender);

        private void OnPictureClick(object sender, RoutedEventArgs e) => Press((Button)sender);

        /// <summary>
        /// The pad on a page: the D-pad moves between its boxes, row by row, and stays put at the edges; A presses the box
        /// focused; B steps back a page, or from Personalisation's boxes to Theme settings' boxes, as Left from their
        /// first column does. Personalisation's own boxes go by <see cref="Rows"/>, its last rows a box each.
        /// </summary>
        private void OnPaneKeyDown(object sender, KeyEventArgs e)
        {
            // Text entry over the page has the pad: B there cancels it, never steps the pane back a page.
            if (core.TextEntry.IsShown)
            {
                return;
            }

            var buttons = Buttons().ToList();
            var here = buttons.IndexOf(e.OriginalSource as Button);
            if (here < 0)
            {
                return;
            }

            var columns = Columns();
            var pad = PlayniteInput.PadButtonOf(e);
            if (pad == ControllerInput.A)
            {
                e.Handled = true;
                Press(buttons[here]);
            }
            else if (pad == ControllerInput.B)
            {
                // Handled here, before the page's B takes it back to where Settings was opened from.
                e.Handled = true;
                Back();
            }
            else if (page == Page.Personalisation)
            {
                MoveOnBoxes(e);
            }
            else if (e.Key == Key.Left || e.Key == Key.Right)
            {
                e.Handled = true;
                var next = here + (e.Key == Key.Left ? -1 : 1);
                if (next >= 0 && next < buttons.Count && next / columns == here / columns)
                {
                    FocusBox(buttons[next]);
                }
            }
            else if (e.Key == Key.Up || e.Key == Key.Down)
            {
                e.Handled = true;
                var next = here + (e.Key == Key.Up ? -columns : columns);
                if (next >= 0)
                {
                    // Down onto a shorter last row lands on its last box.
                    FocusBox(buttons[Math.Min(next, buttons.Count - 1)]);
                }
            }
        }

        /// <summary>The D-pad on Personalisation's own boxes: along a row, or to the row above or below, at its same column or its last box.</summary>
        private void MoveOnBoxes(KeyEventArgs e)
        {
            var rows = Rows;
            var row = Array.FindIndex(rows, r => Array.IndexOf(r, e.OriginalSource) >= 0);
            if (row < 0)
            {
                return;
            }

            var column = Array.IndexOf(rows[row], e.OriginalSource);
            if (e.Key == Key.Left && column == 0)
            {
                e.Handled = true;
                Back();
            }
            else if (e.Key == Key.Left || e.Key == Key.Right)
            {
                e.Handled = true;
                var next = column + (e.Key == Key.Left ? -1 : 1);
                if (next < rows[row].Length)
                {
                    FocusBox(rows[row][next]);
                }
            }
            else if (e.Key == Key.Up || e.Key == Key.Down)
            {
                e.Handled = true;
                var next = row + (e.Key == Key.Up ? -1 : 1);
                if (next >= 0 && next < rows.Length)
                {
                    FocusBox(rows[next][Math.Min(column, rows[next].Length - 1)]);
                }
            }
        }

        private void Press(Button button)
        {
            if (button == BootVideoBox)
            {
                Open(Page.BootVideo, button);
                FocusSoon(() => AtIndex(BootVideoNames.ToList().FindIndex(name => name.Key == State.BootVideo)));
            }
            else if (button == ProfilesBox)
            {
                Open(Page.Profiles, button);
                FocusSoon(() => AtIndex(0));
            }
            else if (button == AskBox)
            {
                Open(Page.WhosPlaying, button);
                FocusSoon(() => AtIndex(State.AskWhosPlaying ? 0 : 1));
            }
            else if (button == BackgroundDelayBox)
            {
                core.SetBackgroundDelay(!State.BackgroundDelay);
            }
            else if (button.DataContext is ChoiceRow row && row.Value is BootVideo video)
            {
                core.SetBootVideo(video);
            }
            else if (button.DataContext is ChoiceRow askRow && askRow.Value is bool ask)
            {
                core.SetAskWhosPlaying(ask);
            }
            else if (button.DataContext is AddProfileItem)
            {
                // Text entry over the page; the profile is added once its gamertag is kept (OnTextEntryChanged).
                profilesBeforeAdd = new HashSet<string>(State.Profiles.Select(p => p.Id));
                core.TypeNewProfile();
                if (!core.TextEntry.IsShown)
                {
                    profilesBeforeAdd = null;
                }
            }
            else if (button == GamertagBox && Profile is Profile gamertagOf)
            {
                core.TypeProfileGamertag(gamertagOf.Id);
            }
            else if (button == RealNameBox && Profile is Profile realNameOf)
            {
                core.TypeProfileRealName(realNameOf.Id);
            }
            else if (button == PictureBox && Profile is Profile pictureOf)
            {
                if (State.Gamerpics.Count > 0)
                {
                    Open(Page.Pictures, button);
                    FocusSoon(() => AtIndex(Math.Max(0, State.Gamerpics.ToList().IndexOf(pictureOf.Picture))));
                }
                else
                {
                    // Nothing to choose from opens nothing, saying where pictures come from.
                    API.Instance.Dialogs.ShowMessage("Add images to the Gamerpics folder to choose from.", "Picture");
                    FocusSoon(() => PictureBox);
                }
            }
            else if (button == RemoveBox && Profile is Profile removing && State.Profiles.Count > 1)
            {
                var sure = API.Instance.Dialogs.ShowMessage($"Remove {removing.Gamertag}?", "Remove profile", MessageBoxButton.YesNo);
                if (sure == MessageBoxResult.Yes)
                {
                    // Back to Profiles first, so the profiles changing refills it, not the page of the one gone; focus
                    // on the profile now in its place, or Add profile after the last.
                    var index = State.Profiles.ToList().FindIndex(p => p.Id == removing.Id);
                    opened.Pop();
                    Show(Page.Profiles);
                    core.RemoveProfile(removing.Id);
                    FocusSoon(() => AtIndex(index));
                }
                else
                {
                    FocusSoon(() => RemoveBox);
                }
            }
            else if (button.DataContext is PictureItem picture && Profile is Profile wearer)
            {
                // Back first, so the profile changing refills the Profile page, not this one.
                Back();
                core.SetProfilePicture(wearer.Id, picture.Path);
            }
            else if (page == Page.Profiles && button.DataContext is Profile profile)
            {
                profileId = profile.Id;
                Open(Page.Profile, profile.Id);
                FocusSoon(() => GamertagBox);
            }
        }

        /// <summary>Opens a page in place of this one, remembering the box that opened it for B.</summary>
        private void Open(Page next, object opener)
        {
            opened.Push(new Opened(page, opener));
            Show(next);
        }

        /// <summary>B: the page before, focus on the box that opened this one; from Personalisation's boxes, Theme settings' boxes.</summary>
        private void Back()
        {
            if (opened.Count == 0)
            {
                BackToThemeSettings?.Invoke(this, EventArgs.Empty);
                return;
            }

            var back = opened.Pop();
            Show(back.Page);
            FocusSoon(() => back.Opener as Button ?? ProfileButton(back.Opener as string) ?? AtIndex(0));
        }

        /// <summary>
        /// The page showing's boxes, in reading order, as laid out: not the visual tree's, which a panel keeps in z-order,
        /// so the focused box, raised over its neighbours for its glow, would come last. A hidden one, as Remove profile
        /// with one profile, aside.
        /// </summary>
        private IEnumerable<Button> Buttons()
        {
            FrameworkElement shown;
            switch (page)
            {
                case Page.BootVideo: shown = BootVideoRows; break;
                case Page.WhosPlaying: shown = WhosPlayingRows; break;
                case Page.Profiles: shown = ProfileBoxes; break;
                case Page.Profile: shown = ProfilePage; break;
                case Page.Pictures: shown = PictureButtons; break;
                default: shown = PersonalisationPage; break;
            }

            return Descendants(shown).OfType<Button>()
                .Where(button => button.IsVisible)
                .Select(button => new { Button = button, At = button.TranslatePoint(new Point(), shown) })
                .OrderBy(box => Math.Round(box.At.Y))
                .ThenBy(box => box.At.X)
                .Select(box => box.Button);
        }

        /// <summary>How many boxes a row of the page showing holds.</summary>
        private int Columns()
        {
            switch (page)
            {
                case Page.BootVideo:
                case Page.WhosPlaying:
                    return 1;
                case Page.Pictures: return 4;
                default: return 2;
            }
        }

        private Button AtIndex(int index)
        {
            var buttons = Buttons().ToList();
            return buttons.Count == 0 ? null : buttons[Math.Max(0, Math.Min(buttons.Count - 1, index))];
        }

        /// <summary>A profile's box on the Profiles page.</summary>
        private Button ProfileButton(string id) =>
            id == null || page != Page.Profiles ? null : Buttons().FirstOrDefault(button => button.DataContext is Profile p && p.Id == id);

        private static void FocusBox(Button button)
        {
            if (button != null && button.Focus())
            {
                button.BringIntoView();
            }
        }

        /// <summary>Focuses a box once the page is laid out, its choices made: a page just shown has none until then.</summary>
        private void FocusSoon(Func<Button> which)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (IsVisible)
                {
                    UpdateLayout();
                    FocusBox(which());
                }
            }));
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                yield return child;
                foreach (var descendant in Descendants(child))
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>A page left for another, and what opened it: a box, or a profile by Id, its box being remade.</summary>
        private sealed class Opened
        {
            public Opened(Page page, object opener)
            {
                Page = page;
                Opener = opener;
            }

            public Page Page { get; }

            public object Opener { get; }
        }
    }

    /// <summary>A choice in a page's list, as a generation on Boot video's, ticked while it's the one set.</summary>
    internal sealed class ChoiceRow
    {
        public ChoiceRow(object value, string name, bool isCurrent)
        {
            Value = value;
            Name = name;
            IsCurrent = isCurrent;
        }

        /// <summary>What choosing it sets: a <see cref="BootVideo"/>, or whether Who's playing today? asks.</summary>
        public object Value { get; }

        public string Name { get; }

        public bool IsCurrent { get; }
    }

    /// <summary>The Profiles page's last box, Add profile.</summary>
    internal sealed class AddProfileItem
    {
    }

    /// <summary>A gamerpic on the Picture page, ticked while the profile wears it.</summary>
    internal sealed class PictureItem
    {
        public PictureItem(string path, bool isCurrent)
        {
            Path = path;
            IsCurrent = isCurrent;
        }

        public string Path { get; }

        public bool IsCurrent { get; }
    }
}
