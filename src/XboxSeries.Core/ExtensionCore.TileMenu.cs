using System;
using System.Collections.Generic;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Tile menu: Menu on a game tile, with the console's main rows, the rest behind More options, a flyout over
    /// it. B steps back one level at a time: from a prompt to the panel that asked it, from the flyout to the menu.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The tile menu went from the Dashboard without an answer, as when Playnite leaves the foreground.</summary>
        public void CloseTileMenu()
        {
            if (Home.TileMenu != null)
            {
                StopRepeat();
                Home.TileMenuFlyout = null;
                Home.TileMenu = null;
            }
        }

        /// <summary>
        /// Playnite asked for its own game menu, by a binding or button the extension took over: Menu or Manage on the
        /// Game hub, Menu on Playnite's own grid. The Tile menu opens for <paramref name="gameId"/> in its place, without
        /// Manage game when <paramref name="fromGameHub"/>, as the player is there already. Nothing opens over the Guide,
        /// text entry or a menu already up.
        /// </summary>
        public void OpenTileMenu(Guid gameId, bool fromGameHub)
        {
            var game = GameById(gameId);
            if (game == null || Home.TileMenu != null || Guide.IsOverlayShown || TextEntry.IsShown)
            {
                return;
            }

            StopRepeat();
            sounds.Play(UiSound.PromptOpen);
            Home.TileMenu = TileMenuFor(game, withManageGame: !fromGameHub);
        }

        /// <summary>
        /// Whether a press of <paramref name="button"/> belongs to the tile menu: any while it is up, and one pressed while
        /// it was up until it is released. Playnite routes its own copy of each pad press, and resends of a held one, to
        /// what has focus under the menu, a tile, its grid or the Game hub's buttons; a resend can come after the press
        /// has closed the menu.
        /// </summary>
        public bool TileMenuHolds(ControllerButton button) =>
            (Home.TileMenu != null && !Guide.IsOpen) || tileMenuHeld.Contains(button);

        /// <summary>
        /// Menu on a game tile: Play game (Install while it isn't installed, Quit while it runs), Add to or Remove from
        /// Favourites, Manage game, Uninstall for an installed game a library installed, not running, then after a gap Customise Home
        /// (on My games &amp; apps Customise games &amp; apps) and More options, which opens its flyout over the menu.
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
            Home.TileMenu = TileMenuFor(game, withManageGame: true);
        }

        private Dialog TileMenuFor(LibraryGame game, bool withManageGame)
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
            };

            if (withManageGame)
            {
                rows.Add(new DialogOption("Manage game", DialogIcon.Manage, () => FromTileMenu(() =>
                {
                    ListPlayHistoryInMainList();
                    playnite.OpenGameHub(gameId);
                })));
            }

            // A game added by hand has no library to uninstall it; the running one has its files in use, and Quit first.
            if (game.Installed && !game.IsCustom && gameId != runningGame)
            {
                rows.Add(new DialogOption("Uninstall", DialogIcon.Uninstall, () => AskUninstall(menu, game)));
            }

            rows.Add(new DialogOption(
                MyGames.IsShown ? "Customise games & apps" : "Customise Home", DialogIcon.Customise,
                () => FromTileMenu(() => Customise(gameId))));
            rows.Add(new DialogOption("More options", DialogIcon.MoreOptions, () => ShowMoreOptions(gameId)));

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

        /// <summary>
        /// More options' flyout, read from the library as it opens: a row for each of the game's actions that don't play
        /// it, Hide or Unhide this game, Enable or Disable HDR on an HDR display, Extensions, and Remove. Playnite's Edit
        /// fields isn't offered (#94).
        /// </summary>
        private void ShowMoreOptions(Guid gameId)
        {
            var game = GameById(gameId);
            if (game == null)
            {
                CloseTileMenu();
                return;
            }

            Dialog flyout = null;
            var rows = game.OtherActions
                .Select((name, index) => new DialogOption(name, DialogIcon.GameAction, () => FromTileMenu(() => playnite.RunGameAction(gameId, index))))
                .ToList();
            rows.Add(game.Hidden
                ? new DialogOption("Unhide this game", DialogIcon.Unhide, () => FromTileMenu(() => playnite.SetHidden(gameId, false)))
                : new DialogOption("Hide this game", DialogIcon.Hide, () => FromTileMenu(() => playnite.SetHidden(gameId, true))));
            if (playnite.IsHdrSupported)
            {
                rows.Add(game.SystemHdr
                    ? new DialogOption("Disable HDR", DialogIcon.Hdr, () => FromTileMenu(() => playnite.SetSystemHdr(gameId, false)))
                    : new DialogOption("Enable HDR", DialogIcon.Hdr, () => FromTileMenu(() => playnite.SetSystemHdr(gameId, true))));
            }

            rows.Add(new DialogOption("Extensions", DialogIcon.Extensions, () => FromTileMenu(() => playnite.OpenExtensionsMenu(gameId))));
            rows.Add(new DialogOption("Remove", DialogIcon.RemoveGame, () => AskRemove(flyout, game)));

            StopRepeat();
            flyout = new Dialog("More options", null, isPrompt: false, CloseMoreOptions, rows.ToArray());
            Home.TileMenuFlyout = flyout;
        }

        /// <summary>B on the flyout: back to the menu, focus still on More options.</summary>
        private void CloseMoreOptions()
        {
            StopRepeat();
            Home.TileMenuFlyout = null;
        }

        /// <summary>
        /// "Remove X?" in the flyout's place. Remove takes the game out of the library; Cancel, or B, goes back to
        /// <paramref name="flyout"/>, focus still on Remove.
        /// </summary>
        private void AskRemove(Dialog flyout, LibraryGame game)
        {
            void Back() => Home.TileMenuFlyout = flyout;

            Home.TileMenuFlyout = new Dialog(
                $"Remove {game.Name}?",
                null,
                isPrompt: true,
                Back,
                new DialogOption("Remove", DialogIcon.RemoveGame, () => FromTileMenu(() => playnite.RemoveGame(game.Id))),
                new DialogOption("Cancel", DialogIcon.Cancel, Back));
        }

        private void FromTileMenu(Action action)
        {
            CloseTileMenu();
            action();
        }
    }
}
