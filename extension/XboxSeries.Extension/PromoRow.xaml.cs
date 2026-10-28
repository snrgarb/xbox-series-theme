using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Playnite.SDK.Events;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The four tiles under Home's Recent row: Discord, Playnite settings, Achievements and Downloads. A on one asks the
    /// core to open where it goes; it holds no other logic: <see cref="RecentRowControl"/>, which hosts it, moves focus
    /// into, along and out of it.
    /// </summary>
    public partial class PromoRow : UserControl
    {
        private ExtensionCore core;

        public PromoRow()
        {
            InitializeComponent();
        }

        internal void Attach(ExtensionCore extensionCore)
        {
            core = extensionCore;
            AchievementsTile.DataContext = core.Status;
            DownloadsTile.DataContext = core.MyGames;
        }

        // The keyboard's Enter and the mouse; the pad's A arrives as OnTileKeyDown, Playnite raising it as KeyDown alone.
        private void OnTileClick(object sender, RoutedEventArgs e) => Open(sender);

        private void OnTileKeyDown(object sender, KeyEventArgs e)
        {
            if (PlayniteInput.PadButtonOf(e) == ControllerInput.A)
            {
                e.Handled = true;
                Open(sender);
            }
        }

        private void Open(object tile)
        {
            if (tile == DiscordTile)
            {
                core.OpenDiscordFromHome();
            }
            else if (tile == PlayniteSettingsTile)
            {
                core.OpenPlayniteSettingsFromHome();
            }
            else if (tile == AchievementsTile)
            {
                core.OpenProfile();
            }
            else if (tile == DownloadsTile)
            {
                core.OpenQueueFromHome();
            }
        }

        /// <summary>The tiles, left to right.</summary>
        internal IReadOnlyList<Button> AllTiles => Tiles.Children.OfType<Button>().ToList();

        /// <summary>The tile nearest below <paramref name="from"/>, by their centres across the screen.</summary>
        internal Button NearestTo(FrameworkElement from)
        {
            var centre = from.TranslatePoint(new Point(from.ActualWidth / 2, 0), this).X;
            return AllTiles.OrderBy(tile => System.Math.Abs(tile.TranslatePoint(new Point(tile.ActualWidth / 2, 0), this).X - centre)).First();
        }
    }
}
