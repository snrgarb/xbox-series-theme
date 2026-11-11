using System;
using System.Windows;
using System.Windows.Controls;
using Playnite.SDK.Controls;
using Playnite.SDK.Models;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The game's Xbox art over one of the theme's own tiles, which are loose XAML and can't fetch it: placed by the
    /// Dashboard as XboxSeries_BoxArt on My games &amp; apps' square grid tiles and XboxSeries_Poster on the Game hub's tall
    /// cover card. It draws the art as the theme draws the cover beneath it, in the theme's XboxTileArt, so its corners
    /// and cropping are the tile's. While the game has no Xbox art of that shape, or Xbox store art is off, it is empty
    /// and the theme's cover shows. A <see cref="PluginUserControl"/>, because Playnite binds any other custom element's
    /// DataContext to the selected game, not the tile's.
    /// </summary>
    public sealed class XboxArtElement : PluginUserControl
    {
        private readonly ExtensionCore core;
        private readonly TileShape shape;
        private readonly int decodeWidth;
        private readonly ContentControl art = new ContentControl { Focusable = false, IsTabStop = false };
        private Guid? gameId;
        private string shown;

        /// <param name="decodeWidth">The width the art is decoded at, as the extension's own tiles of the shape decode theirs.</param>
        internal XboxArtElement(ExtensionCore core, TileShape shape, int decodeWidth)
        {
            this.core = core;
            this.shape = shape;
            this.decodeWidth = decodeWidth;
            Focusable = false;
            IsTabStop = false;
            IsHitTestVisible = false;
            art.SetResourceReference(StyleProperty, "XboxTileArt");
            Content = art;
            Visibility = Visibility.Collapsed;

            // Followed only while on screen: a grid of hundreds of tiles holds none of them alive from the core's event.
            Loaded += (s, e) =>
            {
                core.XboxArtChanged -= Refresh;
                core.XboxArtChanged += Refresh;
                Refresh();
            };
            Unloaded += (s, e) => core.XboxArtChanged -= Refresh;
        }

        public override void GameContextChanged(Game oldContext, Game newContext)
        {
            gameId = newContext?.Id;
            Refresh();
        }

        /// <summary>Shows the game's Xbox art as it is now, decoded once per change, or nothing.</summary>
        private void Refresh()
        {
            var path = gameId == null ? null : core.XboxArtFor(gameId.Value, shape);
            if (path == shown)
            {
                return;
            }

            shown = path;
            var image = DecodedImageConverter.Load(path, decodeWidth);
            art.Content = image;
            Visibility = image == null ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
