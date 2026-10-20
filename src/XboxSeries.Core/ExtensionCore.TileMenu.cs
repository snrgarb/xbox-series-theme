using System;
using System.Collections.Generic;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Tile menu: Menu on a game tile, with the console's main rows, the rest behind More options.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The tile menu went from the Dashboard without an answer, as when Playnite leaves the foreground.</summary>
        public void CloseTileMenu()
        {
            if (Home.TileMenu != null)
            {
                StopRepeat();
                Home.TileMenu = null;
            }
        }

        /// <summary>
        /// Menu on a game tile: Play game (Install while it isn't installed, Quit while it runs), Add to or Remove from
        /// Favourites, Manage game, Uninstall for an installed game a library installed, then after a gap Customise Home
        /// (on My games &amp; apps Customise games &amp; apps) and More options.
        /// Each closes the menu first, so focus is back on the tile before anything else opens.
        /// </summary>
        private void ShowTileMenu(HomeTile tile)
        {
            var game = GameById(tile.GameId);
            if (game == null)
            {
                return;
            }

            // A pending A on the tile would otherwise open its Game hub under the menu.
            CancelTap();
            StopRepeat();
            sounds.Play(UiSound.PromptOpen);
            Home.TileMenu = TileMenuFor(game);
        }

        private Dialog TileMenuFor(LibraryGame game)
        {
            var gameId = game.Id;
            Dialog menu = null;
            var rows = new List<DialogOption>
            {
                gameId == runningGame ? new DialogOption("Quit", DialogIcon.Quit, () => FromTileMenu(() => QuitGame(gameId)))
                    : !game.Installed ? new DialogOption("Install", DialogIcon.Install, () => FromTileMenu(() => InstallThroughTheme(gameId)))
                    : new DialogOption("Play game", DialogIcon.Play, () => FromTileMenu(() => PlayGame(gameId))),
                game.Favorite
                    ? new DialogOption("Remove from Favourites", DialogIcon.Unfavourite, () => FromTileMenu(() => playnite.SetFavourite(gameId, false)))
                    : new DialogOption("Add to Favourites", DialogIcon.Favourite, () => FromTileMenu(() => playnite.SetFavourite(gameId, true))),
                new DialogOption("Manage game", DialogIcon.Manage, () => FromTileMenu(() =>
                {
                    ListPlayHistoryInMainList();
                    playnite.OpenGameHub(gameId);
                })),
            };

            // A game added by hand has no library to uninstall it.
            if (game.Installed && !game.IsCustom)
            {
                rows.Add(new DialogOption("Uninstall", DialogIcon.Uninstall, () => AskUninstall(menu, game)));
            }

            rows.Add(new DialogOption(
                MyGames.IsShown ? "Customise games & apps" : "Customise Home", DialogIcon.Customise,
                () => FromTileMenu(() => Customise(gameId))));
            rows.Add(new DialogOption("More options", DialogIcon.MoreOptions, () => FromTileMenu(() =>
            {
                ListPlayHistoryInMainList();
                playnite.OpenGameMenu(gameId);
            })));

            menu = new Dialog(game.Name, null, isPrompt: false, CloseTileMenu, rows.ToArray());
            return menu;
        }

        /// <summary>
        /// "Uninstall X?" in the menu's place. Uninstall goes the theme's way (<see cref="UninstallThroughTheme"/>);
        /// Cancel, or B, goes back to <paramref name="menu"/>, focus still on Uninstall.
        /// </summary>
        private void AskUninstall(Dialog menu, LibraryGame game)
        {
            void Back() => Home.TileMenu = menu;

            Home.TileMenu = new Dialog(
                $"Uninstall {game.Name}?",
                null,
                isPrompt: true,
                Back,
                new DialogOption("Uninstall", DialogIcon.Uninstall, () => FromTileMenu(() => UninstallThroughTheme(game.Id))),
                new DialogOption("Cancel", DialogIcon.Cancel, Back));
        }

        private void FromTileMenu(Action action)
        {
            CloseTileMenu();
            action();
        }
    }
}
