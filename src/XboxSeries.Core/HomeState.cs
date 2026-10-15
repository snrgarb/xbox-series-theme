using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>What the extension's Home elements show. Views bind to it and hold no logic of their own.</summary>
    public sealed class HomeState : INotifyPropertyChanged
    {
        private IReadOnlyList<HomeTile> recentRow = new HomeTile[0];
        private IReadOnlyList<HomeTile> playHistory = new HomeTile[0];
        private IReadOnlyList<HomeGroup> groups = new HomeGroup[0];
        private HeroArt hero;
        private Dialog menu;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>The Recent row's game tiles, left to right. The Play history tile follows them.</summary>
        public IReadOnlyList<HomeTile> RecentRow
        {
            get => recentRow;
            internal set => Set(ref recentRow, value, nameof(RecentRow));
        }

        /// <summary>The Play history tile's collage: up to 4 played games after those in the Recent row.</summary>
        public IReadOnlyList<HomeTile> PlayHistory
        {
            get => playHistory;
            internal set => Set(ref playHistory, value, nameof(PlayHistory));
        }

        /// <summary>The Groups below the Recent row, top to bottom. Each ends in a See all tile.</summary>
        public IReadOnlyList<HomeGroup> Groups
        {
            get => groups;
            internal set => Set(ref groups, value, nameof(Groups));
        }

        /// <summary>
        /// The art behind Home, from the Recent-row game that has focus; null for the green swirl, as while focus is
        /// anywhere else, or on a game with no art.
        /// </summary>
        public HeroArt Hero
        {
            get => hero;
            internal set => Set(ref hero, value, nameof(Hero));
        }

        /// <summary>
        /// The tile menu, shown centred over Home for the focused tile, which has the pad until it is closed; null when there is none.
        /// </summary>
        public Dialog TileMenu
        {
            get => menu;
            internal set => Set(ref menu, value, nameof(TileMenu));
        }

        private void Set<T>(ref T field, T value, string name)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>A Group on Home: a row of one filter preset's games, or of the user's favourites by default.</summary>
    public sealed class HomeGroup
    {
        internal HomeGroup(string name, Guid? filterPresetId, IReadOnlyList<HomeTile> tiles, IReadOnlyList<HomeTile> seeAll)
        {
            Name = name;
            FilterPresetId = filterPresetId;
            Tiles = tiles;
            SeeAll = seeAll;
        }

        public string Name { get; }

        /// <summary>The filter preset the Group shows; null for the default Favourites Group.</summary>
        public Guid? FilterPresetId { get; }

        /// <summary>The Group's first games, left to right. The See all tile follows them.</summary>
        public IReadOnlyList<HomeTile> Tiles { get; }

        /// <summary>The See all tile's collage: the Group's next games after its tiles, like Play history's. Empty when they all fit.</summary>
        public IReadOnlyList<HomeTile> SeeAll { get; }
    }

    /// <summary>One game tile on the Dashboard: on Home, or a game in My games &amp; apps' Play history.</summary>
    public sealed class HomeTile
    {
        internal HomeTile(Guid gameId, string title, string coverImage, string source, bool isRunning)
        {
            GameId = gameId;
            Title = title;
            CoverImage = coverImage;
            Source = source;
            IsRunning = isRunning;
        }

        public Guid GameId { get; }
        public string Title { get; }

        /// <summary>The full path or URL of the game's cover art; null when it has none.</summary>
        public string CoverImage { get; }

        /// <summary>The game's library source (Steam, Xbox, ...), shown as its icon; null for games without one.</summary>
        public string Source { get; }

        public bool IsRunning { get; }
    }

    /// <summary>The hero's art: a game's background art, or its cover to be shown blurred and darkened.</summary>
    public sealed class HeroArt : IEquatable<HeroArt>
    {
        internal HeroArt(Guid gameId, string image, bool isCover)
        {
            GameId = gameId;
            Image = image;
            IsCover = isCover;
        }

        public Guid GameId { get; }

        /// <summary>The full path or URL of the image.</summary>
        public string Image { get; }

        /// <summary>True when <see cref="Image"/> is the cover, standing in for missing background art.</summary>
        public bool IsCover { get; }

        public bool Equals(HeroArt other) =>
            other != null && GameId == other.GameId && Image == other.Image && IsCover == other.IsCover;

        public override bool Equals(object obj) => Equals(obj as HeroArt);

        public override int GetHashCode() => GameId.GetHashCode() ^ (Image?.GetHashCode() ?? 0);
    }
}
