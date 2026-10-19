using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK.Events;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Debug builds only (the csproj leaves it out of others): Settings' Debug menu, laid out as Personalisation. Its boxes
    /// open Notifications, the Toasts and calls to try, and Steam, the install trials of #59 with their target and tools,
    /// whose kinds of install each open a page of their own. The pad moves between a page's boxes; A opens or presses,
    /// and B steps back a page at a time, focus going back to the box that opened it, then from the menu's boxes to the
    /// rail.
    /// </summary>
    public partial class DebugPane : UserControl
    {
        private readonly ExtensionCore core;
        private readonly DebugPage menu;
        private readonly Stack<Opened> opened = new Stack<Opened>();
        private DebugPage page;

        internal DebugPane(ExtensionCore core)
        {
            InitializeComponent();
            this.core = core;

            var notifications = new DebugPage("Notifications", "Each puts up a Toast, as Playnite's own would.", new[]
            {
                new DebugBox("Notification", "", () => core.DebugNotify("Library updated")),
                new DebugBox("Screenshot notification", "", () => core.DebugNotify("Screenshot failed 0x82323018")),
                new DebugBox("Long notification", "", () => core.DebugNotify(
                    "Library updated: 14 games added, 3 games given new covers and descriptions, and 2 games removed "
                    + "because their install folders could no longer be found on this PC or any of its drives")),
                new DebugBox("Three notifications", "", () =>
                {
                    core.DebugNotify("Library updated");
                    core.DebugNotify("Screenshot failed 0x82323018");
                    core.DebugNotify("Controller disconnected");
                }),
                new DebugBox("Achievement", "", () => core.DebugAchievementsUnlocked(1, false)),
                new DebugBox("Rare achievement", "", () => core.DebugAchievementsUnlocked(1, true)),
                new DebugBox("Three achievements", "", () => core.DebugAchievementsUnlocked(3, false)),
                new DebugBox("Call", "", () => core.DebugRing("Jakob", null)),
                new DebugBox("Call with avatar", "", () => core.DebugRing("Jakob", "https://cdn.discordapp.com/embed/avatars/0.png")),
                new DebugBox("Call, avatar fails", "", () => core.DebugRing("Jakob", "https://cdn.discordapp.com/embed/avatars/does-not-exist.png")),
            });

            var consoleInstalls = new DebugPage("Console installs", "Each hands steam.exe a command line.", new[]
            {
                Trial("+app_install", "", SteamDownloads.Trial.Console),
                Trial("-silent +app_install", "", SteamDownloads.Trial.ConsoleSilent),
                Trial("+app_install, library 0", "", SteamDownloads.Trial.ConsoleLibrary0),
                Trial("The hub's install", "", SteamDownloads.Trial.Hub),
                Trial("steam://install", "", SteamDownloads.Trial.InstallUrl),
            });

            var cefInstalls = new DebugPage("CEF installs", "Each runs Steam's own script over its CEF debugger, which must be on; the last types into its console.", new[]
            {
                Trial("Wizard, continued", "", SteamDownloads.Trial.CefWizard),
                Trial("QueueAppUpdate", "", SteamDownloads.Trial.CefQueue),
                Trial("Console app_install", "", SteamDownloads.Trial.CefConsole),
            });

            var stubInstalls = new DebugPage("Stub installs", "Each writes an appmanifest stub saying the target needs downloading, then:", new[]
            {
                Trial("CEF QueueAppUpdate", "", SteamDownloads.Trial.StubCefQueue),
                Trial("app_set_update_flag", "", SteamDownloads.Trial.StubUpdateFlag),
                Trial("Restart Steam", "", SteamDownloads.Trial.StubRestart),
            });

            var steam = new DebugPage("Steam", "Each install reports what Steam showed as a Toast, 20 seconds on.", new[]
            {
                new DebugBox("Change target", "", () => SteamTrials?.NextTrialTarget()),
                new DebugBox("Request licence", "", () => SteamTrials?.RequestTrialLicence(core.DebugNotify)),
                new DebugBox("Uninstall target", "", () => SteamTrials?.UninstallTrialTarget(core.DebugNotify)),
                new DebugBox("CEF debugging", "", () => SteamTrials?.ToggleCefDebugging(core.DebugNotify)),
                new DebugBox("Console installs", "", consoleInstalls),
                new DebugBox("CEF installs", "", cefInstalls),
                new DebugBox("Stub installs", "", stubInstalls),
            });

            foreach (var steamPage in new[] { steam, consoleInstalls, cefInstalls, stubInstalls })
            {
                steamPage.ShowsSteam = true;
            }

            menu = new DebugPage("Debug menu", null, new[]
            {
                new DebugBox("Notifications", "", notifications),
                new DebugBox("Steam", "", steam),
            });
            Show(menu);
        }

        /// <summary>B, or Left from the first column, on the menu's boxes: back to the rail's Debug menu.</summary>
        public event EventHandler BackToRail;

        /// <summary>The page showing changed, and with it <see cref="Title"/>.</summary>
        public event EventHandler TitleChanged;

        /// <summary>The header over the page showing.</summary>
        public string Title => page.Title;

        /// <summary>The Steam adapter whose install trials the Steam pages run; none, and they do nothing.</summary>
        internal SteamDownloads SteamTrials { get; set; }

        /// <summary>Right or A on the rail's Debug menu: the first box.</summary>
        public void FocusFirst()
        {
            Reset();
            FocusSoon(0);
        }

        /// <summary>The category left, or Settings closed: back to the menu's boxes, for next time.</summary>
        public void Reset()
        {
            opened.Clear();
            if (page != menu)
            {
                Show(menu);
            }
        }

        private DebugBox Trial(string name, string icon, SteamDownloads.Trial trial) =>
            new DebugBox(name, icon, () => SteamTrials?.RunTrial(trial, core.DebugNotify));

        private void Show(DebugPage next)
        {
            page = next;
            Boxes.ItemsSource = next.Boxes;
            ShowHint();
            TitleChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>The line over the page's boxes; on Steam's, the target and CEF debugging as they are now.</summary>
        private void ShowHint()
        {
            var hint = page.Hint;
            if (page.ShowsSteam)
            {
                var target = SteamTrials?.TrialTargetName ?? "none";
                var cef = SteamTrials?.IsCefDebuggingOn == true ? "on" : "off";
                hint = $"Target: {target}. CEF debugging: {cef}. {hint}";
            }

            Hint.Text = hint ?? "";
            Hint.Visibility = hint == null ? Visibility.Collapsed : Visibility.Visible;
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnPaneKeyDown.
        private void OnBoxClick(object sender, RoutedEventArgs e) => Press((Button)sender);

        /// <summary>
        /// The pad on a page: the D-pad moves between its boxes, row by row, and stays put at the edges; A presses the box
        /// focused; B steps back a page, or from the menu's boxes to the rail, as Left from their first column does.
        /// </summary>
        private void OnPaneKeyDown(object sender, KeyEventArgs e)
        {
            var here = page.Boxes.IndexOf((e.OriginalSource as Button)?.DataContext as DebugBox);
            if (here < 0)
            {
                return;
            }

            const int columns = 2;
            var pad = PlayniteInput.PadButtonOf(e);
            if (pad == ControllerInput.A)
            {
                e.Handled = true;
                Press((Button)e.OriginalSource);
            }
            else if (pad == ControllerInput.B || (e.Key == Key.Left && here % columns == 0 && page == menu))
            {
                // Handled here, before the page's B takes it back to where Settings was opened from.
                e.Handled = true;
                Back();
            }
            else if (e.Key == Key.Left || e.Key == Key.Right)
            {
                e.Handled = true;
                var next = here + (e.Key == Key.Left ? -1 : 1);
                if (next >= 0 && next < page.Boxes.Count && next / columns == here / columns)
                {
                    FocusAt(next);
                }
            }
            else if (e.Key == Key.Up || e.Key == Key.Down)
            {
                e.Handled = true;
                var next = here + (e.Key == Key.Up ? -columns : columns);
                if (next >= 0)
                {
                    // Down onto a shorter last row lands on its last box.
                    FocusAt(Math.Min(next, page.Boxes.Count - 1));
                }
            }
        }

        private void Press(Button button)
        {
            if (!(button.DataContext is DebugBox box))
            {
                return;
            }

            if (box.Opens != null)
            {
                opened.Push(new Opened(page, page.Boxes.IndexOf(box)));
                Show(box.Opens);
                FocusSoon(0);
                return;
            }

            box.Press();
            ShowHint();
        }

        /// <summary>B: the page before, focus on the box that opened this one; from the menu's boxes, the rail.</summary>
        private void Back()
        {
            if (opened.Count == 0)
            {
                BackToRail?.Invoke(this, EventArgs.Empty);
                return;
            }

            var back = opened.Pop();
            Show(back.Page);
            FocusSoon(back.Index);
        }

        private void FocusAt(int index)
        {
            var holder = Boxes.ItemContainerGenerator.ContainerFromIndex(index) as ContentPresenter;
            if (holder != null && VisualTreeHelper.GetChildrenCount(holder) > 0 && VisualTreeHelper.GetChild(holder, 0) is Button button)
            {
                button.Focus();
            }
        }

        /// <summary>Focuses a box once the page is laid out: a page just shown has no boxes until then.</summary>
        private void FocusSoon(int index)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (IsVisible)
                {
                    UpdateLayout();
                    FocusAt(index);
                }
            }));
        }

        /// <summary>A page left for another, and the box on it that opened the other.</summary>
        private sealed class Opened
        {
            public Opened(DebugPage page, int index)
            {
                Page = page;
                Index = index;
            }

            public DebugPage Page { get; }

            public int Index { get; }
        }
    }

    /// <summary>A page of the Debug menu: its header, the line over its boxes, and the boxes.</summary>
    internal sealed class DebugPage
    {
        public DebugPage(string title, string hint, IReadOnlyList<DebugBox> boxes)
        {
            Title = title;
            Hint = hint;
            Boxes = boxes.ToList();
        }

        public string Title { get; }

        public string Hint { get; }

        public List<DebugBox> Boxes { get; }

        /// <summary>One of Steam's pages, whose line starts with the target and whether CEF debugging is on.</summary>
        public bool ShowsSteam { get; set; }
    }

    /// <summary>A box on a Debug menu page: what it does when pressed, or the page it opens.</summary>
    internal sealed class DebugBox
    {
        public DebugBox(string name, string icon, Action press)
        {
            Name = name;
            Icon = icon;
            Press = press;
        }

        public DebugBox(string name, string icon, DebugPage opens)
        {
            Name = name;
            Icon = icon;
            Opens = opens;
        }

        public string Name { get; }

        /// <summary>Its Segoe Fluent Icons / MDL2 Assets glyph.</summary>
        public string Icon { get; }

        public Action Press { get; }

        public DebugPage Opens { get; }
    }
}
