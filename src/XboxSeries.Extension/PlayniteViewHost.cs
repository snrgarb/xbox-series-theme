using System.Linq;
using System.Windows;
using System.Windows.Input;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Playnite's own bindings on its view host, around Home and every page in its place, which answer the pad's buttons
    /// and their keys when no page answers them first. Those that would open Playnite's own UI over the Dashboard are
    /// taken over: Y and the keyboard's Y open Search in place of Playnite's keyboard; RS and F (its filters) and the left
    /// stick's click and R (its filter preset list) go; LB/RB and F2/F3 switch the filter preset only on Full library,
    /// whose tabs they are. The Game hub and text entry sit outside the view host, so it never hears them.
    /// </summary>
    internal static class PlayniteViewHost
    {
        /// <summary>Takes the bindings over. Done again, as the page loads again, it finds nothing left to take.</summary>
        public static void TakeOver(FrameworkElement viewHost, ExtensionCore core)
        {
            var search = PlayniteHost.FullscreenCommand("OpenSearchCommand");
            var filters = PlayniteHost.FullscreenCommand("ToggleFiltersCommand");
            var presetPicker = PlayniteHost.FullscreenCommand("SelectFilterPresetCommand");
            var previousPreset = PlayniteHost.FullscreenCommand("PrevFilterViewCommand");
            var nextPreset = PlayniteHost.FullscreenCommand("NextFilterViewCommand");

            foreach (var binding in viewHost.InputBindings.OfType<InputBinding>().Where(b => b.Command != null).ToList())
            {
                if (binding.Command == search)
                {
                    binding.Command = new RelayCommand(core.SearchFromHome);
                }
                else if (binding.Command == filters || binding.Command == presetPicker)
                {
                    viewHost.InputBindings.Remove(binding);
                }
                else if (binding.Command == previousPreset || binding.Command == nextPreset)
                {
                    binding.Command = FullLibraryOnly(binding.Command, core);
                }
            }
        }

        private static ICommand FullLibraryOnly(ICommand command, ExtensionCore core) =>
            new RelayCommand(() => command.Execute(null), () => core.MyGames.ShowsFullLibrary && command.CanExecute(null));
    }
}
