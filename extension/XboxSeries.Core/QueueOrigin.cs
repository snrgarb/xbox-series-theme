using System;

namespace XboxSeries.Core
{
    /// <summary>Where Manage Queue was opened from, which B on it goes back to.</summary>
    internal enum QueueOriginKind
    {
        /// <summary>Manage's Queue box, or the nav: B goes back to Manage.</summary>
        Manage,

        /// <summary>Home's Downloads tile: B closes the page, back to Home.</summary>
        Home,

        /// <summary>A Game hub's Queue: B opens that hub again, focus on Queue.</summary>
        Hub,
    }

    /// <summary>
    /// Where Manage Queue was opened from. From a Game hub, it keeps what the hub was over, to put back under it: My games
    /// &amp; apps' item (null for Home or Search), the main list's filter (null for the user's own), Full library's name,
    /// and, for a hub over Manage Queue itself, where that one was opened from.
    /// </summary>
    internal sealed class QueueOrigin
    {
        public static readonly QueueOrigin Manage = new QueueOrigin(QueueOriginKind.Manage);
        public static readonly QueueOrigin Home = new QueueOrigin(QueueOriginKind.Home);

        private QueueOrigin(QueueOriginKind kind)
        {
            Kind = kind;
        }

        public static QueueOrigin Hub(Guid gameId, MyGamesItem? overItem, GameListFilter filter, string name, QueueOrigin previous) =>
            new QueueOrigin(QueueOriginKind.Hub)
            {
                GameId = gameId,
                OverItem = overItem,
                Filter = filter,
                Name = name ?? string.Empty,
                Previous = previous,
            };

        public QueueOriginKind Kind { get; }

        /// <summary>The hub's game.</summary>
        public Guid GameId { get; private set; }

        public MyGamesItem? OverItem { get; private set; }

        public GameListFilter Filter { get; private set; }

        public string Name { get; private set; } = string.Empty;

        /// <summary>Where the Manage Queue the hub was over was opened from; null when it wasn't over Manage Queue.</summary>
        public QueueOrigin Previous { get; private set; }
    }
}
