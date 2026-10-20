using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Playnite.SDK.Events;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Settings' Playnite settings category: Download metadata and Playnite settings side by side, then Update after
    /// downloads on a row of its own, its info beside it while it has focus. Every box acts at once and opens no page:
    /// Download metadata starts the run for the games missing metadata, or cancels one going; Playnite settings opens
    /// Playnite's own Fullscreen Settings window; Update after downloads turns the setting over. While metadata
    /// downloads, B on the pane cancels it rather than going back. The pane holds no logic; every action goes to the core.
    /// </summary>
    public partial class PlaynitePane : UserControl
    {
        private ExtensionCore core;

        public PlaynitePane()
        {
            InitializeComponent();
        }

        /// <summary>B, or Left from the first column, on the boxes: back to the rail's Playnite settings.</summary>
        public event EventHandler BackToRail;

        /// <summary>The boxes as the pad moves between them: a row each, Update after downloads alone on the second.</summary>
        private Button[][] Rows => new[]
        {
            new[] { DownloadMetadataBox, PlayniteSettingsBox },
            new[] { UpdateAfterDownloadsBox },
        };

        internal void Attach(ExtensionCore extensionCore)
        {
            core = extensionCore;
            DataContext = core.Settings.Playnite;
        }

        /// <summary>Right or A on the rail's Playnite settings: the first box.</summary>
        public void FocusFirst()
        {
            DownloadMetadataBox.Focus();
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnPaneKeyDown, Playnite raising it as KeyDown alone.
        private void OnBoxClick(object sender, RoutedEventArgs e) => Press((Button)sender);

        /// <summary>
        /// The pad on the boxes: the D-pad moves between them, row by row, staying put at the edges, Down from Playnite
        /// settings landing on Update after downloads; A presses the box focused; B, or Left from the first column, goes
        /// back to the rail, but while metadata downloads B cancels it instead and focus stays.
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
            else if (pad == ControllerInput.B && core.Settings.Playnite.IsDownloadingMetadata)
            {
                // Handled here, before the page's B takes it back to where Settings was opened from.
                e.Handled = true;
                core.CancelMetadataDownload();
            }
            else if (pad == ControllerInput.B || (e.Key == Key.Left && column == 0))
            {
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
            var state = core.Settings.Playnite;
            if (box == DownloadMetadataBox)
            {
                if (state.IsDownloadingMetadata)
                {
                    core.CancelMetadataDownload();
                }
                else
                {
                    core.DownloadMissingMetadata();
                }
            }
            else if (box == PlayniteSettingsBox)
            {
                core.OpenPlayniteSettings();
            }
            else if (box == UpdateAfterDownloadsBox)
            {
                core.SetUpdateAfterDownloads(!state.UpdateAfterDownloads);
            }
        }
    }
}
