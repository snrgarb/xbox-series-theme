using System;
using System.Collections.Generic;

namespace XboxSeries.Core
{
    /// <summary>Which part of Home has focus, and so where its page rests.</summary>
    public enum HomeRestKind
    {
        /// <summary>The Recent row, or the top bar above it.</summary>
        RecentRow,

        /// <summary>The wide tiles under the Recent row.</summary>
        WideTiles,

        /// <summary>One of the Groups.</summary>
        Group,
    }

    /// <summary>Where focus is on Home, as far as the page's resting point goes: the Recent row, the wide tiles or a Group, by its place.</summary>
    public readonly struct HomeFocus : IEquatable<HomeFocus>
    {
        private HomeFocus(HomeRestKind kind, int group)
        {
            Kind = kind;
            GroupIndex = group;
        }

        public static HomeFocus RecentRow => new HomeFocus(HomeRestKind.RecentRow, 0);

        public static HomeFocus WideTiles => new HomeFocus(HomeRestKind.WideTiles, 0);

        public static HomeFocus Group(int index) => new HomeFocus(HomeRestKind.Group, Math.Max(0, index));

        public HomeRestKind Kind { get; }

        /// <summary>The Group's place, top to bottom from 0; 0 off the Groups.</summary>
        public int GroupIndex { get; }

        public bool Equals(HomeFocus other) => Kind == other.Kind && GroupIndex == other.GroupIndex;

        public override bool Equals(object obj) => obj is HomeFocus other && Equals(other);

        public static bool operator ==(HomeFocus a, HomeFocus b) => a.Equals(b);

        public static bool operator !=(HomeFocus a, HomeFocus b) => !a.Equals(b);

        public override int GetHashCode() => ((int)Kind * 397) ^ GroupIndex;

        public override string ToString() => Kind == HomeRestKind.Group ? $"Group {GroupIndex}" : Kind.ToString();
    }

    /// <summary>
    /// Home laid out with its page at rest on the Recent row, in the Dashboard's pixels (1080 tall, whatever the screen):
    /// where each Group's header starts, and where its cards start under it and how tall they are. Measured by the view.
    /// </summary>
    public sealed class HomePageLayout
    {
        public HomePageLayout(double screenHeight, IReadOnlyList<double> groupTops, double cardsTop, double cardHeight)
        {
            ScreenHeight = screenHeight;
            GroupTops = groupTops ?? new double[0];
            CardsTop = cardsTop;
            CardHeight = cardHeight;
        }

        public double ScreenHeight { get; }

        /// <summary>Each Group's top, its header's, down the page, top to bottom.</summary>
        public IReadOnlyList<double> GroupTops { get; }

        /// <summary>How far under a Group's top its cards start.</summary>
        public double CardsTop { get; }

        public double CardHeight { get; }
    }

    /// <summary>
    /// Where Home's page rests: how far it has risen, the Recent row, wide tiles and Groups together, and how far the top
    /// bar has, both in the Dashboard's pixels.
    /// </summary>
    public readonly struct HomeRest
    {
        public HomeRest(double pageOffset, double topBarOffset)
        {
            PageOffset = pageOffset;
            TopBarOffset = topBarOffset;
        }

        public double PageOffset { get; }

        public double TopBarOffset { get; }
    }

    /// <summary>
    /// Home as one page that scrolls, as the console's: the top bar, the hero's gap, the Recent row, the wide tiles, then
    /// the Groups. Where it rests follows focus. On the Recent row it rests as it always has. On the wide tiles it rises
    /// until the first Group's header and the top third of its cards show at the screen's foot; the top bar stays. On a
    /// Group, its header is <see cref="GroupTop"/> from the screen's top, the next Group shows whole and the one after is
    /// cut by the screen's foot; the top bar has gone up with the page from the wide tiles, above the screen.
    /// </summary>
    public static class HomeRests
    {
        /// <summary>How far a Group with focus sits from the screen's top.</summary>
        public const double GroupTop = 64;

        /// <summary>Where the top bar ends, 84 from the screen's top and 60 tall: it is above the screen once risen this far.</summary>
        public const double TopBarBottom = 144;

        public static HomeRest At(HomeFocus focus, HomePageLayout layout)
        {
            var wide = WideTilesOffset(layout);
            switch (focus.Kind)
            {
                case HomeRestKind.WideTiles:
                    return new HomeRest(wide, 0);
                case HomeRestKind.Group when layout.GroupTops.Count > 0:
                    var top = layout.GroupTops[Math.Min(focus.GroupIndex, layout.GroupTops.Count - 1)];
                    var page = top - GroupTop;
                    return new HomeRest(page, Math.Max(page - wide, TopBarBottom));
                default:
                    return new HomeRest(0, 0);
            }
        }

        /// <summary>What the log says as Home comes to rest, for the end-to-end tests: "Home rests at Group My Picks".</summary>
        public static string Says(HomeFocus focus, string groupName) =>
            "Home rests at " + (focus.Kind == HomeRestKind.Group ? $"Group {groupName}" : focus.Kind.ToString());

        private static double WideTilesOffset(HomePageLayout layout) =>
            layout.GroupTops.Count == 0
                ? 0
                : Math.Max(0, layout.GroupTops[0] + layout.CardsTop + layout.CardHeight / 3 - layout.ScreenHeight);
    }
}
