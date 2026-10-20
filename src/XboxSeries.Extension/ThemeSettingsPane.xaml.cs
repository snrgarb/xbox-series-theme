using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Playnite.SDK.Events;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Settings' Theme settings category: Personalisation and Guide side by side, then Discord, each opening its page in
    /// the pane. A box asks the core for its page (<see cref="ExtensionCore.OpenThemePage"/>); Settings shows the page and
    /// moves focus into it. The pane holds no logic.
    /// </summary>
    public partial class ThemeSettingsPane : UserControl
    {
        private ExtensionCore core;

        public ThemeSettingsPane()
        {
            InitializeComponent();
        }

        /// <summary>B, or Left from the first column, on the boxes: back to the rail's Theme settings.</summary>
        public event EventHandler BackToRail;

        /// <summary>
        /// The boxes as the pad moves between them, a row each, in reading order: not the panel's children, which a
        /// focused box raised over its neighbours reorders.
        /// </summary>
        private Button[][] Rows => new[]
        {
            new[] { PersonalisationBox, GuideBox },
            new[] { DiscordBox },
        };

        internal void Attach(ExtensionCore extensionCore)
        {
            core = extensionCore;
        }

        /// <summary>Right or A on the rail's Theme settings: the first box.</summary>
        public void FocusFirst()
        {
            PersonalisationBox.Focus();
        }

        /// <summary>A page left: the box that opened it.</summary>
        public void FocusBox(ThemeSettingsPage page)
        {
            BoxOf(page)?.Focus();
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnPaneKeyDown, Playnite raising it as KeyDown alone.
        private void OnBoxClick(object sender, RoutedEventArgs e) => Press((Button)sender);

        /// <summary>
        /// The pad on the boxes: the D-pad moves between them, row by row, staying put at the edges, Down from Guide
        /// landing on Discord; A opens the box's page; B, or Left from the first column, goes back to the rail.
        /// </summary>
        private void OnPaneKeyDown(object sender, KeyEventArgs e)
        {
            var rows = Rows;
            var row = Array.FindIndex(rows, r => Array.IndexOf(r, e.OriginalSource) >= 0);
            if (row < 0)
            {
                return;
            }

            var column = Array.IndexOf(rows[row], e.OriginalSource);
            var pad = PlayniteInput.PadButtonOf(e);
            if (pad == ControllerInput.A)
            {
                e.Handled = true;
                Press((Button)e.OriginalSource);
            }
            else if (pad == ControllerInput.B || (e.Key == Key.Left && column == 0))
            {
                // Handled here, before the page's B takes it back to where Settings was opened from.
                e.Handled = true;
                BackToRail?.Invoke(this, EventArgs.Empty);
            }
            else if (e.Key == Key.Left || e.Key == Key.Right)
            {
                e.Handled = true;
                var next = column + (e.Key == Key.Left ? -1 : 1);
                if (next >= 0 && next < rows[row].Length)
                {
                    rows[row][next].Focus();
                }
            }
            else if (e.Key == Key.Up || e.Key == Key.Down)
            {
                e.Handled = true;
                var next = row + (e.Key == Key.Up ? -1 : 1);
                if (next >= 0 && next < rows.Length)
                {
                    rows[next][Math.Min(column, rows[next].Length - 1)].Focus();
                }
            }
        }

        private void Press(Button box)
        {
            var page = box == PersonalisationBox ? ThemeSettingsPage.Personalisation
                : box == GuideBox ? ThemeSettingsPage.Guide
                : box == DiscordBox ? ThemeSettingsPage.Discord
                : ThemeSettingsPage.None;
            if (page != ThemeSettingsPage.None)
            {
                core.OpenThemePage(page);
            }
        }

        private Button BoxOf(ThemeSettingsPage page)
        {
            switch (page)
            {
                case ThemeSettingsPage.Personalisation: return PersonalisationBox;
                case ThemeSettingsPage.Guide: return GuideBox;
                case ThemeSettingsPage.Discord: return DiscordBox;
                default: return null;
            }
        }
    }
}
