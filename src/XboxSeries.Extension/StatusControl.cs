using System.Windows.Input;
using Playnite.SDK;
using Playnite.SDK.Controls;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Home's top bar's status area, injected as a custom element: the controller's battery and the clock, in place of
    /// Playnite's PC battery and clock. The Dashboard's profile opens the Guide on Profile &amp; system with
    /// <see cref="OpenProfileCommand"/>. A <see cref="PluginUserControl"/>, because Playnite binds any other custom
    /// element's DataContext to the selected game.
    /// </summary>
    public sealed class StatusControl : PluginUserControl
    {
        internal StatusControl(ExtensionCore core)
        {
            Focusable = false;
            IsHitTestVisible = false;
            Content = new StatusArea { DataContext = core.Status };
            OpenProfileCommand = new RelayCommand(core.OpenProfile);
        }

        /// <summary>The top bar's profile: the Guide, on Profile &amp; system.</summary>
        public ICommand OpenProfileCommand { get; }
    }
}
