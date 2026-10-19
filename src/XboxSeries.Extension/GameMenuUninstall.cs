using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: Playnite's game menu's Uninstall (and so Manage game's, which is the same menu) uninstalls a game its
    /// client can uninstall silently through the core, in place of the library plugin's own route, which for Steam is
    /// <c>steam://uninstall</c> and raises Steam's window and its confirmation over the theme. Any other game, and a game
    /// the core turns down (running, or already being uninstalled), keeps Playnite's own Uninstall, run as before.
    /// </summary>
    /// <remarks>
    /// Not in the SDK: the menu is Fullscreen's GameMenuWindow, whose model builds a list of GameActionItems, each a row's
    /// Command, CommandParameter, Title and Template, and whose own XAML (not the theme's) binds each row's ButtonEx's
    /// Command to its item's. The model's commands are new on each get, and the item tells no one when it changes, so
    /// neither can be changed once the row is built. The row's button can, as it is pressed: a button raises Click and
    /// only then reads its Command to run it (ButtonBase.OnClick), by the pad's A (ButtonEx's KeyDown calls OnClick), a
    /// key or the mouse alike, so a class handler on Click clears the row's binding and sets its Command to one that
    /// asks the core first, and that is the one run. Not as the row loads: Loaded reaches only elements with a handler
    /// of their own, never a class handler (tried on a harness hosting the menu's own item template; it never ran).
    /// The row is known by its Template, the resource Playnite looked up for Uninstall alone
    /// (GameMenuUninstallButtonTemplate), not by its title, which is localised.
    /// </remarks>
    internal static class GameMenuUninstall
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>The template Playnite gives the game menu's Uninstall row, and no other, from the theme.</summary>
        internal const string UninstallTemplateKey = "GameMenuUninstallButtonTemplate";

        /// <summary>Set once the menu's model wasn't as expected, so the warning is logged once, not on each press.</summary>
        private static bool warned;

        /// <summary>Class handlers last for the app's life, so this runs once, in Fullscreen only.</summary>
        public static void Register(ExtensionCore core)
        {
            EventManager.RegisterClassHandler(typeof(ButtonBase), ButtonBase.ClickEvent,
                new RoutedEventHandler((sender, e) => OnClick(core, sender, e)));
        }

        private static void OnClick(ExtensionCore core, object sender, RoutedEventArgs e)
        {
            // Every button's click in the app comes through here, once for each button it bubbles past, so the cheap
            // checks come first.
            if (sender != e.OriginalSource || !(sender is ButtonBase button) || button.Command is SilentUninstallCommand)
            {
                return;
            }

            var item = button.DataContext;
            if (item == null || !IsUninstallItem(item))
            {
                return;
            }

            var menu = Window.GetWindow(button)?.DataContext;
            var gameId = (Get(menu, "Game") as Playnite.SDK.Models.Game)?.Id;
            var close = Get(menu, "CloseCommand") as ICommand;
            var uninstall = button.Command ?? Get(item, "Command") as ICommand;
            if (gameId == null || close == null || uninstall == null)
            {
                WarnOnce("Can't uninstall from the game menu without Steam's own windows: this Playnite build's game menu "
                    + "has no Game, CloseCommand or Uninstall command; its own Uninstall is left as it is");
                return;
            }

            // A local value would replace the one-way binding anyway; cleared first, so it is plain it never comes back.
            BindingOperations.ClearBinding(button, ButtonBase.CommandProperty);
            button.Command = new SilentUninstallCommand(core, gameId.Value, close, uninstall);
        }

        /// <summary>Whether the row's item is the menu's Uninstall: its Template is the one Playnite found for Uninstall.</summary>
        private static bool IsUninstallItem(object item)
        {
            var template = item.GetType().GetProperty("Template")?.GetValue(item);

            // A theme without it leaves each such row's template unset, so unset matches nothing.
            return template != null && template != DependencyProperty.UnsetValue
                && ReferenceEquals(template, Application.Current?.TryFindResource(UninstallTemplateKey));
        }

        private static object Get(object model, string property) =>
            model?.GetType().GetProperty(property)?.GetValue(model);

        private static void WarnOnce(string message)
        {
            if (!warned)
            {
                warned = true;
                logger.Warn(message);
            }
        }

        /// <summary>
        /// The Uninstall row's command: a game the core can uninstall silently closes the menu first, as Playnite's own
        /// Uninstall does, then goes to the core; any other runs Playnite's own, with its row's parameter. Asked as the
        /// row is pressed, not as the menu opens, so a game that started or began uninstalling meanwhile is judged as it is.
        /// </summary>
        private sealed class SilentUninstallCommand : ICommand
        {
            private readonly ExtensionCore core;
            private readonly Guid gameId;
            private readonly ICommand close;
            private readonly ICommand playnites;

            public SilentUninstallCommand(ExtensionCore core, Guid gameId, ICommand close, ICommand playnites)
            {
                this.core = core;
                this.gameId = gameId;
                this.close = close;
                this.playnites = playnites;
            }

            public event EventHandler CanExecuteChanged
            {
                add => playnites.CanExecuteChanged += value;
                remove => playnites.CanExecuteChanged -= value;
            }

            public bool CanExecute(object parameter) => playnites.CanExecute(parameter);

            public void Execute(object parameter)
            {
                if (!core.CanUninstallSilently(gameId))
                {
                    playnites.Execute(parameter);
                    return;
                }

                logger.Info($"Uninstalling {gameId} through its client from the game menu, in place of Playnite's own Uninstall");
                close.Execute(null);
                core.UninstallGame(gameId);
            }
        }
    }
}
