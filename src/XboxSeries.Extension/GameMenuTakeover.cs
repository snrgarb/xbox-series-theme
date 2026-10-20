using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: every place in Playnite's Fullscreen window that opens Playnite's own game menu opens the Tile menu
    /// instead, so the theme has one console-style menu. Those places are Menu (and X) on Playnite's own grid, its
    /// game options button, and the Game hub's Manage and Menu. On the Game hub the Tile menu opens without Manage game.
    /// </summary>
    /// <remarks>
    /// Not in the SDK: each place runs the main view model's OpenGameMenuCommand, which opens the menu for the main
    /// list's selected game. Most are bound in Playnite's own code: the grid's InputBindings, and the hub's
    /// PART_ButtonOptions, set as its template is applied. The hub's Menu is bound in the theme
    /// (GameDetails.xaml), so the theme still opens Playnite's menu without the extension. The command has a private
    /// setter, so it can't be replaced on the model. Instead, each binding or button found holding it is given a command
    /// of the extension's own. The hub's template is applied as it first shows, so the window is looked over again
    /// each time the hub shows.
    /// </remarks>
    internal sealed class GameMenuTakeover
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly ExtensionCore core;
        private readonly ICommand fromGrid;
        private readonly ICommand fromGameHub;

        public GameMenuTakeover(ExtensionCore core)
        {
            this.core = core;
            fromGrid = new TileMenuCommand(this, fromGameHub: false);
            fromGameHub = new TileMenuCommand(this, fromGameHub: true);
        }

        /// <summary>
        /// Gives every binding and button under <paramref name="root"/> that opens Playnite's game menu the Tile menu
        /// instead: those inside <paramref name="gameHub"/> open it without Manage game. Running it again changes only
        /// what Playnite has bound since.
        /// </summary>
        public void TakeOver(DependencyObject root, DependencyObject gameHub)
        {
            var gameMenu = PlayniteHost.FullscreenCommand("OpenGameMenuCommand");
            if (gameMenu != null && root != null)
            {
                Walk(root, gameMenu, gameHub, inGameHub: false);
            }
        }

        private void Walk(DependencyObject node, ICommand gameMenu, DependencyObject gameHub, bool inGameHub)
        {
            inGameHub = inGameHub || node == gameHub;
            var command = inGameHub ? fromGameHub : fromGrid;
            if (node is UIElement element)
            {
                foreach (InputBinding binding in element.InputBindings)
                {
                    // A binding is a Freezable; one frozen can't be changed, so it is left as it is.
                    if (binding.Command == gameMenu && !binding.IsFrozen)
                    {
                        binding.Command = command;
                    }
                }
            }

            if (node is ButtonBase button && button.Command == gameMenu)
            {
                // A local value would replace the binding anyway; cleared first, so it is plain it never comes back.
                BindingOperations.ClearBinding(button, ButtonBase.CommandProperty);
                button.Command = command;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                Walk(VisualTreeHelper.GetChild(node, i), gameMenu, gameHub, inGameHub);
            }
        }

        /// <summary>The main list's selected game, which Playnite's game menu, the grid's focus and the Game hub all follow.</summary>
        private static Guid? SelectedGame()
        {
            var model = Application.Current?.MainWindow?.DataContext;
            var entry = model?.GetType().GetProperty("SelectedGame")?.GetValue(model);
            if (entry?.GetType().GetProperty("Game")?.GetValue(entry) is Playnite.SDK.Models.Game game)
            {
                return game.Id;
            }

            logger.Warn("Can't open the Tile menu in place of Playnite's game menu: this Playnite build has no SelectedGame.Game");
            return null;
        }

        private sealed class TileMenuCommand : ICommand
        {
            private readonly GameMenuTakeover owner;
            private readonly bool fromGameHub;

            public TileMenuCommand(GameMenuTakeover owner, bool fromGameHub)
            {
                this.owner = owner;
                this.fromGameHub = fromGameHub;
            }

            // Always runnable: whether there is a game is asked as it runs.
            public event EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object parameter) => true;

            public void Execute(object parameter)
            {
                if (SelectedGame() is Guid gameId)
                {
                    owner.core.OpenTileMenu(gameId, fromGameHub);
                }
            }
        }
    }
}
