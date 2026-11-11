using System;

namespace XboxSeries.Core
{
    /// <summary>
    /// A game's own art from the Xbox store, as the console's tiles show it, saved in the extension's data folder: its
    /// box art, square, and its poster, 2:3. Either can be missing, as a store page can lack one.
    /// </summary>
    public sealed class XboxArt : IEquatable<XboxArt>
    {
        public XboxArt(string boxArt, string poster)
        {
            BoxArt = boxArt;
            Poster = poster;
        }

        /// <summary>The full path of the game's box art, square; null when the store has none.</summary>
        public string BoxArt { get; }

        /// <summary>The full path of the game's poster, 2:3; null when the store has none.</summary>
        public string Poster { get; }

        public bool Equals(XboxArt other) => other != null && BoxArt == other.BoxArt && Poster == other.Poster;

        public override bool Equals(object obj) => Equals(obj as XboxArt);

        public override int GetHashCode() => (BoxArt?.GetHashCode() ?? 0) ^ (Poster?.GetHashCode() ?? 0);
    }

    /// <summary>A game tile's shape, which says which of the game's art fits it.</summary>
    public enum TileShape
    {
        /// <summary>Most of the Dashboard's tiles: the Recent row's, Search's, Play history's, the Queue's.</summary>
        Square,

        /// <summary>A Group's cards on Home, and the Game hub's cover card: taller than wide.</summary>
        Tall,
    }

    /// <summary>
    /// The art a game's tile shows, chosen for its shape as the console chooses it: a square tile the game's Xbox box
    /// art, a tall one its Xbox poster, so that neither is cropped to a shape it wasn't drawn for, cutting off its logo.
    /// Without that art, Playnite's cover, which the tile crops from the top so a logo at the top of it stays.
    /// </summary>
    public static class TileArt
    {
        /// <summary>The art for a tile of <paramref name="shape"/>: the Xbox art of that shape, else <paramref name="cover"/>.</summary>
        public static string For(TileShape shape, string cover, XboxArt xboxArt) => XboxArtFor(shape, xboxArt) ?? cover;

        /// <summary>The Xbox art of <paramref name="shape"/> alone; null when there is none, and the cover stands.</summary>
        public static string XboxArtFor(TileShape shape, XboxArt xboxArt) =>
            shape == TileShape.Square ? xboxArt?.BoxArt : xboxArt?.Poster;
    }
}
